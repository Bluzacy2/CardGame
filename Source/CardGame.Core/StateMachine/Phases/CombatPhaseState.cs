using CardGame.Core.Application;
using CardGame.Core.Commands.Implementations;
using CardGame.Core.Commands.Interfaces;
using CardGame.Core.Combat;
using CardGame.Core.Events;
using CardGame.Core.GameRules.Death;
using CardGame.Core.State.Enums;
using CardGame.Core.State.Models;
using CardGame.Core.StateMachine.Interfaces;

namespace CardGame.Core.StateMachine.Phases
{
    /// <summary>
    /// Handles the Combat Phase where units battle each other and the phase transitions to the next round.
    /// </summary>
    public class CombatPhaseState : IPhaseState
    {
        #region Properties
        /// <summary>
        /// Gets the type of game phase this state represents (Combat).
        /// </summary>
        public GamePhase PhaseType => GamePhase.Combat;
        #endregion

        #region Command Validation
        /// <summary>
        /// Determines whether a specific command is allowed in the current combat phase.
        /// </summary>
        /// <param name="command">The command to validate.</param>
        /// <param name="state">The current game state.</param>
        /// <returns>True only for EndPhaseCommand, false for all other commands.</returns>
        public bool IsCommandAllowed(IGameCommand command, GameState state)
        {
            return command is EndPhaseCommand || command is SelectTargetCommand;
        }

        /// <summary>
        /// Determines whether the combat phase should end automatically without player input.
        /// </summary>
        /// <param name="state">The current game state.</param>
        /// <returns>Always true for combat phase - ends automatically after resolution.</returns>
        public bool ShouldEndPhaseAutomatically(GameState state)
        {
            return state.PendingInteraction == null;
        }
        #endregion

        #region Phase Transition Logic
        /// <summary>
        /// Processes the end of the combat phase, resolves combat, and transitions to the next round.
        /// </summary>
        /// <param name="currentState">The current game state at phase end.</param>
        /// <param name="eventBus">The event bus for publishing combat and transition events.</param>
        /// <param name="context">The game context with additional services.</param>
        /// <returns>The updated game state for the next round.</returns>
        public GameState ProcessEndPhase(GameState currentState, EventBus eventBus, GameContext context)
        {
            var orchestrator = new CombatOrchestrator();

            // 1. Run the (potentially resumed) combat
            var stateAfterCombat = orchestrator.ResolveCombatPhase(currentState, eventBus, context);

            // 2. CHECK FOR PAUSE (Bug #2 FIX)
            // If the Occultist is waiting for a target, we stop here and DO NOT transition to Round 2.
            if (stateAfterCombat.PendingInteraction != null)
            {
                return stateAfterCombat;
            }

            // 3. COMBAT IS FULLY OVER - PROCEED TO ROUND END CLEANUP
            var workingState = stateAfterCombat;

            // Process Keywords like 'Burning' for all units
            foreach (var unit in workingState.Board.GetAllUnits())
                workingState = context.Keywords.ProcessRoundEnd(workingState, unit, context);

            // Final death check after Burning/End-of-round effects
            workingState = new DeathResolver().ResolveDeaths(workingState, eventBus, context);

            // Check if the game ended during Round-End processing
            if (workingState.PlayerA.Health <= 0 || workingState.PlayerB.Health <= 0)
                return workingState;

            // 4. TRANSITION TO NEXT ROUND
            int nextRoundStarter = 3 - currentState.RoundStartingPlayerId;
            int nextTurnNumber = currentState.TurnNumber + 1;
            int manaLimit = nextTurnNumber;

            // Player A draw and resource refill
            int oldBloodA = workingState.PlayerA.CurrentBlood;
            var playerA = workingState.PlayerA.WithTurnStartBlood(manaLimit, true).WithCardDrawn(eventBus);
            eventBus.Publish(new ResourceChangedEvent(playerA.PlayerId, oldBloodA, playerA.CurrentBlood));

            // Player B draw and resource refill
            int oldBloodB = workingState.PlayerB.CurrentBlood;
            var playerB = workingState.PlayerB.WithTurnStartBlood(manaLimit, true).WithCardDrawn(eventBus);
            eventBus.Publish(new ResourceChangedEvent(playerB.PlayerId, oldBloodB, playerB.CurrentBlood));

            // Return the state for the new round, RESETTING combatLineIndex to 0
            return workingState.With(
                turnNumber: nextTurnNumber,
                currentPhase: GamePhase.UnitOnly,
                activePlayerId: nextRoundStarter,
                roundStartingPlayerId: nextRoundStarter,
                playerA: playerA,
                playerB: playerB,
                combatLineIndex: 0,
                combatStep: 0
            );
        }
        #endregion
    }
}