using CardGame.Core.Application;
using CardGame.Core.Cards.Component.Actions;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Logic;
using CardGame.Core.Events;
using CardGame.Core.Events.Interfaces;
using CardGame.Core.State.Models;
using System;
using System.Linq;

namespace CardGame.Core.Cards.Components.Actions.Handlers
{
    public class TutorCardHandler : IActionHandler
    {
        public ActionType Type => ActionType.TutorCard;

        public GameState Execute(GameState state, GameContext context, ActionData action, EffectTargets targets, int sourceId, IGameEvent gameEvent)
        {
        
            if (targets.TargetPlayer != null && gameEvent is CardPlayedEvent cpe && cpe.SelectedTargetId.HasValue)
            {
                var card = targets.TargetPlayer.DrawPile.FirstOrDefault(c => c.InstanceId == cpe.SelectedTargetId.Value);
                if (card != null)
                {
                  
                    return state.UpdatePlayer(targets.TargetPlayer.WithCardRemovedFromDeck(card).WithCardAddedToHand(card));
                }
                else
                {
                  
                }
            }
            return state;
        }
    }
}