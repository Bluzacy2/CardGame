using CardGame.Core.AI.Interfaces;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Models;
using CardGame.Core.State.Models;
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

            // --- KLUCZOWA POPRAWKA: DETEKCJA KOŃCA GRY ---
            if (bot.Health <= 0) return -1000000f; // Przegrana to absolutne dno
            if (enemy.Health <= 0) return 1000000f; // Wygrana to absolutny priorytet

            float score = 2000.0f;

            // 1. MANA
            float spentBlood = bot.MaxBlood - bot.CurrentBlood;
            score += spentBlood * 20.0f;

            // 2. ŻYCIE (Bardzo wysoka waga przetrwania)
            score += bot.Health * 50.0f;
            score -= enemy.Health * 30.0f;

            // 3. PLANSZA
            bool hasGerard = state.Board.GetAllUnits().Any(u => u.OwnerPlayerId == botPlayerId && u.Definition.Name.Contains("Gerard"));

            foreach (var line in state.Board.Lines)
            {
                var my = (botPlayerId == 1) ? line.Player1Unit : line.Player2Unit;
                var en = (botPlayerId == 1) ? line.Player2Unit : line.Player1Unit;

                score += ScoreUnit(my, en, hasGerard, true) * 25.0f;
                score -= ScoreUnit(en, my, false, false) * 35.0f; // Wrogowie są teraz "drożsi" (bot chce ich zabijać)
            }

            // 4. KARA ZA PASS (Gdy grozi śmierć lub mamy ruchy)
            if (state.ActivePlayerId == botPlayerId && bot.CurrentBlood > 0)
            {
                if (bot.Hand.Any(c => c.CurrentStats.BloodCost <= bot.CurrentBlood))
                    score -= 500.0f;
            }

            return score;
        }

        private float ScoreUnit(CardInstance? u, CardInstance? opp, bool g, bool f)
        {
            if (u == null) return 0;
            var s = u.CurrentStats;

            // POPRAWKA: Atak wroga jest groźniejszy niż jego HP (priorytet dla Glocka)
            float val = f ? (s.Attack * 3.0f + s.Health * 2.0f)
                          : (s.Attack * 6.0f + s.Health * 1.5f);

            if (s.Keywords.Contains(Keyword.Marked)) val += f ? -50 : (g ? 100 : 30);
            if (s.Keywords.Contains(Keyword.Unkillable)) val += 20;

            return val;
        }
    }
}