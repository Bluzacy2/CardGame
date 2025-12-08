using CardGame.Core.Application;
using CardGame.Core.Cards.Component.Actions;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Logic;
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
                    var tokenCard = context.Factory.CreateCard(action.ValueParam, targets.TargetPlayer.PlayerId);
                    Console.WriteLine($"[EFEKT] Dodano {tokenCard.Definition.Name} do ręki.");
                    return state.UpdatePlayer(targets.TargetPlayer.WithCardAddedToHand(tokenCard));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[BŁĄD] AddCard: {ex.Message}");
                }
            }
            return state;
        }
    }
}