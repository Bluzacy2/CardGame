using CardGame.Core.Application;
using CardGame.Core.Cards.Component.Actions;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Logic;
using CardGame.Core.Events;
using CardGame.Core.Events.Interfaces;
using CardGame.Core.State.Models;
using System;

namespace CardGame.Core.Cards.Components.Actions.Handlers
{
    #region Deck Manipulation Handlers

    /// <summary>
    /// Handles the ShuffleDeck action, randomizing the order of cards in a player's draw pile.
    /// </summary>
    public class ShuffleDeckHandler : IActionHandler
    {
        /// <summary>
        /// Gets the action type this handler processes.
        /// </summary>
        public ActionType Type => ActionType.ShuffleDeck;

        /// <summary>
        /// Executes the ShuffleDeck action, randomizing the target player's draw pile.
        /// </summary>
        public GameState Execute(
            GameState state,
            GameContext context,
            ActionData action,
            EffectTargets targets,
            int sourceId,
            IGameEvent gameEvent)
        {
            if (targets.TargetPlayer != null)
            {
                var shuffledPlayer = targets.TargetPlayer.WithShuffledDeck(context.Rng);
                context.Events.Publish(new DeckShuffledEvent(shuffledPlayer.PlayerId));

                return state.UpdatePlayer(shuffledPlayer);
            }

            return state;
        }
    }

    #endregion
}