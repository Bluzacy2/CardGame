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
    /// <summary>
    /// Represents an evolvable AI strategy that uses genetic algorithm principles to evaluate game states.
    /// </summary>
    public class EvolvableStrategy : IAIStrategy
    {
        #region Properties

        /// <summary>
        /// Gets the genetic DNA array containing strategy weights and parameters.
        /// </summary>
        public float[] DNA { get; }

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the EvolvableStrategy class with genetic parameters.
        /// </summary>
        /// <param name="genes">The genetic parameters array (minimum 109 genes).</param>
        public EvolvableStrategy(float[] genes)
        {
            // DNA size safeguard (update to 109 genes)
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

        #endregion

        #region IAIStrategy Implementation

        /// <summary>
        /// Evaluates a game state using genetic parameters and strategic archetypes.
        /// </summary>
        /// <param name="state">The game state to evaluate.</param>
        /// <param name="botId">The ID of the bot player being evaluated.</param>
        /// <returns>A fitness score representing the desirability of the state.</returns>
        public float Evaluate(GameState state, int botId)
        {
            var bot = state.GetPlayer(botId);
            var enemy = state.GetOpponent(botId);

            // Immediate endgame states
            if (enemy.Health <= 0 && bot.Health > 0) return 10000000f;
            if (bot.Health <= 0) return -10000000f;

            if (state.CurrentPhase == GamePhase.Mulligan)
            {
                return EvaluateMulliganHand(state.GetPlayer(botId));
            }

            // Archetype and mood weights
            float aggroFactor = DNA[65] * (bot.Health > 15 ? 1.0f : DNA[90]);
            float controlFactor = DNA[66];
            float tempoFactor = DNA[67];
            float valueFactor = DNA[68];
            float comboFactor = DNA[108]; // NEW GENE

            float totalScore = 0f;

            // Main analysis
            totalScore += EvaluateMacroEconomy(state, bot, enemy, valueFactor, comboFactor);
            totalScore += EvaluateBoardGeometry(state, botId, tempoFactor);
            totalScore += EvaluateUnitsAndSynergies(state, botId, aggroFactor, controlFactor, comboFactor);
            totalScore += EvaluateForecasting(state, bot, enemy);

            // Specific scoring for Combo: Bonus for cost reductions in hand
            float totalDiscounts = bot.Hand.Sum(c => c.CostReduction);
            totalScore += totalDiscounts * 25.0f * comboFactor;

            return totalScore;
        }

        #endregion

        #region Private Evaluation Methods

        /// <summary>
        /// Evaluates the hand during the mulligan phase.
        /// </summary>
        private float EvaluateMulliganHand(PlayerState player)
        {
            float score = 0;

            foreach (var card in player.Hand)
            {
                bool isUnit = card.Definition.Type == CardType.Unit;
                float cost = card.CurrentStats.BloodCost;

                // Gene 71: Preference for low-cost units at start
                // DNA[71] is the ideal cost. The further the card is from this cost, the worse.
                if (isUnit)
                {
                    if (cost <= DNA[71]) score += 1000f; // We really want 1-2 drop units
                    else score -= (cost - DNA[71]) * 500f; // Penalty for "bricks" in hand
                }
                else // Spells
                {
                    // Gene 72: Whether the bot likes to keep spells
                    score += DNA[72] * 200f;
                    // Generally we want to discard spells unless they're very cheap
                    if (cost > 2) score -= 1000f;
                }

                // Gene 73: Whether the card is part of a combo (e.g., Black Cat for Sacrifice deck)
                if (new[] { "12", "3", "10" }.Contains(card.Definition.Id))
                {
                    score += DNA[73] * 300f;
                }
            }

            return score;
        }

        /// <summary>
        /// Evaluates macroeconomic factors like health, resources, and card value.
        /// </summary>
        private float EvaluateMacroEconomy(GameState state, PlayerState bot, PlayerState enemy, float valueFactor, float comboFactor)
        {
            float score = 0;

            // Hero life
            float selfHpValue = bot.Health * DNA[0];
            if (bot.Health < 10) selfHpValue *= DNA[2]; // Panic threshold
            score += selfHpValue;

            float enemyHpLoss = (30 - enemy.Health) * DNA[1];
            if (enemy.Health < 10) enemyHpLoss *= DNA[3]; // Lethal fever
            score += enemyHpLoss;

            // Card value in hand (enhanced by Combo)
            float handValueMultiplier = 1.0f + (comboFactor * 0.5f);
            score += bot.Hand.Count * DNA[6] * valueFactor * handValueMultiplier;

            if (bot.Hand.Count >= 7) score -= DNA[7]; // Overdraw risk

            // Resource economy (Gene 105 - ResourceGainValue)
            score += bot.CurrentBlood * DNA[105];

            if (state.ActivePlayerId == bot.PlayerId)
            {
                float unusedBloodPenalty = bot.CurrentBlood * DNA[4];
                if (state.CurrentPhase == GamePhase.UnitOnly)
                {
                    unusedBloodPenalty *= DNA[5];
                }
                score -= unusedBloodPenalty;
            }

            // Finishers in deck
            float finishersInDeck = bot.DrawPile.Count(c => c.CurrentStats.Attack >= 5);
            score += finishersInDeck * DNA[46] * DNA[48];

            return score;
        }

        /// <summary>
        /// Evaluates board positioning and geometry.
        /// </summary>
        private float EvaluateBoardGeometry(GameState state, int botId, float tempoFactor)
        {
            float score = 0;
            int occupiedLines = state.Board.Lines.Count(l => !l.IsSlotEmpty(botId));
            score += occupiedLines * DNA[77] * tempoFactor;

            foreach (var line in state.Board.Lines)
            {
                var myUnit = botId == 1 ? line.Player1Unit : line.Player2Unit;
                var enemyUnit = botId == 1 ? line.Player2Unit : line.Player1Unit;

                if (myUnit != null)
                {
                    if (enemyUnit == null) score += DNA[12]; // Empty lane bonus
                    if (line.Index == 0 || line.Index == 3) score += DNA[78]; // Corner value
                    if (HasNeighbor(state, line.Index, botId)) score += DNA[14]; // Adjacency

                    // Lane priorities (Genes 100-103)
                    score += myUnit.CurrentStats.Attack * DNA[100 + line.Index];
                }
            }

            return score;
        }

        /// <summary>
        /// Evaluates unit stats, keywords, and synergies.
        /// </summary>
        private float EvaluateUnitsAndSynergies(GameState state, int botId, float aggroFactor, float controlFactor, float comboFactor)
        {
            float score = 0;
            var units = state.Board.GetAllUnits();

            foreach (var unit in units)
            {
                bool isFriendly = unit.OwnerPlayerId == botId;
                var stats = unit.CurrentStats;
                float unitValue = 0;
                int lineIdx = GetLineIndex(state, unit.InstanceId);

                // Base statistics with archetype consideration
                if (isFriendly)
                {
                    unitValue += stats.Attack * DNA[8] * aggroFactor;
                    unitValue += stats.Health * DNA[9];
                    if (lineIdx != -1) unitValue += stats.Attack * DNA[100 + lineIdx];
                }
                else
                {
                    unitValue += stats.Attack * DNA[10] * controlFactor;
                    unitValue += stats.Health * DNA[11];
                    if (lineIdx != -1) unitValue += stats.Attack * DNA[100 + lineIdx];
                }

                // Keywords and statuses
                if (stats.Keywords.Contains(Keyword.Marked))
                {
                    float markBonus = isFriendly ? -DNA[16] : DNA[15];
                    unitValue += markBonus;
                }

                if (stats.Keywords.Contains(Keyword.Unkillable)) unitValue += DNA[18];
                if (stats.Keywords.Contains(Keyword.SoulGuard)) unitValue += DNA[19];

                // NEW: Flying (Gene 104)
                if (stats.Keywords.Contains(Keyword.Flying)) unitValue += DNA[104] * 2.0f;

                if (stats.Keywords.Contains(Keyword.SplashDamage) && lineIdx != -1)
                {
                    int neighborCount = CountEnemyNeighbors(state, lineIdx, botId);
                    unitValue += neighborCount * DNA[22];
                }

                // Subtypes and synergies (NEW: DNA[107] for Machines)
                if (unit.Definition.Subtypes.Contains("Monster")) unitValue += DNA[27];
                if (unit.Definition.Subtypes.Contains("Mercenary")) unitValue += DNA[28];
                if (unit.Definition.Subtypes.Contains("Animal")) unitValue += DNA[29];
                if (unit.Definition.Subtypes.Contains("Machine")) unitValue += DNA[30] + DNA[107];
                if (unit.Definition.Subtypes.Contains("Human")) unitValue += DNA[31];
                if (unit.Definition.Subtypes.Contains("Demon")) unitValue += DNA[32];

                // NEW: Token Synergy (Gene 106)
                if (unit.Definition.Id == "901" || unit.Definition.Id == "500") unitValue += DNA[106];

                // Permanent buffs (Gene 49)
                if (unit.PermanentBuffs.Attack > 0 || unit.PermanentBuffs.Health > 0)
                {
                    unitValue += (unit.PermanentBuffs.Attack + unit.PermanentBuffs.Health) * DNA[49];
                }

                // Combo-specific: Tea Maid (ID 46)
                if (isFriendly && unit.Definition.Id == "46") unitValue += 60.0f * comboFactor;

                score += isFriendly ? unitValue : -unitValue;
            }

            return score;
        }

        /// <summary>
        /// Evaluates future risks and opponent capabilities.
        /// </summary>
        private float EvaluateForecasting(GameState state, PlayerState bot, PlayerState enemy)
        {
            float score = 0;

            // Predicting enemy AoE
            if (state.Board.GetAllUnits().Count(u => u.OwnerPlayerId == bot.PlayerId) >= 3)
            {
                if (enemy.MaxBlood >= 4) score -= DNA[85]; // Fear of Hellfire
            }

            score -= enemy.Hand.Count * DNA[44]; // Enemy hand size fear

            // Bluff risk
            if (enemy.CurrentBlood > 0 && state.ActivePlayerId == bot.PlayerId)
            {
                score -= DNA[81];
            }

            return score;
        }

        #endregion

        #region Geometry Helper Methods

        /// <summary>
        /// Gets the line index where a unit is located.
        /// </summary>
        private int GetLineIndex(GameState state, int unitId)
        {
            for (int i = 0; i < 4; i++)
            {
                if (state.Board.Lines[i].Player1Unit?.InstanceId == unitId ||
                    state.Board.Lines[i].Player2Unit?.InstanceId == unitId)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Checks if a lane has a neighboring friendly unit.
        /// </summary>
        private bool HasNeighbor(GameState state, int lineIdx, int myId)
        {
            foreach (int i in new[] { lineIdx - 1, lineIdx + 1 })
            {
                if (i >= 0 && i < 4 && !state.Board.Lines[i].IsSlotEmpty(myId))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Counts enemy units adjacent to a lane.
        /// </summary>
        private int CountEnemyNeighbors(GameState state, int lineIdx, int myId)
        {
            int opponentId = 3 - myId;
            int count = 0;

            foreach (int i in new[] { lineIdx - 1, lineIdx + 1 })
            {
                if (i >= 0 && i < 4 && !state.Board.Lines[i].IsSlotEmpty(opponentId))
                {
                    count++;
                }
            }

            return count;
        }

        #endregion
    }
}