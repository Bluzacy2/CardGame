using CardGame.Core.State.Models;
using System;

namespace CardGame.Core.AI.Interfaces
{
    /// <summary>
    /// Defines the contract for AI strategies that evaluate game states and make decisions.
    /// </summary>
    public interface IAIStrategy
    {
        /// <summary>
        /// Evaluates the desirability of a game state from the perspective of the specified bot player.
        /// </summary>
        /// <param name="state">The game state to evaluate.</param>
        /// <param name="botPlayerId">The ID of the bot player being evaluated.</param>
        /// <returns>A float score representing the desirability of the state (higher is better).</returns>
        float Evaluate(GameState state, int botPlayerId);
    }
}