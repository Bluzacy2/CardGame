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
    /// <summary>
    /// Standard AI strategy implementation with configurable weight-based evaluation.
    /// Prioritizes board control, resource efficiency, and threat assessment.
    /// </summary>
    public class StandardStrategy : IAIStrategy
    {
        #region Weight Configuration
        private const float HealthWeight = 25.0f;
        private const float BoardWeight = 45.0f;
        private const float HandValueWeight = 20.0f;
        private const float EfficiencyWeight = 10.0f;
        private const float ThreatPenaltyWeight = 30.0f;
        #endregion

        #region Card Identification Constants
        private static readonly HashSet<string> HighValueTargetIds = new() { "24", "5", "36", "14", "6", "23" };
        private static readonly HashSet<string> FodderIds = new() { "12", "3", "1", "900" };
        private static readonly HashSet<string> SacrificeCardIds = new() { "10", "12", "3", "9", "900", "1" };
        private static readonly HashSet<string> MarkCardIds = new() { "4", "32", "24", "25", "26" };
        #endregion

        #region Public Methods
        /// <summary>
        /// Evaluates the game state from the perspective of the specified player.
        /// </summary>
        /// <param name="state">The current game state to evaluate.</param>
        /// <param name="botPlayerId">The ID of the player to evaluate for.</param>
        /// <returns>A score representing the desirability of the game state for the specified player.</returns>
        public float Evaluate(GameState state, int botPlayerId)
        {
            var bot = state.GetPlayer(botPlayerId);
            var enemy = state.GetOpponent(botPlayerId);

            // 1. Absolute priorities
            if (enemy.Health <= 0 && bot.Health > 0) return 3000000f; // Lethal
            if (bot.Health <= 0) return -3000000f; // Defeat
            if (bot.Health <= 0 && enemy.Health <= 0) return 0f; // Draw

            float score = 10000.0f;

            // 2. Board identification and situation assessment
            var myUnits = state.Board.GetAllUnits().Where(unit => unit.OwnerPlayerId == botPlayerId).ToList();
            var enemyUnits = state.Board.GetAllUnits().Where(unit => unit.OwnerPlayerId != botPlayerId).ToList();

            bool hasGerard = myUnits.Any(unit => unit.Definition.Name.Contains("Gerard"));
            bool isSacrificeStrategy = bot.Hand.Any(IsSacrificeCard) || myUnits.Any(IsSacrificeCard);
            bool isMarkStrategy = bot.Hand.Any(IsMarkCard) || myUnits.Any(IsMarkCard);

            // 3. Resource efficiency
            // Penalize for unused blood, unless we have a combo plan
            score -= (bot.CurrentBlood * EfficiencyWeight);
            score += EvaluateRealizablePotential(state, botPlayerId, isSacrificeStrategy, isMarkStrategy);

            // 4. Hero life
            score += (bot.Health - enemy.Health) * HealthWeight;
            if (bot.Health < 12) score -= (15 - bot.Health) * 45.0f; // Panic Mode

            // 5. Board control and kill priority
            foreach (var unit in myUnits)
            {
                score += EvaluateFriendlyUnit(unit, state, hasGerard, isSacrificeStrategy, true) * BoardWeight;

                // Check if we've exposed a key unit to certain death
                if (IsHighValueTarget(unit)) score += EvaluateSafety(unit, state, botPlayerId);
            }

            foreach (var unit in enemyUnits)
            {
                float threat = CalculateEnemyThreat(unit);

                if (unit.CurrentStats.Health > 0)
                {
                    score -= threat * (BoardWeight * 1.3f);
                }
            }

            // 6. Hand evaluation (no penalty for few cards)
            // Bot should play with what it has. We only assess card quality in the context of the board.
            foreach (var card in bot.Hand)
            {
                score += EvaluateCardInHand(card, enemyUnits, myUnits, bot.CurrentBlood) * HandValueWeight;
            }

            // 7. Anti-loafing (blocking passivity)
            // If bot has mana and cards, but does nothing -> penalty.
            if (state.ActivePlayerId == botPlayerId && bot.CurrentBlood > 0)
            {
                bool hasPlayableCard = bot.Hand.Any(card => card.CurrentStats.BloodCost <= bot.CurrentBlood);
                if (hasPlayableCard)
                {
                    score -= 400.0f;
                }
            }

            return Math.Clamp(score, -4000000f, 4000000f);
        }
        #endregion

        #region Unit Evaluation
        private float EvaluateFriendlyUnit(CardInstance unit, GameState state, bool hasGerard, bool isSacrificeStrategy, bool isFriendly)
        {
            var stats = unit.CurrentStats;
            float value = (stats.Attack * 3.0f) + (stats.Health * 2.0f);

            // Friendly fire fix
            if (stats.Health <= 0)
            {
                if (IsFodder(unit)) value += 40.0f; // Sacrifice is good
                else value -= 250.0f; // Loss of an important unit is a big mistake
            }
            else if (unit.DamageTaken > 0)
            {
                value -= (unit.DamageTaken * 12.0f);
            }

            // Status effects
            if (stats.Keywords.Contains(Keyword.Marked)) value -= 40.0f;
            if (stats.Keywords.Contains(Keyword.Unkillable)) value += 40.0f;
            if (stats.Keywords.Contains(Keyword.SoulGuard)) value += 20.0f;

            // Splash damage positioning
            if (stats.Keywords.Contains(Keyword.SplashDamage))
            {
                int lineIndex = GetLineIndex(state, unit.InstanceId);
                if (lineIndex == 1 || lineIndex == 2) value += 15.0f;
            }

            // Bonus for Sacrifice deck
            if (isSacrificeStrategy && IsFodder(unit)) value += 15.0f;

            return value;
        }

        private float CalculateEnemyThreat(CardInstance unit)
        {
            var stats = unit.CurrentStats;
            float threat = (stats.Attack * 3.5f) + (stats.Health * 2.0f);

            // 1. Engine threat (highest priority - kill it!)
            if (IsHighValueTarget(unit)) threat += 200.0f;

            // 2. Mark utility
            if (stats.Keywords.Contains(Keyword.Marked))
            {
                if (stats.Health <= 1) threat -= 10.0f; // Mark on 1 HP unit is useless (unless for trigger)
                else threat += (stats.Health * 10.0f);   // Mark on unit with >3 HP is very valuable
            }

            if (stats.Keywords.Contains(Keyword.SplashDamage)) threat += 50.0f;

            return threat;
        }

        private float EvaluateSafety(CardInstance unit, GameState state, int botId)
        {
            if (unit.CurrentStats.Health <= 3)
            {
                var opponent = state.GetOpponent(botId);
                if (opponent.Hand.Count > 0 && opponent.MaxBlood >= 2)
                {
                    return -ThreatPenaltyWeight;
                }
            }
            return 0.0f;
        }
        #endregion

        #region Card Evaluation
        private float EvaluateCardInHand(CardInstance card, List<CardInstance> enemyUnits, List<CardInstance> myUnits, int currentBlood)
        {
            float value = 1.0f;

            // Playable card now is more valuable (Tempo)
            if (card.CurrentStats.BloodCost <= currentBlood) value += 3.0f;

            // Contextual spells
            if (card.Definition.Type == CardType.Spell)
            {
                // Glock: More valuable when there's a target that can be killed
                if (card.Definition.Id == "7")
                {
                    bool hasHighValueTarget = enemyUnits.Any(unit => IsHighValueTarget(unit) || (unit.CurrentStats.Health <= 3 && unit.CurrentStats.Attack >= 4));
                    value += hasHighValueTarget ? 8.0f : 0.0f;
                }

                // Buffs: More valuable when we have units on board
                if (card.Definition.Effects.Any(effect => effect.Actions.Any(action => action.Type == ActionType.BuffStats)))
                {
                    value += (myUnits.Count > 0) ? 5.0f : -5.0f;
                }
            }

            // Units are more valuable with an empty board
            if (card.Definition.Type == CardType.Unit && myUnits.Count == 0) value += 10.0f;

            return value;
        }

        private float EvaluateRealizablePotential(GameState state, int botId, bool isSacrificeStrategy, bool isMarkStrategy)
        {
            var bot = state.GetPlayer(botId);
            float potential = 0;
            int nextTurnMana = bot.MaxBlood + 1;

            // Combo: Sacrifice
            bool hasBear = bot.Hand.Any(card => card.Definition.Id == "10");
            bool hasFuel = bot.Hand.Any(card => card.Definition.Id == "12");
            if (isSacrificeStrategy && hasBear && hasFuel && nextTurnMana >= 3)
            {
                potential += 80.0f;
            }

            // Combo: Mark Execution (Gerard + Marks)
            if (isMarkStrategy && bot.Hand.Any(card => card.Definition.Id == "24"))
            {
                int markedEnemies = state.Board.GetAllUnits().Count(unit => unit.OwnerPlayerId != botId && unit.CurrentStats.Keywords.Contains(Keyword.Marked));
                potential += (markedEnemies * 40.0f);
            }

            return potential;
        }
        #endregion

        #region Helper Methods
        private int GetLineIndex(GameState state, int unitId)
        {
            for (int i = 0; i < 4; i++)
            {
                if (!state.Board.Lines[i].IsSlotEmpty(1) && state.Board.Lines[i].Player1Unit.InstanceId == unitId)
                    return i;
                else if (!state.Board.Lines[i].IsSlotEmpty(2) && state.Board.Lines[i].Player2Unit.InstanceId == unitId)
                    return i;
            }
            return -1;
        }

        private bool IsHighValueTarget(CardInstance unit) => HighValueTargetIds.Contains(unit.Definition.Id);
        private bool IsFodder(CardInstance card) => FodderIds.Contains(card.Definition.Id);
        private bool IsSacrificeCard(CardInstance card) => SacrificeCardIds.Contains(card.Definition.Id);
        private bool IsMarkCard(CardInstance card) => MarkCardIds.Contains(card.Definition.Id);
        #endregion
    }
}