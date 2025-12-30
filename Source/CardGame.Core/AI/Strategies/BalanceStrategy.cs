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
        // Wagi globalne
        private const float HealthWeight = 15.0f;
        private const float BoardWeight = 25.0f;
        private const float HandValueWeight = 12.0f;
        private const float ResourceEfficiencyWeight = 5.0f;

        public float Evaluate(GameState state, int botPlayerId)
        {
            var bot = state.GetPlayer(botPlayerId);
            var enemy = state.GetOpponent(botPlayerId);
            float score = 2000.0f; // Wyższa baza dla precyzji

            // 1. IDENTYFIKACJA STRATEGII (Na podstawie kart na stole i w ręce)
            bool isMarkStrategy = bot.Hand.Any(c => IsMarkCard(c)) || state.Board.GetAllUnits().Any(u => u.OwnerPlayerId == botPlayerId && IsMarkCard(u));
            bool isSacrificeStrategy = bot.Hand.Any(c => IsSacrificeCard(c)) || state.Board.GetAllUnits().Any(u => u.OwnerPlayerId == botPlayerId && IsSacrificeCard(u));

            // 2. ZASOBY I TEMPO
            float bloodUsage = bot.MaxBlood - bot.CurrentBlood;
            score += bloodUsage * ResourceEfficiencyWeight;

            // Kara za trzymanie zbyt wielu kart (overdraw risk / brak tempa)
            if (bot.Hand.Count > 6) score -= 20.0f;

            // 3. ŻYCIE BOHATERA (Nieliniowe - panika poniżej 10 HP)
            float hpDiff = bot.Health - enemy.Health;
            score += hpDiff * HealthWeight;
            if (bot.Health < 10) score -= (10 - bot.Health) * 30.0f;
            if (enemy.Health < 5) score += 100.0f; // Blisko wygranej!

            // 4. ANALIZA PLANSZY
            var myUnits = state.Board.GetAllUnits().Where(u => u.OwnerPlayerId == botPlayerId).ToList();
            var enemyUnits = state.Board.GetAllUnits().Where(u => u.OwnerPlayerId != botPlayerId).ToList();

            bool hasGerard = myUnits.Any(u => u.Definition.Name.Contains("Gerard"));

            for (int i = 0; i < 4; i++)
            {
                var line = state.Board.Lines[i];
                var my = (botPlayerId == 1) ? line.Player1Unit : line.Player2Unit;
                var en = (botPlayerId == 1) ? line.Player2Unit : line.Player1Unit;

                score += EvaluateUnit(my, en, i, state, hasGerard, true, isMarkStrategy, isSacrificeStrategy) * BoardWeight;
                score -= EvaluateUnit(en, my, i, state, false, false, false, false) * (BoardWeight * 1.1f);
            }

            // 5. POTENCJAŁ RĘKI (Karty to nie tylko punkty, to opcje)
            foreach (var card in bot.Hand)
            {
                score += EvaluateCardInHand(card, enemyUnits, isMarkStrategy, isSacrificeStrategy) * HandValueWeight;
            }

            return score;
        }

        private float EvaluateUnit(CardInstance? u, CardInstance? opp, int lineIdx, GameState state, bool hasGerard, bool isFriendly, bool isMarkStrat, bool isSacStrat)
        {
            if (u == null) return 0;
            var s = u.CurrentStats;

            // Podstawa: Siła ognia + wytrzymałość
            float val = (s.Attack * 1.8f) + (s.Health * 1.2f);

            // Synergia: Unkillable / Black Cat w strategii Sacrifice
            if (isSacStrat && (u.Definition.Id == "12" || s.Keywords.Contains(Keyword.Unkillable)))
                val += 15.0f; // Wyższa wartość dla "paliwa"

            // Synergia: Gerard / Marki
            if (s.Keywords.Contains(Keyword.Marked))
            {
                if (isFriendly) val -= 25.0f; // Bycie oznaczonym to ogromne ryzyko
                else val += hasGerard ? 45.0f : 15.0f; // Przeciwnik z marką to cel/zasób
            }

            // Splash Damage (Pozycjonowanie)
            if (s.Keywords.Contains(Keyword.SplashDamage))
            {
                int targets = CountSplashTargets(u, lineIdx, state);
                val += (targets * 10.0f) + 5.0f;
            }

            // Przeżywalność (Combat Projection Lite)
            if (opp != null)
            {
                bool iDie = s.Health <= opp.CurrentStats.Attack;
                bool heDies = opp.CurrentStats.Health <= s.Attack;

                if (iDie && !heDies) val -= 20.0f; // Zły trade
                if (!iDie && heDies) val += 15.0f; // Dobry trade
                if (s.Keywords.Contains(Keyword.Stunned)) val *= 0.3f; // Ogłuszony jest prawie bezużyteczny w tej turze
            }

            return val;
        }

        private float EvaluateCardInHand(CardInstance card, List<CardInstance> enemyUnits, bool isMarkStrat, bool isSacStrat)
        {
            float val = 1.0f;

            // Bonusy za synergię z archetypem
            if (isMarkStrat && IsMarkCard(card)) val += 1.5f;
            if (isSacStrat && IsSacrificeCard(card)) val += 1.5f;

            // Skalowanie czarów ofensywnych (np. Glock-17)
            if (card.Definition.Type == CardType.Spell && card.Definition.Effects.Any(e => e.Actions.Any(a => a.Type == ActionType.DealDamage)))
            {
                // Jeśli wróg ma jednostki, czar niszczący ma dużą wartość
                if (enemyUnits.Any(u => u.CurrentStats.Health <= 3)) val += 2.0f;
            }

            // Unkillable jest zawsze dobre jako inwestycja
            if (card.CurrentStats.Keywords.Contains(Keyword.Unkillable)) val += 2.5f;

            return val;
        }

        private int CountSplashTargets(CardInstance u, int lineIdx, GameState state)
        {
            int hits = 0;
            int opponentId = 3 - u.OwnerPlayerId;
            foreach (int n in new[] { lineIdx - 1, lineIdx + 1 })
            {
                if (n >= 0 && n < 4 && !state.Board.Lines[n].IsSlotEmpty(opponentId)) hits++;
            }
            return hits;
        }

        // Pomocnicze do detekcji archetypu
        private bool IsMarkCard(CardInstance c) => new[] { "4", "24", "25", "26", "32" }.Contains(c.Definition.Id);
        private bool IsSacrificeCard(CardInstance c) => new[] { "10", "12", "3", "900" }.Contains(c.Definition.Id);
    }
}