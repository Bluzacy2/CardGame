using CardGame.Core.Application;
using CardGame.Core.Cards.Component.Actions;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Logic;
using CardGame.Core.Events;
using CardGame.Core.Events.Interfaces;
using CardGame.Core.State.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CardGame.Core.Cards.Components.Actions.Handlers
{
    /// <summary>
    /// Handler for the TutorCard action, which allows a player to search their deck for a specific card.
    /// </summary>
    public class TutorCardHandler : IActionHandler
    {
        /// <summary>
        /// Gets the action type this handler processes.
        /// </summary>
        public ActionType Type => ActionType.TutorCard;

        /// <summary>
        /// Executes the TutorCard action, either presenting card choices or processing the player's selection.
        /// </summary>
        /// <param name="state">The current game state.</param>
        /// <param name="context">The game context providing access to services.</param>
        /// <param name="action">The action data defining the tutor effect.</param>
        /// <param name="targets">The resolved targets for the action.</param>
        /// <param name="sourceId">The ID of the card that initiated the action.</param>
        /// <param name="gameEvent">The event that triggered this action.</param>
        /// <returns>The updated game state after processing the tutor action.</returns>
        public GameState Execute(
            GameState state,
            GameContext context,
            ActionData action,
            EffectTargets targets,
            int sourceId,
            IGameEvent gameEvent)
        {
            int choosingPlayerId = EffectTargetResolver.GetOwner(state, sourceId, gameEvent);
            var player = targets.TargetPlayer ?? state.GetPlayer(gameEvent.SourcePlayerId);

            // 1. CHECK IF THIS IS A RESPONSE TO A PLAYER CHOICE
            // If the event is TargetSelectedEvent, the player has chosen a card from the list.
            // We don't check state.PendingInteraction because JsonEffectComponent already cleared it!
            if (gameEvent is TargetSelectedEvent targetSelectedEvent)
            {
                int selectedIndex = targetSelectedEvent.SelectedTargetId;
                var deck = player.DrawPile.ToList();

                // Validate index
                if (selectedIndex >= 0 && selectedIndex < deck.Count)
                {
                    var selectedCard = deck[selectedIndex];

                    // Draw the card: Remove from DrawPile, add to Hand
                    // Pending interaction is already cleared by the parent component
                    return state.UpdatePlayer(
                        player.WithCardRemovedFromDeck(selectedCard)
                              .WithCardAddedToHand(selectedCard));
                }

                // If index is invalid, return state unchanged (optionally log error)
                return state;
            }

            // 2. INITIALIZATION (If this is CardPlayedEvent, i.e., first play)
            var currentDeck = player.DrawPile;
            if (currentDeck.Count == 0)
            {
                return state; // Empty deck
            }

            // Create choice options for the client
            var options = currentDeck
                .Select(c => $"{c.Definition.Name} ({c.CurrentStats.BloodCost})")
                .ToList();

            // Return pending state, awaiting player choice
            return state.With(
                pendingInteraction: new PendingInteraction(
                    sourceId,
                    0, // Effect Index
                    0, // Action Index
                    TargetType.Choice,
                    choosingPlayerId,
                    options));
        }
    }
}