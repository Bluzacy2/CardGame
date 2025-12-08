using CardGame.Core.Application;
using CardGame.Core.Cards.Component.Actions;
using CardGame.Core.Cards.Components.Actions;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Logic;
using CardGame.Core.Events.Interfaces;
using CardGame.Core.State.Models;
using System;

namespace CardGame.Core.Cards.Components.Actions.Handlers
{
    public class DestroyUnitHandler : IActionHandler
    {
        public ActionType Type => ActionType.DestroyUnit;

        public GameState Execute(GameState state, GameContext context, ActionData action, EffectTargets targets, int sourceId, IGameEvent gameEvent)
        {
            foreach (var targetUnit in targets.UnitTargets)
            {
                Console.WriteLine($"[EFEKT] DestroyUnit: {targetUnit.Definition.Name}");
                var deadUnit = targetUnit.TakeDamage(99999);
                state = state.UpdateBoard(state.Board.UpdateUnit(deadUnit));
            }
            
            return state;
        }
    }
}