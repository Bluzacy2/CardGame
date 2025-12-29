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
    public class DestroyUnitHandler : IActionHandler
    {
        public ActionType Type => ActionType.DestroyUnit;

        public GameState Execute(GameState state, GameContext context, ActionData action, EffectTargets targets, int sourceId, IGameEvent gameEvent)
        {
            var workingState = state;
            foreach (var targetUnit in targets.UnitTargets)
            {
             
                if (targetUnit.OwnerPlayerId == gameEvent.SourcePlayerId)
                {
                    context.Events.Publish(new UnitSacrificedEvent(targetUnit, -1));
                }

             
                var deadUnit = targetUnit.TakeDamage(99999);
                workingState = workingState.UpdateBoard(workingState.Board.UpdateUnit(deadUnit));
            }

            return workingState;
        }
    }
}