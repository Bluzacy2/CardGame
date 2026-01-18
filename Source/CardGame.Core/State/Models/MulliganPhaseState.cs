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
    /// Handles the Mulligan Phase where players can replace cards from their initial hand before the game begins.
    /// </summary>
    public class MulliganPhaseState : IPhaseState
    {
        #region Properties
        /// <summary>
        /// Gets the type of game phase this state represents (Mulligan).
        /// </summary>
        public GamePhase PhaseType => GamePhase.Mulligan;
        #endregion

        #region Command Validation
        /// <summary>
        /// Determines whether a specific command is allowed in the current mulligan phase.
        /// </summary>
        /// <param name="command">The command to validate.</param>
        /// <param name="state">The current game state.</param>
        /// <returns>True only for ConfirmMulliganCommand, false for all other commands.</returns>
        public bool IsCommandAllowed(IGameCommand command, GameState state) => 
            command is ConfirmMulliganCommand;

        /// <summary>
        /// Determines whether the mulligan phase should end automatically when both players are ready.
        /// </summary>
        /// <param name="state">The current game state.</param>
        /// <returns>True if both players have confirmed their mulligan, otherwise false.</returns>
        public bool ShouldEndPhaseAutomatically(GameState state) => 
            state.PlayersReady.Contains(1) && state.PlayersReady.Contains(2);
        #endregion

        #region Phase Transition Logic
        /// <summary>
        /// Processes the end of the mulligan phase and transitions to the first unit phase of the game.
        /// </summary>
        /// <param name="currentState">The current game state at phase end.</param>
        /// <param name="eventBus">The event bus for publishing phase transition events.</param>
        /// <param name="context">The game context with additional services.</param>
        /// <returns>The updated game state with phase set to UnitOnly and both players drawing a card.</returns>
        public GameState ProcessEndPhase(GameState currentState, EventBus eventBus, GameContext context)
        {
            // Start Round 1: Both players draw a card
            var playerA = currentState.PlayerA.WithCardDrawn(eventBus);
            var playerB = currentState.PlayerB.WithCardDrawn(eventBus);

            return currentState.With(
                currentPhase: GamePhase.UnitOnly,
                activePlayerId: 1,
                roundStartingPlayerId: 1,
                playerA: playerA,
                playerB: playerB
            );
        }
        #endregion
    }
}