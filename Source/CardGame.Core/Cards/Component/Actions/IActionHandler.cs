using CardGame.Core.Application;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Logic;
using CardGame.Core.Events.Interfaces;
using CardGame.Core.State.Models;

namespace CardGame.Core.Cards.Component.Actions
{
    /// <summary>
    /// Defines the contract for handlers that execute specific card actions.
    /// </summary>
    public interface IActionHandler
    {
        /// <summary>
        /// Gets the type of action this handler processes.
        /// </summary>
        ActionType Type { get; }

        /// <summary>
        /// Executes the action with the provided parameters.
        /// </summary>
        /// <param name="state">The current game state.</param>
        /// <param name="context">The game context providing access to services.</param>
        /// <param name="actionData">The data defining the action to execute.</param>
        /// <param name="targets">The resolved targets for the action.</param>
        /// <param name="sourceId">The ID of the card that initiated the action.</param>
        /// <param name="gameEvent">The event that triggered this action.</param>
        /// <returns>The updated game state after executing the action.</returns>
        GameState Execute(
            GameState state,
            GameContext context,
            ActionData actionData,
            EffectTargets targets,
            int sourceId,
            IGameEvent gameEvent
        );
    }
}