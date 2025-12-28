using System;
using System.Linq;
using CardGame.Core.Events;
using CardGame.Core.Application;
using CardGame.Core.State.Models;
using CardGame.Core.Commands.Interfaces;
using CardGame.Core.Cards.Logic;

namespace CardGame.Core.Commands.Implementations
{
    public class PlayUnitCommand : IGameCommand
    {
        public int PlayerId { get; }
        public int CardInstanceId { get; }
        public int TargetLineIndex { get; }
        public int? SelectedTargetId { get; }

        public PlayUnitCommand(int playerId, int cardInstanceId, int targetLineIndex, int? selectedTargetId = null)
        {
            PlayerId = playerId;
            CardInstanceId = cardInstanceId;
            TargetLineIndex = targetLineIndex;
            SelectedTargetId = selectedTargetId;
        }

        public GameState Execute(GameState currentState, EventBus eventBus, GameContext context)
        {
            var playerState = currentState.GetPlayer(PlayerId);
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

            var cardToPlay = card.AddPermanentBuff(playerState.GlobalUnitBuffs);
            var newBoardState = currentState.Board.WithUnitPlacedAt(TargetLineIndex, PlayerId, cardToPlay);

            eventBus.Publish(new CardPlayedEvent(PlayerId, card, TargetLineIndex, SelectedTargetId));

            return currentState.With(
                playerA: PlayerId == 1 ? newPlayerState : currentState.PlayerA,
                playerB: PlayerId == 2 ? newPlayerState : currentState.PlayerB,
                board: newBoardState);
        }
    }
}