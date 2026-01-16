using CardGame.Core.Application;
using CardGame.Core.Events;
using CardGame.Core.State.Models;
using System;

namespace CardGame.Core.Commands.Interfaces
{
    /// <summary>
    /// Defines the contract for game commands that can be executed to modify the game state.
    /// </summary>
    public interface IGameCommand
    {
        #region Properties
        /// <summary>
        /// Gets the ID of the player who initiated the command.
        /// </summary>
        int PlayerId { get; }
        #endregion

        #region Command Execution
        /// <summary>
        /// Executes the command, applying its effects to the current game state.
        /// </summary>
        /// <param name="currentState">The current game state before command execution.</param>
        /// <param name="eventBus">The event bus for publishing game events.</param>
        /// <param name="context">The game context with additional services.</param>
        /// <returns>The updated game state after command execution.</returns>
        GameState Execute(GameState currentState, EventBus eventBus, GameContext context);
        #endregion
    }
}