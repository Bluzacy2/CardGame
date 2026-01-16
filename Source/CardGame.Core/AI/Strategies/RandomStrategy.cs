using CardGame.Core.AI.Interfaces;
using CardGame.Core.State.Models;
using System;

namespace CardGame.Core.AI.Strategies
{
    /// <summary>
    /// Provides random evaluation for AI decisions, creating unpredictable and chaotic behavior.
    /// </summary>
    public class RandomStrategy : IAIStrategy
    {
        private readonly Random _random = new Random();

        #region IAIStrategy Implementation

        /// <summary>
        /// Evaluates a game state by returning a random value, making the AI unpredictable.
        /// </summary>
        /// <param name="state">The game state to evaluate.</param>
        /// <param name="botPlayerId">The ID of the bot player.</param>
        /// <returns>A random float value between 0 and 100.</returns>
        public float Evaluate(GameState state, int botPlayerId) =>
            (float)_random.NextDouble() * 100f;

        #endregion
    }
}