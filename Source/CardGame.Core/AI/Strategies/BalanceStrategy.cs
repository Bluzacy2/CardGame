using CardGame.Core.AI.Interfaces;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Models;
using CardGame.Core.State.Models;
using CardGame.Core.Cards.Logic;
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
            bool isAggroMood = unitCount > spellCount; // Jeśli mam więcej jednostek, chcę spamić
            // 2. ZASOBY I TEMPO
            float bloodUsage = bot.MaxBlood - bot.CurrentBlood;
            score += bloodUsage * ResourceEfficiencyWeight;

            // Kara za trzymanie zbyt wielu kart (overdraw risk / brak tempa)
            if (bot.Hand.Count > 6) score -= 20.0f;
                // Jeśli mam nastroj kontrolny, wydawanie jest ryzykowne (+5)
            // 3. ŻYCIE BOHATERA (Nieliniowe - panika poniżej 10 HP)
            float hpDiff = bot.Health - enemy.Health;
            score += hpDiff * HealthWeight;
            if (bot.Health < 10) score -= (10 - bot.Health) * 30.0f;
            if (enemy.Health < 5) score += 100.0f; // Blisko wygranej!
            else
            {
                score += spentBlood * 25.0f; // W późniejszych fazach zawsze chcemy wydać wszystko
            }

            // 3. ŻYCIE
            score += (bot.Health - enemy.Health) * 10.0f;

            // 4. PLANSZA - ANALIZA LINII I SPLASH DAMAGE
            bool hasGerard = state.Board.GetAllUnits().Any(u => u.OwnerPlayerId == botPlayerId && u.Definition.Name.Contains("Gerard"));

            // 4. ANALIZA PLANSZY
            var myUnits = state.Board.GetAllUnits().Where(u => u.OwnerPlayerId == botPlayerId).ToList();
            var enemyUnits = state.Board.GetAllUnits().Where(u => u.OwnerPlayerId != botPlayerId).ToList();

            bool hasGerard = myUnits.Any(u => u.Definition.Name.Contains("Gerard"));

            // 4. RĘKA - Kara za zapchanie i bonus za synergię
            score += bot.Hand.Count * 5.0f;
            if (bot.Hand.Count >= 8) score -= 150.0f;

            // 5. BLOKADA POMIJANIA TURY
            if (state.ActivePlayerId == botPlayerId && bot.CurrentBlood > 0)
            {
                if (bot.Hand.Any(c => c.CurrentStats.BloodCost <= bot.CurrentBlood))
                    score -= 100.0f; // Kara za PASS gdy nas stać na ruch
            }

                var line = state.Board.Lines[i];
                var my = (botPlayerId == 1) ? line.Player1Unit : line.Player2Unit;
                var en = (botPlayerId == 1) ? line.Player2Unit : line.Player1Unit;
        private float EvaluateUnit(CardInstance? u, CardInstance? opp, int lineIdx, GameState state, bool hasGerard, bool isFriendly, bool isMarkStrat, bool isSacStrat)
                score += EvaluateUnit(my, en, i, state, hasGerard, true, isMarkStrategy, isSacrificeStrategy) * BoardWeight;
                score -= EvaluateUnit(en, my, i, state, false, false, false, false) * (BoardWeight * 1.1f);
            }

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
            // 5. RĘKA
            score += bot.Hand.Count * 10.0f;
            return score;
        }
                int splashPower = 1;
                if (u.Definition.BaseStats.KeywordParams.TryGetValue(Keyword.SplashDamage, out int p)) splashPower = p;
            if (u == null) return 0;
                int actualHits = 0;
                foreach (int neighbor in new[] { lineIdx - 1, lineIdx + 1 })
                {
                    if (neighbor >= 0 && neighbor < 4)
                    {
                        var nLine = state.Board.Lines[neighbor];
                        var potentialVictim = (u.OwnerPlayerId == 1) ? nLine.Player2Unit : nLine.Player1Unit;
                        if (potentialVictim != null) actualHits++;
                    }
                }
                // Wyższa wartość na liniach 1 i 2 (środek planszy)
                val += (actualHits * splashPower * 2.0f);
                if (lineIdx == 1 || lineIdx == 2) val += 5.0f;
            }
                        if (potentialVictim != null) targetsHit++;
            // Analiza walki (Trade analysis)
            if (opp != null)
            // Skalowanie czarów ofensywnych (np. Glock-17)
            if (card.Definition.Type == CardType.Spell && card.Definition.Effects.Any(e => e.Actions.Any(a => a.Type == ActionType.DealDamage)))

                bool iDie = s.Health <= opp.CurrentStats.Attack;
                bool enemyDies = opp.CurrentStats.Health <= s.Attack;
                if (iDie && !enemyDies) val -= 25.0f; // Fatalny ruch (oddanie jednostki za nic)
                if (!iDie && enemyDies) val += 15.0f; // Świetny ruch
                // Jeśli wróg ma jednostki, czar niszczący ma dużą wartość
                if (enemyUnits.Any(u => u.CurrentStats.Health <= 3)) val += 2.0f;

                // Bonus za samo "dobre pozycjonowanie" (środkowe linie dają potencjał na 2 cele)
            if (s.Keywords.Contains(Keyword.Marked)) val += isFriendly ? -40 : (playerHasGerard ? 50 : 15);
            if (s.Keywords.Contains(Keyword.Stunned)) val *= 0.1f;
            if (s.Keywords.Contains(Keyword.Unkillable)) val += 15.0f;
            // Unkillable jest zawsze dobre jako inwestycja
            if (card.CurrentStats.Keywords.Contains(Keyword.Unkillable)) val += 2.5f;
            // Analiza walki
            if (opp != null)
            {
                if (s.Health <= opp.CurrentStats.Attack && opp.CurrentStats.Health > s.Attack) val -= 15.0f;
                if (opp.CurrentStats.Health <= s.Attack && s.Health > opp.CurrentStats.Attack) val += 10.0f;
            }

            if (s.Keywords.Contains(Keyword.Marked)) val += f ? -30 : (g ? 40 : 10);
            if (s.Keywords.Contains(Keyword.Stunned)) val *= 0.1f;
            if (s.Keywords.Contains(Keyword.Unkillable)) val += 15.0f;

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