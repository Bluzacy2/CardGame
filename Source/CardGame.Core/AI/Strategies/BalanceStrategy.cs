using CardGame.Core.AI.Interfaces;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Logic;
using CardGame.Core.Cards.Models;
using CardGame.Core.State.Enums;
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
            float score = 1000.0f;

            // 1. ANALIZA RĘKI - CZY JESTEM AGRESOREM?
            int unitCount = bot.Hand.Count(c => c.Definition.Type == CardType.Unit);
            int spellCount = bot.Hand.Count(c => c.Definition.Type == CardType.Spell);
            bool isAggroMood = unitCount > spellCount; // Jeśli mam więcej jednostek, chcę spamić

            // 2. DYNAMICZNA MANA
            float spentBlood = bot.MaxBlood - bot.CurrentBlood;
            if (state.CurrentPhase == GamePhase.UnitOnly)
            {
                // Jeśli mam nastroj agresywny (Sacrifice), wydawanie many na jednostki jest OK (+20)
                // Jeśli mam nastroj kontrolny, wydawanie jest ryzykowne (+5)
                score += spentBlood * (isAggroMood ? 20.0f : 5.0f);
            }
            else
            {
                score += spentBlood * 25.0f; // W późniejszych fazach zawsze chcemy wydać wszystko
            }

            // 3. ŻYCIE
            score += (bot.Health - enemy.Health) * 10.0f;

            // 4. PLANSZA - ANALIZA LINII I SPLASH DAMAGE
            bool hasGerard = state.Board.GetAllUnits().Any(u => u.OwnerPlayerId == botPlayerId && u.Definition.Name.Contains("Gerard"));

            for (int i = 0; i < 4; i++)
            {
                var line = state.Board.Lines[i];
                var my = (botPlayerId == 1) ? line.Player1Unit : line.Player2Unit;
                var en = (botPlayerId == 1) ? line.Player2Unit : line.Player1Unit;

                score += EvaluateUnitPresence(my, en, i, state, hasGerard, true) * 20.0f;
                score -= EvaluateUnitPresence(en, my, i, state, false, false) * 22.0f;
            }

            // 5. RĘKA
            score += bot.Hand.Count * 10.0f;
            return score;
        }

        private float EvaluateUnitPresence(CardInstance? u, CardInstance? opp, int lineIdx, GameState state, bool g, bool f)
        {
            if (u == null) return 0;
            var s = u.CurrentStats;
            float val = (s.Attack * 2.0f) + s.Health;

            // --- LOGIKA SPLASH DAMAGE ---
            if (s.Keywords.Contains(Keyword.SplashDamage))
            {
                // Pobieramy wartość splasha z parametrów (np. 3)
                int splashPower = 1;
                if (u.Definition.BaseStats.KeywordParams.TryGetValue(Keyword.SplashDamage, out int p)) splashPower = p;

                // Sprawdzamy sąsiadów na planszy
                int targetsHit = 0;
                foreach (int neighborIdx in new[] { lineIdx - 1, lineIdx + 1 })
                {
                    if (neighborIdx >= 0 && neighborIdx < 4)
                    {
                        var neighborLine = state.Board.Lines[neighborIdx];
                        var potentialVictim = (u.OwnerPlayerId == 1) ? neighborLine.Player2Unit : neighborLine.Player1Unit;
                        if (potentialVictim != null) targetsHit++;
                    }
                }

                // Splash jest wart tym więcej, im więcej jednostek faktycznie uderzy
                val += (targetsHit * splashPower * 1.5f);

                // Bonus za samo "dobre pozycjonowanie" (środkowe linie dają potencjał na 2 cele)
                if (lineIdx == 1 || lineIdx == 2) val += 2.0f;
            }

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
    }
}