using CardGame.Core.Application;
using CardGame.Core.Commands.Interfaces;
using CardGame.Core.Events;
using CardGame.Core.State.Models;
using System.Collections.Generic;

namespace CardGame.Core.Commands.Implementations
{
    /// <summary>
    /// Command to confirm mulligan choices and replace specified cards from the player's hand.
    /// </summary>
    public class ConfirmMulliganCommand : IGameCommand
    {
        #region Properties
        /// <summary>
        /// Gets the ID of the player confirming the mulligan.
        /// </summary>
        public int PlayerId { get; }

        /// <summary>
        /// Gets the list of card instance IDs to be replaced during the mulligan.
        /// </summary>
        public List<int> RejectedCardIds { get; }
        #endregion

        #region Constructor
        /// <summary>
        /// Initializes a new instance of the ConfirmMulliganCommand class.
        /// </summary>
        /// <param name="playerId">The ID of the player confirming the mulligan.</param>
        /// <param name="rejectedCardIds">The list of card instance IDs to be replaced, or null for an empty list.</param>
        public ConfirmMulliganCommand(int playerId, List<int> rejectedCardIds)
        {
            PlayerId = playerId;
            RejectedCardIds = rejectedCardIds ?? new List<int>();
        }
        #endregion

        #region Command Execution
        /// <summary>
        /// Executes the mulligan confirmation, replacing specified cards and marking the player as ready.
        /// </summary>
        /// <param name="currentState">The current game state.</param>
        /// <param name="eventBus">The event bus for publishing game events.</param>
        /// <param name="context">The game context with additional services.</param>
        /// <returns>The updated game state after performing the mulligan.</returns>
        public GameState Execute(GameState currentState, EventBus eventBus, GameContext context)
        {
            // 1. Get the player
            var player = currentState.GetPlayer(PlayerId);

            // 2. Perform the card exchange logic
            var newPlayer = player.WithMulliganPerformed(RejectedCardIds);

            // 3. Mark the player as ready in the game state
            var newState = currentState
                .UpdatePlayer(newPlayer)
                .MarkPlayerAsReady(PlayerId);

            // We could generate an event (optional)
            // eventBus.Publish(new MulliganCompletedEvent(PlayerId));

            return newState;
        }
        #endregion
    }
}