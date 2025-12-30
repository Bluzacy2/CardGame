using CardGame.Core.AI.Interfaces;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Logic;
using CardGame.Core.Cards.Models;
using CardGame.Core.State.Enums;
using CardGame.Core.State.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CardGame.Core.AI.Strategies
{
    public class BalanceStrategy : IAIStrategy
    {
        // Wagi globalne do łatwego tuningu
        private const float HealthWeight = 15.0f;
        private const float BoardWeight = 25.0f;
        private const float HandValueWeight = 12.0f;

        public float Evaluate(GameState state, int botPlayerId)
        {
            var bot = state.GetPlayer(botPlayerId);
            var enemy = state.GetOpponent(botPlayerId);

            // --- KLUCZOWA POPRAWKA: DETEKCJA KOŃCA GRY (z HEAD) ---
            if (bot.Health <= 0) return -1000000f; // Przegrana to absolutne dno
            if (enemy.Health <= 0) return 1000000f; // Wygrana to absolutny priorytet

            float score = 2000.0f; // Baza punktowa

            // --- 1. ANALIZA STRATEGII I NASTROJU ---
            var hand = bot.Hand;
            int unitCount = hand.Count(c => c.Definition.Type == CardType.Unit);
            int spellCount = hand.Count(c => c.Definition.Type == CardType.Spell);

            bool isMarkStrategy = hand.Any(IsMarkCard) || state.Board.GetAllUnits().Any(u => u.OwnerPlayerId == botPlayerId && IsMarkCard(u));
            bool isSacrificeStrategy = hand.Any(IsSacrificeCard) || state.Board.GetAllUnits().Any(u => u.OwnerPlayerId == botPlayerId && IsSacrificeCard(u));

            // AggroMood: Jeśli mamy dużo jednostek, gramy ofensywnie
            bool isAggroMood = unitCount > spellCount;

            // --- 2. ZASOBY I TEMPO ---
            float spentBlood = bot.MaxBlood - bot.CurrentBlood;
            if (state.CurrentPhase == GamePhase.UnitOnly)
            {
                score += spentBlood * (isAggroMood ? 20.0f : 5.0f);
            }
            else
            {
                score += spentBlood * 25.0f;
            }

            // Kara za PASS, gdy stać nas na zagranie czegokolwiek (zwiększona kara z HEAD)
            if (state.ActivePlayerId == botPlayerId && bot.CurrentBlood > 0)
            {
                if (hand.Any(c => c.CurrentStats.BloodCost <= bot.CurrentBlood))
                    score -= 300.0f;
            }

            // --- 3. ŻYCIE BOHATERA (Nieliniowe) ---
            float hpDiff = bot.Health - enemy.Health;
            score += hpDiff * HealthWeight;

            // Panic Mode: Poniżej 12 HP bot traktuje każdy punkt życia jak skarb
            if (bot.Health < 12)
                score -= (15 - bot.Health) * 20.0f;

            // Lethal focus: Jeśli wróg ma mało HP, bot staje się agresywny
            if (enemy.Health < 5)
                score += 200.0f;

            // --- 4. PLANSZA (Linie, Splash, Trade Analysis) ---
            bool hasGerard = state.Board.GetAllUnits().Any(u => u.OwnerPlayerId == botPlayerId && u.Definition.Name.Contains("Gerard"));
            var enemyUnits = state.Board.GetAllUnits().Where(u => u.OwnerPlayerId != botPlayerId).ToList();

            for (int i = 0; i < 4; i++)
            {
                var line = state.Board.Lines[i];
                var myUnit = (botPlayerId == 1) ? line.Player1Unit : line.Player2Unit;
                var enUnit = (botPlayerId == 1) ? line.Player2Unit : line.Player1Unit;

                // Sumujemy obecność na planszy (wrogowie są nieco "drożsi", by bot chciał ich usuwać)
                score += EvaluateUnitPresence(myUnit, enUnit, i, state, hasGerard, true, isMarkStrategy, isSacrificeStrategy) * BoardWeight;
                score -= EvaluateUnitPresence(enUnit, myUnit, i, state, false, false, false, false) * (BoardWeight * 1.15f);
            }

            // --- 5. ANALIZA RĘKI (Potencjał opcji) ---
            foreach (var card in hand)
            {
                score += EvaluateCardInHand(card, enemyUnits, isMarkStrategy, isSacrificeStrategy) * HandValueWeight;
            }

            // Kara za zapchaną rękę (Overdraw risk)
            if (hand.Count >= 8) score -= 100.0f;

            return score;
        }

        private float EvaluateUnitPresence(CardInstance? u, CardInstance? opp, int lineIdx, GameState state, bool playerHasGerard, bool isFriendly, bool isMarkStrat, bool isSacStrat)
        {
            if (u == null) return 0;
            var s = u.CurrentStats;

            // Podstawa: Atak (Presja) + HP (Przeżywalność)
            // Jeśli to wróg, jego atak jest dla nas groźniejszy niż jego HP (priorytet eliminacji)
            float val = isFriendly
                ? (s.Attack * 2.5f + s.Health * 1.5f)
                : (s.Attack * 5.0f + s.Health * 1.5f);

            // Bonus za Splash Damage
            if (s.Keywords.Contains(Keyword.SplashDamage))
            {
                int splashPower = u.Definition.BaseStats.KeywordParams.GetValueOrDefault(Keyword.SplashDamage, 1);
                int hits = CountSplashTargets(u, lineIdx, state);
                val += (hits * splashPower * 2.5f);
            }

            // --- TRADE ANALYSIS ---
            if (opp != null)
            {
                bool iDie = s.Health <= opp.CurrentStats.Attack;
                bool enemyDies = opp.CurrentStats.Health <= s.Attack;

                if (iDie && !enemyDies) val -= 20.0f; // Fatalny trade
                if (!iDie && enemyDies) val += 15.0f; // Świetny trade
            }

            // --- STATUSY I SYNERGIE ---
            if (s.Keywords.Contains(Keyword.Marked))
            {
                if (isFriendly) val -= 40.0f;
                else val += playerHasGerard ? 60.0f : 20.0f;
            }

            if (s.Keywords.Contains(Keyword.Stunned)) val *= 0.25f;

            if (s.Keywords.Contains(Keyword.Unkillable)) val += 25.0f;

            if (isSacStrat && (u.Definition.Id == "12" || u.Definition.Id == "3"))
                val += 15.0f;

            return val;
        }

        private float EvaluateCardInHand(CardInstance card, List<CardInstance> enemyUnits, bool isMarkStrat, bool isSacStrat)
        {
            float val = 1.0f;

            if (isMarkStrat && IsMarkCard(card)) val += 2.0f;
            if (isSacStrat && IsSacrificeCard(card)) val += 2.0f;
            if (card.CurrentStats.Keywords.Contains(Keyword.Unkillable)) val += 2.5f;

            if (card.Definition.Type == CardType.Spell && card.Definition.Effects.Any(e => e.Actions.Any(a => a.Type == ActionType.DealDamage)))
            {
                if (enemyUnits.Any(u => u.CurrentStats.Health <= 3)) val += 3.0f;
            }

            return val;
        }

        private int CountSplashTargets(CardInstance u, int lineIdx, GameState state)
        {
            int hits = 0;
            int opponentId = 3 - u.OwnerPlayerId;
            foreach (int n in new[] { lineIdx - 1, lineIdx + 1 })
            {
                if (n >= 0 && n < 4)
                {
                    var neighborLine = state.Board.Lines[n];
                    var potentialVictim = (opponentId == 1) ? neighborLine.Player1Unit : neighborLine.Player2Unit;
                    if (potentialVictim != null) hits++;
                }
            }
            return hits;
        }

        private bool IsMarkCard(CardInstance c) => new[] { "4", "32", "24", "25", "26" }.Contains(c.Definition.Id);
        private bool IsSacrificeCard(CardInstance c) => new[] { "12", "10", "11", "3", "9" }.Contains(c.Definition.Id);
    }
}