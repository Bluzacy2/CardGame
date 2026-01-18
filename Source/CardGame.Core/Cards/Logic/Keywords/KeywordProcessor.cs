using System.Collections.Generic;
using System.Linq;
using CardGame.Core.Application;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Models;
using CardGame.Core.GameRules.Damage;
using CardGame.Core.State.Models;

namespace CardGame.Core.Cards.Logic.Keywords
{
    /// <summary>
    /// Processes keyword effects and delegates to registered keyword handlers for various game events.
    /// </summary>
    public class KeywordProcessor
    {
        #region Private Fields
        private readonly Dictionary<Keyword, IKeywordHandler> _handlers = new();
        #endregion

        #region Handler Registration
        /// <summary>
        /// Registers a keyword handler for a specific keyword type.
        /// </summary>
        /// <param name="handler">The handler to register.</param>
        public void RegisterHandler(IKeywordHandler handler) => _handlers[handler.Type] = handler;
        #endregion

        #region Damage Processing
        /// <summary>
        /// Processes damage taken by applying keyword-based damage modifications.
        /// </summary>
        /// <param name="amount">The initial damage amount.</param>
        /// <param name="context">The damage context containing source, target, and damage type.</param>
        /// <returns>The final damage amount after keyword modifications.</returns>
        public int ProcessDamageTaken(int amount, DamageContext context)
        {
            int finalAmount = amount;
            foreach (var keyword in context.Target.CurrentStats.Keywords)
            {
                if (_handlers.TryGetValue(keyword, out var handler))
                    finalAmount = handler.OnModifyDamageTaken(finalAmount, context);
            }
            return finalAmount;
        }
        #endregion

        #region Combat Processing
        /// <summary>
        /// Processes post-attack keyword effects after a unit attacks.
        /// </summary>
        /// <param name="state">The current game state.</param>
        /// <param name="attacker">The unit that performed the attack.</param>
        /// <param name="victim">The target of the attack, or null if attacking a hero.</param>
        /// <param name="lineIndex">The line index where the attack occurred.</param>
        /// <param name="context">The game context with additional services.</param>
        /// <returns>The updated game state after processing post-attack keyword effects.</returns>
        public GameState ProcessPostAttack(GameState state, CardInstance attacker, CardInstance? victim, int lineIndex, GameContext context)
        {
            var workingState = state;
            foreach (var keyword in attacker.CurrentStats.Keywords)
            {
                if (_handlers.TryGetValue(keyword, out var handler))
                    workingState = handler.OnAfterAttack(workingState, attacker, victim, lineIndex, context);
            }
            return workingState;
        }
        #endregion

        #region Turn Processing
        /// <summary>
        /// Processes round-end keyword effects for a specific unit.
        /// </summary>
        /// <param name="state">The current game state.</param>
        /// <param name="unit">The unit to process round-end effects for.</param>
        /// <param name="context">The game context with additional services.</param>
        /// <returns>The updated game state after processing round-end keyword effects.</returns>
        public GameState ProcessRoundEnd(GameState state, CardInstance unit, GameContext context)
        {
            var workingState = state;
            foreach (var keyword in unit.CurrentStats.Keywords)
            {
                if (_handlers.TryGetValue(keyword, out var handler))
                    workingState = handler.OnRoundEnd(workingState, unit, context);
            }
            return workingState;
        }
        #endregion

        #region Death Prevention
        /// <summary>
        /// Attempts to prevent a unit's death using keyword-based death prevention effects.
        /// Combines keywords from the current stats and the base card definition.
        /// </summary>
        /// <param name="state">The current game state (passed by reference for potential modification).</param>
        /// <param name="unit">The unit that is about to die.</param>
        /// <param name="context">The game context with additional services.</param>
        /// <param name="isSacrifice">Whether the death is due to a sacrifice effect.</param>
        /// <returns>True if death was prevented, otherwise false.</returns>
        public bool TryPreventDeath(ref GameState state, CardInstance unit, GameContext context, bool isSacrifice)
        {
            // Combine keywords from current stats AND from the base card definition
            // This ensures Unkillable works even when dying from a spell
            var allKeywords = unit.CurrentStats.Keywords
                .Concat(unit.Definition.Keywords)
                .Distinct();

            foreach (var keyword in allKeywords)
            {
                if (_handlers.TryGetValue(keyword, out var handler))
                {
                    // Unkillable should not work if the card is silenced
                    if (unit.IsSilenced) continue;

                    if (handler.OnPreventDeath(ref state, unit, context, isSacrifice))
                        return true;
                }
            }
            return false;
        }
        #endregion
    }
}