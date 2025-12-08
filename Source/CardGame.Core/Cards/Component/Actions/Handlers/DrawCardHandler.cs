using CardGame.Core.Application;
using CardGame.Core.Cards.Component.Actions;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Logic;
using CardGame.Core.Events.Interfaces;
using CardGame.Core.State.Models;
using System;

namespace CardGame.Core.Cards.Components.Actions.Handlers
{
    public class DrawCardHandler : IActionHandler
    {
        public ActionType Type => ActionType.DrawCard;

        public GameState Execute(GameState state, GameContext context, ActionData action, EffectTargets targets, int sourceId, IGameEvent gameEvent)
        {
            if (targets.TargetPlayer != null)
            {
                Console.WriteLine($"[EFEKT] Gracz {targets.TargetPlayer.PlayerId} dobiera kartę.");
                return state.UpdatePlayer(targets.TargetPlayer.WithCardDrawn());
            }
            return state;
        }
    }
}