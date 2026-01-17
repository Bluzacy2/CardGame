using CardGame.Core.Application;
using CardGame.Core.Cards.Component.Actions;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Logic;
using CardGame.Core.Events.Interfaces;
using CardGame.Core.State.Models;
using System;

namespace CardGame.Core.Cards.Components.Actions.Handlers
{
    public class ShuffleDeckHandler : IActionHandler
    {
        public ActionType Type => ActionType.ShuffleDeck;

        public GameState Execute(GameState state, GameContext context, ActionData action, EffectTargets targets, int sourceId, IGameEvent gameEvent)
        {
            if (targets.TargetPlayer != null)
            {
                var shufPlayer = targets.TargetPlayer.WithShuffledDeck(context.Rng);

                // NEW:
                context.Events.Publish(new DeckShuffledEvent(shufPlayer.PlayerId));
                return state.UpdatePlayer(shufPlayer);
            }
            return state;
        }
    }
}