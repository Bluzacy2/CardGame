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
    /// Handles the Combined Phase where players can play both units and action cards (spells).
    /// </summary>
    public class BothPhaseState : IPhaseState
    {
        #region Properties
        /// <summary>
        /// Gets the type of game phase this state represents (UnitAndAction).
        /// </summary>
        public GamePhase PhaseType => GamePhase.UnitAndAction;
        #endregion

        #region Command Validation
        /// <summary>
        /// Determines whether a specific command is allowed in the current combined phase.
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

            // Allow unit playing, spell playing, or phase ending commands for the active player
            return command.PlayerId == state.ActivePlayerId &&
                   (command is PlayUnitCommand ||
                    command is PlaySpellCommand ||
                    command is EndPhaseCommand);
        }
        #endregion

        #region Phase Transition Logic
        /// <summary>
        /// Determines whether the combined phase should end automatically without player input.
        /// </summary>
        /// <param name="state">The current game state.</param>
        /// <returns>Always false for combined phase - requires explicit EndPhaseCommand.</returns>
        public bool ShouldEndPhaseAutomatically(GameState state) => false;

        /// <summary>
        /// Processes the end of the combined phase and transitions to the action-only phase.
        /// </summary>
        /// <param name="currentState">The current game state at phase end.</param>
        /// <param name="eventBus">The event bus for publishing phase transition events.</param>
        /// <param name="context">The game context with additional services.</param>
        /// <returns>The updated game state with phase set to ActionOnly and active player reset to the round starter.</returns>
        public GameState ProcessEndPhase(GameState currentState, EventBus eventBus, GameContext context)
        {
            return currentState.With(
                currentPhase: GamePhase.ActionOnly,
                activePlayerId: currentState.RoundStartingPlayerId
            );
        }
        #endregion
    }
}