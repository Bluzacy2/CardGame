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
    #region Card Creation Handlers

    /// <summary>
    /// Handles the AddCardToHand action, creating new card instances and adding them to a player's hand.
    /// </summary>
    public class AddCardToHandHandler : IActionHandler
    {
        /// <summary>
        /// Gets the action type this handler processes.
        /// </summary>
        public ActionType Type => ActionType.AddCardToHand;

        /// <summary>
        /// Executes the AddCardToHand action, creating a new card and adding it to the target player's hand.
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
                try
                {
                    int playerId = targets.TargetPlayer.PlayerId;
                    var tokenCard = context.Factory.CreateCard(action.ValueParam, playerId);
                    var freshPlayer = state.GetPlayer(playerId);

                    context.Events.Publish(new CardCreatedEvent(tokenCard, playerId));
                    context.Events.Publish(new CardMovedEvent(
                        tokenCard.InstanceId,
                        playerId,
                        CardZone.Deck,
                        CardZone.Hand));

                    return state.UpdatePlayer(freshPlayer.WithCardAddedToHand(tokenCard));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] AddCardToHandHandler: {ex.Message}");
                }
            }

            return state;
        }
    }

    #endregion
}