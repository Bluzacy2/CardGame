using CardGame.Core.Application;
using CardGame.Core.Events.Interfaces;
using CardGame.Core.State.Models;

namespace CardGame.Core.Cards.Component
{
    /// <summary>
    /// Defines the contract for card components that can respond to game events.
    /// </summary>
    public interface ICardComponent
    {
        /// <summary>
        /// Determines whether this component should trigger in response to a game event.
        /// </summary>
        /// <param name="gameEvent">The game event that occurred.</param>
        /// <param name="state">The current game state.</param>
        /// <returns>True if the component should trigger, otherwise false.</returns>
        bool ShouldTrigger(IGameEvent gameEvent, GameState state);

        /// <summary>
        /// Resolves the component's effect, potentially modifying the game state.
        /// </summary>
        /// <param name="gameEvent">The triggering game event.</param>
        /// <param name="currentState">The current game state before resolution.</param>
        /// <param name="context">The game context providing access to services and factories.</param>
        /// <returns>The updated game state after resolution.</returns>
        GameState Resolve(IGameEvent gameEvent, GameState currentState, GameContext context);
    }
}