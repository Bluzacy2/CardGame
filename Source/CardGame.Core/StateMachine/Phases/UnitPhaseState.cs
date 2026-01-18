using CardGame.Core.Application;
using CardGame.Core.Commands.Implementations;
using CardGame.Core.Commands.Interfaces;
using CardGame.Core.Events;
using CardGame.Core.State.Enums;
using CardGame.Core.State.Models;
using CardGame.Core.StateMachine.Interfaces;

namespace CardGame.Core.StateMachine.Phases
{
    /// <summary>
    /// Handles the Unit Phase where players can only play unit cards and not action cards.
    /// </summary>
    public class UnitPhaseState : IPhaseState
    {
        #region Properties
        /// <summary>
        /// Gets the type of game phase this state represents (UnitOnly).
        /// </summary>
        public GamePhase PhaseType => GamePhase.UnitOnly;
        #endregion

        #region Command Validation
        /// <summary>
        /// Determines whether a specific command is allowed in the current unit phase.
        /// </summary>
        /// <param name="command">The command to validate.</param>
        /// <param name="state">The current game state.</param>
        /// <returns>True if the command is allowed in this phase, otherwise false.</returns>
        public bool IsCommandAllowed(IGameCommand command, GameState state)
        {
            // Always allow target selection commands (for resolving pending interactions)
            if (command is SelectTargetCommand) return true;

            // Only allow unit playing or phase ending commands for the active player
            return command.PlayerId == state.ActivePlayerId &&
                   (command is PlayUnitCommand || command is EndPhaseCommand);
        }
        #endregion

        #region Phase Transition Logic
        /// <summary>
        /// Determines whether the unit phase should end automatically without player input.
        /// </summary>
        /// <param name="state">The current game state.</param>
        /// <returns>Always false for unit phase - requires explicit EndPhaseCommand.</returns>
        public bool ShouldEndPhaseAutomatically(GameState state) => false;

        /// <summary>
        /// Processes the end of the unit phase and transitions to the combined phase (UnitAndAction) for the opponent.
        /// </summary>
        /// <param name="currentState">The current game state at phase end.</param>
        /// <param name="eventBus">The event bus for publishing phase transition events.</param>
        /// <param name="context">The game context with additional services.</param>
        /// <returns>The updated game state with phase set to UnitAndAction and active player switched to opponent.</returns>
        public GameState ProcessEndPhase(GameState currentState, EventBus eventBus, GameContext context)
        {
            int opponentId = 3 - currentState.ActivePlayerId;
            return currentState.With(
                currentPhase: GamePhase.UnitAndAction,
                activePlayerId: opponentId
            );
        }
        #endregion
    }
}