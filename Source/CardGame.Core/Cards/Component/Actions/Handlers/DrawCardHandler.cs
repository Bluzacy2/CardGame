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
                var workingPlayer = targets.TargetPlayer;
                int amount = action.Amount > 0 ? action.Amount : 1;

                Console.WriteLine($"[EFEKT] Gracz {workingPlayer.PlayerId} dobiera {amount} kart.");

                for (int i = 0; i < amount; i++)
                {
                    workingPlayer = workingPlayer.WithCardDrawn();
                }

                return state.UpdatePlayer(workingPlayer);
            }
            return state;
        }
    }
}