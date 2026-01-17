using CardGame.Core.Application;
using CardGame.Core.Cards.Logic;
using CardGame.Core.Commands.Interfaces;
using CardGame.Core.Events;
using CardGame.Core.State.Models;
using System;
using System.Linq;

namespace CardGame.Core.Commands.Implementations
{
    public class PlaySpellCommand : IGameCommand
    {
        public int PlayerId { get; }
        public int CardInstanceId { get; }
        public int? SelectedTargetId { get; }

        public PlaySpellCommand(int playerId, int cardInstanceId, int? selectedTargetId = null)
        {
            PlayerId = playerId;
            CardInstanceId = cardInstanceId;
            SelectedTargetId = selectedTargetId;
        }

        public GameState Execute(GameState currentState, EventBus eventBus, GameContext context)
        {
            var player = currentState.GetPlayer(PlayerId);
            int oldBlood = player.CurrentBlood;

            var card = player.Hand.FirstOrDefault(c => c.InstanceId == CardInstanceId);

            if (card == null) return currentState;

            if (!PlayValidator.CanPlay(card, currentState, PlayerId))
            {
                return currentState;
            }

            var newPlayer = player.WithBloodSpent(card.CurrentStats.BloodCost)
                                  .WithCardRemovedFromHand(card);

            eventBus.Publish(new ResourceChangedEvent(PlayerId, oldBlood, newPlayer.CurrentBlood));

            var newStack = currentState.SpellStack.ToList();
            newStack.Add(card);

            var newState = currentState.UpdatePlayer(newPlayer).With(spellStack: newStack);

            eventBus.Publish(new CardMovedEvent(card.InstanceId, PlayerId, CardZone.Hand, CardZone.Stack));
            eventBus.Publish(new CardPlayedEvent(PlayerId, card, null, SelectedTargetId));

            return newState;
        }
    }
}