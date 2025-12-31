using CardGame.Core.AI.Interfaces;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Models;
using CardGame.Core.State.Enums;
using CardGame.Core.State.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CardGame.Core.AI.Strategies
{
    public class EvolvableStrategy : IAIStrategy
    {
        public float[] DNA { get; }

        public EvolvableStrategy(float[] genes)
        {
            // Zabezpieczenie rozmiaru DNA (aktualizacja do 109 genów)
            if (genes != null && genes.Length < 109)
            {
                var newDna = new float[109];
                Array.Copy(genes, newDna, genes.Length);
                DNA = newDna;
            }
            else
            {
                DNA = genes ?? new float[109];
            }
        }

        public float Evaluate(GameState state, int botId)
        {
            var bot = state.GetPlayer(botId);
            var enemy = state.GetOpponent(botId);

            // Natychmiastowe stany końca gry
            if (enemy.Health <= 0 && bot.Health > 0) return 10000000f;
            if (bot.Health <= 0) return -10000000f;

            // Archetypy i wagi nastroju
            float aggroFactor = DNA[65] * (bot.Health > 15 ? 1.0f : DNA[90]);
            float controlFactor = DNA[66];
            float tempoFactor = DNA[67];
            float valueFactor = DNA[68];
            float comboFactor = DNA[108]; // NOWY GEN

            float totalScore = 0f;

            // Główna analiza
            totalScore += EvaluateMacroEconomy(state, bot, enemy, valueFactor, comboFactor);
            totalScore += EvaluateBoardGeometry(state, botId, tempoFactor);
            totalScore += EvaluateUnitsAndSynergies(state, botId, aggroFactor, controlFactor, comboFactor);
            totalScore += EvaluateForecasting(state, bot, enemy);

            // Specyficzna punktacja dla Combo: Premia za zniżki kosztów w ręku
            float totalDiscounts = bot.Hand.Sum(c => c.CostReduction);
            totalScore += totalDiscounts * 25.0f * comboFactor;

            return totalScore;
        }

        private float EvaluateMacroEconomy(GameState state, PlayerState bot, PlayerState enemy, float valueFactor, float comboFactor)
        {
            float score = 0;

            // Życie bohatera
            float selfHpValue = bot.Health * DNA[0];
            if (bot.Health < 10) selfHpValue *= DNA[2]; // Panic threshold
            score += selfHpValue;

            float enemyHpLoss = (30 - enemy.Health) * DNA[1];
            if (enemy.Health < 10) enemyHpLoss *= DNA[3]; // Lethal fever
            score += enemyHpLoss;

            // Wartość kart na ręce (wzmocniona przez Combo)
            float handValueMultiplier = 1.0f + (comboFactor * 0.5f);
            score += bot.Hand.Count * DNA[6] * valueFactor * handValueMultiplier;

            if (bot.Hand.Count >= 7) score -= DNA[7]; // Overdraw risk

            // Ekonomia zasobów (Gen 105 - ResourceGainValue)
            score += bot.CurrentBlood * DNA[105];

            if (state.ActivePlayerId == bot.PlayerId)
            {
                float unusedBloodPenalty = bot.CurrentBlood * DNA[4];
                if (state.CurrentPhase == GamePhase.UnitOnly)
                    unusedBloodPenalty *= DNA[5];
                score -= unusedBloodPenalty;
            }

            // Finishery w talii
            float finishersInDeck = bot.DrawPile.Count(c => c.CurrentStats.Attack >= 5);
            score += finishersInDeck * DNA[46] * DNA[48];

            return score;
        }

        private float EvaluateBoardGeometry(GameState state, int botId, float tempoFactor)
        {
            float score = 0;
            int occupiedLines = state.Board.Lines.Count(l => !l.IsSlotEmpty(botId));
            score += occupiedLines * DNA[77] * tempoFactor;

            foreach (var line in state.Board.Lines)
            {
                var myU = botId == 1 ? line.Player1Unit : line.Player2Unit;
                var enU = botId == 1 ? line.Player2Unit : line.Player1Unit;

                if (myU != null)
                {
                    if (enU == null) score += DNA[12]; // Empty lane bonus
                    if (line.Index == 0 || line.Index == 3) score += DNA[78]; // Corner value
                    if (HasNeighbor(state, line.Index, botId)) score += DNA[14]; // Adjacency

                    // Priorytety linii (Geny 100-103)
                    score += myU.CurrentStats.Attack * DNA[100 + line.Index];
                }
            }
            return score;
        }

        private float EvaluateUnitsAndSynergies(GameState state, int botId, float aggroFactor, float controlFactor, float comboFactor)
        {
            float score = 0;
            var units = state.Board.GetAllUnits();

            foreach (var u in units)
            {
                bool isFriendly = u.OwnerPlayerId == botId;
                var s = u.CurrentStats;
                float uVal = 0;
                int lineIdx = GetLineIndex(state, u.InstanceId);

                // Bazowe statystyki z uwzględnieniem archetypów
                if (isFriendly)
                {
                    uVal += s.Attack * DNA[8] * aggroFactor;
                    uVal += s.Health * DNA[9];
                    if (lineIdx != -1) uVal += s.Attack * DNA[100 + lineIdx];
                }
                else
                {
                    uVal += s.Attack * DNA[10] * controlFactor;
                    uVal += s.Health * DNA[11];
                    if (lineIdx != -1) uVal += s.Attack * DNA[100 + lineIdx];
                }

                // Keywordy i statusy
                if (s.Keywords.Contains(Keyword.Marked))
                {
                    float markBonus = isFriendly ? -DNA[16] : DNA[15];
                    uVal += markBonus;
                }

                if (s.Keywords.Contains(Keyword.Unkillable)) uVal += DNA[18];
                if (s.Keywords.Contains(Keyword.SoulGuard)) uVal += DNA[19];

                // NOWE: Flying (Gen 104)
                if (s.Keywords.Contains(Keyword.Flying)) uVal += DNA[104] * 2.0f;

                if (s.Keywords.Contains(Keyword.SplashDamage) && lineIdx != -1)
                {
                    int neighborCount = CountEnemyNeighbors(state, lineIdx, botId);
                    uVal += neighborCount * DNA[22];
                }

                // Podtypy i synergie (NOWE: DNA[107] dla Maszyn)
                if (u.Definition.Subtypes.Contains("Monster")) uVal += DNA[27];
                if (u.Definition.Subtypes.Contains("Mercenary")) uVal += DNA[28];
                if (u.Definition.Subtypes.Contains("Machine")) uVal += DNA[30] + DNA[107];

                // NOWE: Token Synergy (Gen 106)
                if (u.Definition.Id == "901" || u.Definition.Id == "500") uVal += DNA[106];

                // Buff stałe (Gen 49)
                if (u.PermanentBuffs.Attack > 0 || u.PermanentBuffs.Health > 0)
                {
                    uVal += (u.PermanentBuffs.Attack + u.PermanentBuffs.Health) * DNA[49];
                }

                // Specyficzne dla Combo: Tea Maid (ID 45)
                if (isFriendly && u.Definition.Id == "45") uVal += 60.0f * comboFactor;

                score += isFriendly ? uVal : -uVal;
            }

            return score;
        }

        private float EvaluateForecasting(GameState state, PlayerState bot, PlayerState enemy)
        {
            float score = 0;
            // Przewidywanie AoE wroga
            if (state.Board.GetAllUnits().Count(u => u.OwnerPlayerId == bot.PlayerId) >= 3)
            {
                if (enemy.MaxBlood >= 4) score -= DNA[85]; // Fear of Hellfire
            }

            score -= enemy.Hand.Count * DNA[44]; // Enemy hand size fear

            // Ryzyko Bluffu
            if (enemy.CurrentBlood > 0 && state.ActivePlayerId == bot.PlayerId)
                score -= DNA[81];

            return score;
        }

        // --- HELPERY GEOMETRYCZNE ---

        private int GetLineIndex(GameState s, int id)
        {
            for (int i = 0; i < 4; i++)
                if (s.Board.Lines[i].Player1Unit?.InstanceId == id || s.Board.Lines[i].Player2Unit?.InstanceId == id) return i;
            return -1;
        }

        private bool HasNeighbor(GameState s, int lineIdx, int myId)
        {
            foreach (int i in new[] { lineIdx - 1, lineIdx + 1 })
            {
                if (i >= 0 && i < 4 && !s.Board.Lines[i].IsSlotEmpty(myId)) return true;
            }
            return false;
        }

        private int CountEnemyNeighbors(GameState s, int lineIdx, int myId)
        {
            int oppId = 3 - myId;
            int count = 0;
            foreach (int i in new[] { lineIdx - 1, lineIdx + 1 })
            {
                if (i >= 0 && i < 4 && !s.Board.Lines[i].IsSlotEmpty(oppId)) count++;
            }
            return count;
        }
    }
}