using CardGame.Core.Application;
using CardGame.Core.Commands.Interfaces;
using CardGame.Core.Events;
using CardGame.Core.State.Enums;
using CardGame.Core.State.Models;

namespace CardGame.Core.StateMachine.Interfaces
{
    /// <summary>
    /// Defines the contract for game phase states, handling command validation and phase transitions.
    /// </summary>
    public interface IPhaseState
    {
        #region Properties
        /// <summary>
        /// Gets the type of game phase this state represents.
        /// </summary>
        GamePhase PhaseType { get; }
        #endregion

        #region Phase Management
        /// <summary>
        /// Determines whether a specific command is allowed in the current phase.
        /// </summary>
        /// <param name="command">The command to validate.</param>
        /// <param name="state">The current game state.</param>
        /// <returns>True if the command is allowed in this phase, otherwise false.</returns>
        bool IsCommandAllowed(IGameCommand command, GameState state);

        /// <summary>
        /// Determines whether the current phase should end automatically without player input.
        /// </summary>
        /// <param name="state">The current game state.</param>
        /// <returns>True if the phase should end automatically, otherwise false.</returns>
        bool ShouldEndPhaseAutomatically(GameState state);
        #endregion

        #region Phase Transition
        /// <summary>
        /// Processes the end of the current phase and transitions to the next phase.
        /// </summary>
        /// <param name="currentState">The current game state at phase end.</param>
        /// <param name="eventBus">The event bus for publishing phase transition events.</param>
        /// <param name="context">The game context with additional services.</param>
        /// <returns>The updated game state after processing the phase end.</returns>
        GameState ProcessEndPhase(GameState currentState, EventBus eventBus, GameContext context);
        #endregion
    }
}