using CardGame.Core.Application;
using CardGame.Core.Cards.Logic;
using CardGame.Core.Commands.Interfaces;
using CardGame.Core.Events;
using CardGame.Core.State.Models;
using System;
using System.Linq;

namespace CardGame.Core.Commands.Implementations
{
    /// <summary>
    /// Command to play a unit card from the player's hand onto a specified board line.
    /// </summary>
    public class PlayUnitCommand : IGameCommand
    {
        #region Properties
        /// <summary>
        /// Gets the ID of the player playing the unit.
        /// </summary>
        public int PlayerId { get; }

        /// <summary>
        /// Gets the instance ID of the unit card being played.
        /// </summary>
        public int CardInstanceId { get; }

        /// <summary>
        /// Gets the target line index where the unit should be placed.
        /// </summary>
        public int TargetLineIndex { get; }

        /// <summary>
        /// Gets the optional ID of a selected target for the unit's abilities.
        /// </summary>
        public int? SelectedTargetId { get; }
        #endregion

        #region Constructor
        /// <summary>
        /// Initializes a new instance of the PlayUnitCommand class.
        /// </summary>
        /// <param name="playerId">The ID of the player playing the unit.</param>
        /// <param name="cardInstanceId">The instance ID of the unit card being played.</param>
        /// <param name="targetLineIndex">The line index where the unit should be placed.</param>
        /// <param name="selectedTargetId">Optional ID of a selected target for the unit's abilities.</param>
        public PlayUnitCommand(int playerId, int cardInstanceId, int targetLineIndex, int? selectedTargetId = null)
        {
            PlayerId = playerId;
            CardInstanceId = cardInstanceId;
            TargetLineIndex = targetLineIndex;
            SelectedTargetId = selectedTargetId;
        }
        #endregion

        #region Command Execution
        /// <summary>
        /// Executes the unit playing command, validating the play, paying costs, and placing the unit on the board.
        /// </summary>
        /// <param name="currentState">The current game state.</param>
        /// <param name="eventBus">The event bus for publishing game events.</param>
        /// <param name="context">The game context with additional services.</param>
        /// <returns>The updated game state after playing the unit.</returns>
        public GameState Execute(GameState currentState, EventBus eventBus, GameContext context)
        {
            var playerState = currentState.GetPlayer(PlayerId);
            int oldBlood = playerState.CurrentBlood;

            var card = playerState.Hand.FirstOrDefault(c => c.InstanceId == CardInstanceId);

            if (card == null) return currentState;

            if (!PlayValidator.CanPlay(card, currentState, PlayerId))
            {
                return currentState;
            }

            if (!currentState.Board.Lines[TargetLineIndex].IsSlotEmpty(PlayerId))
            {
                return currentState;
            }

            var newPlayerState = playerState
                .WithBloodSpent(card.CurrentStats.BloodCost)
                .WithCardRemovedFromHand(card);

            eventBus.Publish(new ResourceChangedEvent(PlayerId, oldBlood, newPlayerState.CurrentBlood));

            var cardToPlay = card.AddPermanentBuff(playerState.GlobalUnitBuffs);
            var newBoardState = currentState.Board.WithUnitPlacedAt(TargetLineIndex, PlayerId, cardToPlay);

            eventBus.Publish(new CardMovedEvent(card.InstanceId, PlayerId, CardZone.Hand, CardZone.Board, TargetLineIndex));
            eventBus.Publish(new CardPlayedEvent(PlayerId, card, TargetLineIndex, SelectedTargetId));

            return currentState.With(
                playerA: PlayerId == 1 ? newPlayerState : currentState.PlayerA,
                playerB: PlayerId == 2 ? newPlayerState : currentState.PlayerB,
                board: newBoardState);
        }
        #endregion
    }
}