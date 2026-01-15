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
    public class AddCardToHandHandler : IActionHandler
    {
        public ActionType Type => ActionType.AddCardToHand;

        public GameState Execute(GameState state, GameContext context, ActionData action, EffectTargets targets, int sourceId, IGameEvent gameEvent)
        {
            if (targets.TargetPlayer != null)
            {
                try
                {
                    int pid = targets.TargetPlayer.PlayerId;
                    var tokenCard = context.Factory.CreateCard(action.ValueParam, pid);

                    var freshPlayer = state.GetPlayer(pid);

                    context.Events.Publish(new CardCreatedEvent(tokenCard, pid));
                    context.Events.Publish(new CardMovedEvent(tokenCard.InstanceId, pid, CardZone.Deck, CardZone.Hand));

                    return state.UpdatePlayer(freshPlayer.WithCardAddedToHand(tokenCard));

                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[BŁĄD] AddCardToHandHandler: {ex.Message}");
                }
            }
            return state;
        }
    }
}