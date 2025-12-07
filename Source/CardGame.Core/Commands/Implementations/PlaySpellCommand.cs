using System;
using System.Linq;
using CardGame.Core.Cards.Data;
using CardGame.Core.Commands.Interfaces;
using CardGame.Core.Events;
using CardGame.Core.State.Models;

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

      
        public GameState Execute(GameState currentState, EventBus eventBus)
        {
            var playerState = currentState.GetPlayer(PlayerId);

            // 1. Znajdź kartę
            var card = playerState.Hand.FirstOrDefault(c => c.InstanceId == CardInstanceId);
            if (card == null)
                throw new InvalidOperationException($"Gracz {PlayerId} nie ma karty {CardInstanceId} w ręce!");

            // 2. Walidacja typu (Teraz card.Definition.Type zadziała!)
            if (card.Definition.Type != CardType.Spell)
                throw new InvalidOperationException($"Karta {card.Definition.Name} nie jest czarem!");

            // 3. Walidacja kosztu
            if (!playerState.CanPlayCard(card.CurrentStats.BloodCost))
                throw new InvalidOperationException("Za mało krwi!");

            // --- WYKONANIE ---

            // A. Płacimy i wyrzucamy na cmentarz
            var newPlayerState = playerState
                .WithBloodSpent(card.CurrentStats.BloodCost)
                .WithCardRemovedFromHand(card)
                .WithCardAddedToDiscard(card);

            // B. Event (Uruchamia efekt OnPlayed)
            eventBus.Publish(new CardPlayedEvent(PlayerId, card, null, SelectedTargetId));

            // C. Zwracamy stan
            return currentState.UpdatePlayer(newPlayerState);
        }
    }
}