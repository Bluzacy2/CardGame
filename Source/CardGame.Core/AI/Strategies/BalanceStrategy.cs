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
    /// Provides a balanced AI strategy that evaluates game states with emphasis on resource management and tactical positioning.
    /// </summary>
    public class BalanceStrategy : IAIStrategy
    {
        #region Constants

        /// <summary>
        /// Global weight for health value in evaluation.
        /// </summary>
        private const float HealthWeight = 15.0f;

        /// <summary>
        /// Global weight for board presence value in evaluation.
        /// </summary>
        private const float BoardWeight = 25.0f;

        /// <summary>
        /// Global weight for hand card value in evaluation.
        /// </summary>
        private const float HandValueWeight = 12.0f;

        #endregion

        #region IAIStrategy Implementation

        /// <summary>
        /// Evaluates the game state with balanced consideration of multiple strategic factors.
        /// </summary>
        /// <param name="state">The current game state to evaluate.</param>
        /// <param name="botPlayerId">The ID of the bot player.</param>
        /// <returns>A score representing the desirability of the state for the bot player.</returns>
        public float Evaluate(GameState state, int botPlayerId)
        {
            var bot = state.GetPlayer(botPlayerId);
            var enemy = state.GetOpponent(botPlayerId);

            // --- CRITICAL FIX: GAME END DETECTION ---
            if (bot.Health <= 0) return -1000000f; // Loss is absolute bottom
            if (enemy.Health <= 0) return 1000000f; // Win is absolute priority

            float score = 2000.0f; // Base point value

            // --- 1. STRATEGY AND MOOD ANALYSIS ---
            var hand = bot.Hand;
            int unitCount = hand.Count(c => c.Definition.Type == CardType.Unit);
            int spellCount = hand.Count(c => c.Definition.Type == CardType.Spell);

            bool isMarkStrategy = hand.Any(IsMarkCard) ||
                state.Board.GetAllUnits().Any(u => u.OwnerPlayerId == botPlayerId && IsMarkCard(u));
            bool isSacrificeStrategy = hand.Any(IsSacrificeCard) ||
                state.Board.GetAllUnits().Any(u => u.OwnerPlayerId == botPlayerId && IsSacrificeCard(u));

            // AggroMood: If we have many units, play offensively
            bool isAggroMood = unitCount > spellCount;

            // --- 2. RESOURCES AND TEMPO ---
            float spentBlood = bot.MaxBlood - bot.CurrentBlood;
            if (state.CurrentPhase == GamePhase.UnitOnly)
            {
                score += spentBlood * (isAggroMood ? 20.0f : 5.0f);
            }
            else
            {
                score += spentBlood * 25.0f;
            }

            // Penalty for PASS when we can afford to play something (increased penalty from HEAD)
            if (state.ActivePlayerId == botPlayerId && bot.CurrentBlood > 0)
            {
                if (hand.Any(c => c.CurrentStats.BloodCost <= bot.CurrentBlood))
                {
                    score -= 300.0f;
                }
            }

            // --- 3. HERO HEALTH (Non-linear) ---
            float hpDiff = bot.Health - enemy.Health;
            score += hpDiff * HealthWeight;

            // Panic Mode: Below 12 HP, bot treats every health point as treasure
            if (bot.Health < 12)
            {
                score -= (15 - bot.Health) * 20.0f;
            }

            // Lethal focus: If enemy has low HP, bot becomes aggressive
            if (enemy.Health < 5)
            {
                score += 200.0f;
            }

            // --- 4. BOARD ANALYSIS (Lines, Splash, Trade Analysis) ---
            bool hasGerard = state.Board.GetAllUnits().Any(u =>
                u.OwnerPlayerId == botPlayerId && u.Definition.Name.Contains("Gerard"));
            var enemyUnits = state.Board.GetAllUnits().Where(u => u.OwnerPlayerId != botPlayerId).ToList();

            for (int i = 0; i < 4; i++)
            {
                var line = state.Board.Lines[i];
                var myUnit = (botPlayerId == 1) ? line.Player1Unit : line.Player2Unit;
                var enemyUnit = (botPlayerId == 1) ? line.Player2Unit : line.Player1Unit;

                // Sum board presence (enemies are slightly more "expensive" so bot wants to remove them)
                score += EvaluateUnitPresence(myUnit, enemyUnit, i, state, hasGerard, true, isMarkStrategy, isSacrificeStrategy) * BoardWeight;
                score -= EvaluateUnitPresence(enemyUnit, myUnit, i, state, false, false, false, false) * (BoardWeight * 1.15f);
            }

            // --- 5. HAND ANALYSIS (Option potential) ---
            foreach (var card in hand)
            {
                score += EvaluateCardInHand(card, enemyUnits, isMarkStrategy, isSacrificeStrategy) * HandValueWeight;
            }

            // Penalty for overfilled hand (Overdraw risk)
            if (hand.Count >= 8)
            {
                score -= 100.0f;
            }

            return score;
        }

        #endregion

        #region Private Helper Methods

        /// <summary>
        /// Evaluates the value of a unit's presence on the board.
        /// </summary>
        private float EvaluateUnitPresence(CardInstance? unit, CardInstance? opponentUnit, int lineIdx, GameState state, bool playerHasGerard, bool isFriendly, bool isMarkStrategy, bool isSacrificeStrategy)
        {
            if (unit == null)
            {
                return 0;
            }

            var stats = unit.CurrentStats;

            // Base: Attack (Pressure) + HP (Survivability)
            // If it's an enemy, its attack is more threatening than its HP (priority for elimination)
            float value = isFriendly
                ? (stats.Attack * 2.5f + stats.Health * 1.5f)
                : (stats.Attack * 5.0f + stats.Health * 1.5f);

            // Bonus for Splash Damage
            if (stats.Keywords.Contains(Keyword.SplashDamage))
            {
                int splashPower = unit.Definition.BaseStats.KeywordParams.GetValueOrDefault(Keyword.SplashDamage, 1);
                int hits = CountSplashTargets(unit, lineIdx, state);
                value += (hits * splashPower * 2.5f);
            }

            // --- TRADE ANALYSIS ---
            if (opponentUnit != null)
            {
                bool iDie = stats.Health <= opponentUnit.CurrentStats.Attack;
                bool enemyDies = opponentUnit.CurrentStats.Health <= stats.Attack;

                if (iDie && !enemyDies) value -= 20.0f; // Fatal trade
                if (!iDie && enemyDies) value += 15.0f; // Excellent trade
            }

            // --- STATUSES AND SYNERGIES ---
            if (stats.Keywords.Contains(Keyword.Marked))
            {
                if (isFriendly)
                {
                    value -= 40.0f;
                }
                else
                {
                    value += playerHasGerard ? 60.0f : 20.0f;
                }
            }

            if (stats.Keywords.Contains(Keyword.Stunned))
            {
                value *= 0.25f;
            }

            if (stats.Keywords.Contains(Keyword.Unkillable))
            {
                value += 25.0f;
            }

            if (isSacrificeStrategy && (unit.Definition.Id == "12" || unit.Definition.Id == "3"))
            {
                value += 15.0f;
            }

            return value;
        }

        /// <summary>
        /// Evaluates the value of a card in hand.
        /// </summary>
        private float EvaluateCardInHand(CardInstance card, List<CardInstance> enemyUnits, bool isMarkStrategy, bool isSacrificeStrategy)
        {
            float value = 1.0f;

            if (isMarkStrategy && IsMarkCard(card))
            {
                value += 2.0f;
            }

            if (isSacrificeStrategy && IsSacrificeCard(card))
            {
                value += 2.0f;
            }

            if (card.CurrentStats.Keywords.Contains(Keyword.Unkillable))
            {
                value += 2.5f;
            }

            if (card.Definition.Type == CardType.Spell &&
                card.Definition.Effects.Any(e => e.Actions.Any(a => a.Type == ActionType.DealDamage)))
            {
                if (enemyUnits.Any(u => u.CurrentStats.Health <= 3))
                {
                    value += 3.0f;
                }
            }

            return value;
        }

        /// <summary>
        /// Counts potential splash damage targets adjacent to a unit.
        /// </summary>
        private int CountSplashTargets(CardInstance unit, int lineIdx, GameState state)
        {
            int hits = 0;
            int opponentId = 3 - unit.OwnerPlayerId;

            foreach (int neighborIdx in new[] { lineIdx - 1, lineIdx + 1 })
            {
                if (neighborIdx >= 0 && neighborIdx < 4)
                {
                    var neighborLine = state.Board.Lines[neighborIdx];
                    var potentialVictim = (opponentId == 1) ? neighborLine.Player1Unit : neighborLine.Player2Unit;
                    if (potentialVictim != null)
                    {
                        hits++;
                    }
                }
            }

            return hits;
        }

        /// <summary>
        /// Determines if a card is part of the "Mark" strategy.
        /// </summary>
        private bool IsMarkCard(CardInstance card) =>
            new[] { "4", "32", "24", "25", "26" }.Contains(card.Definition.Id);

        /// <summary>
        /// Determines if a card is part of the "Sacrifice" strategy.
        /// </summary>
        private bool IsSacrificeCard(CardInstance card) =>
            new[] { "12", "10", "11", "3", "9" }.Contains(card.Definition.Id);

        #endregion
    }
}