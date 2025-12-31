using CardGame.Core.Application;
using CardGame.Core.Cards.Component.Actions;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Logic;
using CardGame.Core.Events.Interfaces;
using CardGame.Core.State.Models;

namespace CardGame.Core.Cards.Components.Actions.Handlers
{
    public class AddResourceHandler : IActionHandler
    {
        public ActionType Type => ActionType.AddResource;

        public GameState Execute(GameState state, GameContext context, ActionData action, EffectTargets targets, int sourceId, IGameEvent gameEvent)
        {
         
            if (targets.TargetPlayer != null)
            {
                var player = state.GetPlayer(targets.TargetPlayer.PlayerId);
           
                int amount = action.Amount > 0 ? action.Amount : 1;
                var newPlayer = player.With(currentBlood: player.CurrentBlood + amount);

                return state.UpdatePlayer(newPlayer);
            }
            return state;
        }
    }
}