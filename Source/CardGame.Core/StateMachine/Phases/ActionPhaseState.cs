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
    /// Handles the Action Phase where players can only play action cards (spells) and not units.
    /// </summary>
    public class ActionPhaseState : IPhaseState
    {
        #region Properties
        /// <summary>
        /// Gets the type of game phase this state represents (ActionOnly).
        /// </summary>
        public GamePhase PhaseType => GamePhase.ActionOnly;
        #endregion

        #region Command Validation
        /// <summary>
        /// Determines whether a specific command is allowed in the current action phase.
        /// </summary>
        /// <param name="command">The command to validate.</param>
        /// <param name="state">The current game state.</param>
        /// <returns>True if the command is allowed in this phase, otherwise false.</returns>
        public bool IsCommandAllowed(IGameCommand command, GameState state)
        {
            // Always allow target selection commands (for resolving pending interactions)
            if (command is SelectTargetCommand stc)
            {
             
                return state.PendingInteraction != null &&
                       stc.PlayerId == state.PendingInteraction.PlayerIdWhoChooses;
            }

            // Only allow spell playing or phase ending commands for the active player
            return command.PlayerId == state.ActivePlayerId &&
                   (command is PlaySpellCommand || command is EndPhaseCommand);
        }
        #endregion

        #region Phase Transition Logic
        /// <summary>
        /// Determines whether the action phase should end automatically without player input.
        /// </summary>
        /// <param name="state">The current game state.</param>
        /// <returns>Always false for action phase - requires explicit EndPhaseCommand.</returns>
        public bool ShouldEndPhaseAutomatically(GameState state) => false;

        /// <summary>
        /// Processes the end of the action phase and transitions to the combat phase.
        /// </summary>
        /// <param name="currentState">The current game state at phase end.</param>
        /// <param name="eventBus">The event bus for publishing phase transition events.</param>
        /// <param name="context">The game context with additional services.</param>
        /// <returns>The updated game state with phase set to Combat.</returns>
        public GameState ProcessEndPhase(GameState currentState, EventBus eventBus, GameContext context)
        {
            return currentState.With(currentPhase: GamePhase.Combat);
        }
        #endregion
    }
}