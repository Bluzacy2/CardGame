using CardGame.Core.Application;
using CardGame.Core.Cards.Component.Actions;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Logic;
using CardGame.Core.Events.Interfaces;
using CardGame.Core.State.Models;

namespace CardGame.Core.Cards.Components.Actions.Handlers
{
    public class DrawFromDiscardHandler : IActionHandler
    {
        public ActionType Type => ActionType.DrawFromDiscard;
        public GameState Execute(GameState state, GameContext context, ActionData action, EffectTargets targets, int sourceId, IGameEvent gameEvent)
        {
            if (targets.TargetPlayer == null) return state;
            var newPlayer = targets.TargetPlayer.WithCardsDrawnFromDiscard(action.Amount);
            return state.UpdatePlayer(newPlayer);
        }
    }
}