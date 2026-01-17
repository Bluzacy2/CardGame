using CardGame.Core.Application;
using CardGame.Core.Cards.Component.Actions;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Logic;
using CardGame.Core.Events;
using CardGame.Core.Events.Interfaces;
using CardGame.Core.State.Models;
using System;
using System.Linq;
using System.Collections.Generic;

namespace CardGame.Core.Cards.Components.Actions.Handlers
{
    public class TutorCardHandler : IActionHandler
    {
        public ActionType Type => ActionType.TutorCard;

        public GameState Execute(GameState state, GameContext context, ActionData action, EffectTargets targets, int sourceId, IGameEvent gameEvent)
        {
            var player = targets.TargetPlayer ?? state.GetPlayer(gameEvent.SourcePlayerId);

            // 1. SPRAWDZAMY CZY TO ODPOWIEDŹ NA WYBÓR
            // Jeśli zdarzenie to TargetSelectedEvent, to znaczy, że gracz wybrał kartę z listy.
            // Nie sprawdzamy state.PendingInteraction, bo JsonEffectComponent już go wyczyścił!

            if (gameEvent is TargetSelectedEvent tse)
            {
                int idx = tse.SelectedTargetId;
                var deck = player.DrawPile.ToList();

                // Walidacja indeksu
                if (idx >= 0 && idx < deck.Count)
                {
                    var card = deck[idx];

                    // Dobieramy kartę: Usuwamy z DrawPile, dodajemy do Hand
                    // Pending jest już wyczyszczone przez komponent nadrzędny
                    return state.UpdatePlayer(player.WithCardRemovedFromDeck(card).WithCardAddedToHand(card));
                }

                // Jeśli indeks błędny, zwracamy stan bez zmian (ewentualnie log błędu)
                return state;
            }

            // 2. INICJALIZACJA (Jeśli to CardPlayedEvent, czyli pierwsze zagranie)

            var currentDeck = player.DrawPile;
            if (currentDeck.Count == 0) return state; // Pusta talia

            // Tworzymy listę opcji dla klienta
            var options = currentDeck.Select(c => $"{c.Definition.Name} ({c.CurrentStats.BloodCost})").ToList();

            // Zwracamy stan zawieszony, oczekując na wybór
            return state.With(
                pendingInteraction: new PendingInteraction(
                    sourceId,
                    0, // Effect Index
                    0, // Action Index
                    TargetType.Choice,
                    options
                )
            );
        }
    }
}