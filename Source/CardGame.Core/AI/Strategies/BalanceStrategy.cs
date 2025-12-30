using CardGame.Core.AI.Interfaces;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Models;
using CardGame.Core.State.Models;
using CardGame.Core.Cards.Logic;
using System;
using System.Linq;

namespace CardGame.Core.AI.Strategies
{
    public class BalanceStrategy : IAIStrategy
    {
        public float Evaluate(GameState state, int botPlayerId)
        {
            var bot = state.GetPlayer(botPlayerId);
            var enemy = state.GetOpponent(botPlayerId);
            float score = 1000.0f;

            // 1. ZASOBY - Bot chce wydać CurrentBlood, by nie marnować tury
            float bloodToSpend = bot.CurrentBlood;
            score -= (bloodToSpend * 15.0f);

            // 2. ŻYCIE - Kluczowe przy końcówkach
            score += (bot.Health - enemy.Health) * 10.0f;
            if (bot.Health < 12) score -= (15 - bot.Health) * 12.0f;

            // 3. PLANSZA - ANALIZA LINII I SPLASH DAMAGE
            bool hasGerard = state.Board.GetAllUnits().Any(u => u.OwnerPlayerId == botPlayerId && u.Definition.Name.Contains("Gerard"));

            for (int i = 0; i < 4; i++)
            {
                var line = state.Board.Lines[i];
                var my = (botPlayerId == 1) ? line.Player1Unit : line.Player2Unit;
                var en = (botPlayerId == 1) ? line.Player2Unit : line.Player1Unit;

                score += EvaluateUnitPresence(my, en, i, state, hasGerard, true) * 20.0f;
                score -= EvaluateUnitPresence(en, my, i, state, false, false) * 22.0f;
            }

            // 4. RĘKA - Kara za zapchanie i bonus za synergię
            score += bot.Hand.Count * 5.0f;
            if (bot.Hand.Count >= 8) score -= 150.0f;

            // 5. BLOKADA POMIJANIA TURY
            if (state.ActivePlayerId == botPlayerId && bot.CurrentBlood > 0)
            {
                if (bot.Hand.Any(c => c.CurrentStats.BloodCost <= bot.CurrentBlood))
                    score -= 100.0f; // Kara za PASS gdy nas stać na ruch
            }

            return score;
        }

        private float EvaluateUnitPresence(CardInstance? u, CardInstance? opp, int lineIdx, GameState state, bool playerHasGerard, bool isFriendly)
        {
            if (u == null) return 0;
            var s = u.CurrentStats;
            float val = (s.Attack * 2.5f) + s.Health;

            // --- LOGIKA SPLASH DAMAGE ---
            if (s.Keywords.Contains(Keyword.SplashDamage))
            {
                int splashPower = 1;
                if (u.Definition.BaseStats.KeywordParams.TryGetValue(Keyword.SplashDamage, out int p)) splashPower = p;

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

            // Analiza walki (Trade analysis)
            if (opp != null)
            {
                bool iDie = s.Health <= opp.CurrentStats.Attack;
                bool enemyDies = opp.CurrentStats.Health <= s.Attack;
                if (iDie && !enemyDies) val -= 25.0f; // Fatalny ruch (oddanie jednostki za nic)
                if (!iDie && enemyDies) val += 15.0f; // Świetny ruch
            }

            if (s.Keywords.Contains(Keyword.Marked)) val += isFriendly ? -40 : (playerHasGerard ? 50 : 15);
            if (s.Keywords.Contains(Keyword.Stunned)) val *= 0.1f;
            if (s.Keywords.Contains(Keyword.Unkillable)) val += 15.0f;

            return val;
        }
    }
}