using CardGame.Core.Application;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Models;
using CardGame.Core.GameRules.Damage;
using CardGame.Core.State.Models;

namespace CardGame.Core.Cards.Logic.Keywords
{
    /// <summary>
    /// Defines the contract for keyword handlers that process keyword-specific effects and interactions.
    /// </summary>
    public interface IKeywordHandler
    {
        #region Properties
        /// <summary>
        /// Gets the keyword type this handler processes.
        /// </summary>
        Keyword Type { get; }
        #endregion

        #region Damage Modification
        /// <summary>
        /// Modifies incoming damage based on the keyword's properties.
        /// </summary>
        /// <param name="amount">The initial damage amount.</param>
        /// <param name="context">The damage context containing source, target, and damage type.</param>
        /// <returns>The modified damage amount after applying keyword effects.</returns>
        int OnModifyDamageTaken(int amount, DamageContext context);
        #endregion

        #region Combat Processing
        /// <summary>
        /// Processes effects that occur after a unit attacks.
        /// </summary>
        /// <param name="state">The current game state.</param>
        /// <param name="attacker">The unit that performed the attack.</param>
        /// <param name="victim">The target of the attack, or null if attacking a hero.</param>
        /// <param name="lineIndex">The line index where the attack occurred.</param>
        /// <param name="context">The game context with additional services.</param>
        /// <returns>The updated game state after processing attack effects.</returns>
        GameState OnAfterAttack(GameState state, CardInstance attacker, CardInstance? victim, int lineIndex, GameContext context);
        #endregion

        #region Turn Processing
        /// <summary>
        /// Processes effects that occur at the end of a round for a unit.
        /// </summary>
        /// <param name="state">The current game state.</param>
        /// <param name="unit">The unit to process round-end effects for.</param>
        /// <param name="context">The game context with additional services.</param>
        /// <returns>The updated game state after processing round-end effects.</returns>
        GameState OnRoundEnd(GameState state, CardInstance unit, GameContext context);
        #endregion

        #region Death Prevention
        /// <summary>
        /// Attempts to prevent a unit's death using keyword-specific prevention mechanics.
        /// </summary>
        /// <param name="state">The current game state (passed by reference for potential modification).</param>
        /// <param name="unit">The unit that is about to die.</param>
        /// <param name="context">The game context with additional services.</param>
        /// <param name="isSacrifice">Whether the death is due to a sacrifice effect.</param>
        /// <returns>True if death was prevented, otherwise false.</returns>
        bool OnPreventDeath(ref GameState state, CardInstance unit, GameContext context, bool isSacrifice);
        #endregion
    }
}