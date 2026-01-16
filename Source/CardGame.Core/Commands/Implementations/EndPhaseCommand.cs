using CardGame.Core.Application;
using CardGame.Core.Commands.Interfaces;
using CardGame.Core.Events;
using CardGame.Core.State.Models;
using CardGame.Core.StateMachine.Interfaces;

namespace CardGame.Core.Commands.Implementations
{
    /// <summary>
    /// Command that signals the state machine to end the current game phase and transition to the next phase.
    /// Unlike other commands, it does not modify game state directly but triggers a phase transition.
    /// </summary>
    public class EndPhaseCommand : IGameCommand
    {
        #region Properties
        /// <summary>
        /// Gets the ID of the player ending the phase.
        /// </summary>
        public int PlayerId { get; }
        #endregion

        #region Constructor
        /// <summary>
        /// Initializes a new instance of the EndPhaseCommand class.
        /// </summary>
        /// <param name="playerId">The ID of the player ending the phase.</param>
        public EndPhaseCommand(int playerId)
        {
            PlayerId = playerId;
        }
        #endregion

        #region Command Execution
        /// <summary>
        /// Signals the state machine to end the current phase and transition to the next phase.
        /// This command is an exception to the usual pattern - it does not modify game state directly
        /// but returns the current state unchanged, allowing the state machine to handle the phase transition.
        /// </summary>
        /// <param name="currentState">The current game state.</param>
        /// <param name="eventBus">The event bus for publishing game events.</param>
        /// <param name="context">The game context with additional services.</param>
        /// <returns>The current game state unchanged, signaling that phase transition logic should be handled elsewhere.</returns>
        public GameState Execute(GameState currentState, EventBus eventBus, GameContext context)
        {
            // Phase ending logic is an exception to the rule. Most other commands directly modify game state
            // by performing operations such as deducting health points, playing cards, etc.
            // Ending a phase is different because the StateMachine decides how and to which phase to transition next.
            // Therefore, we return the current game state and signal the StateMachine to end the phase
            // and proceed to the next one.
            return currentState;
        }
        #endregion
    }
}