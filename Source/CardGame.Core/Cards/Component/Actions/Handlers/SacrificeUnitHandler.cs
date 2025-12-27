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
    public class SacrificeUnitHandler : IActionHandler
    {
        public ActionType Type => ActionType.SacrificeUnit;

        public GameState Execute(GameState state, GameContext context, ActionData action, EffectTargets targets, int sourceId, IGameEvent gameEvent)
        {
            if (targets.TargetUnit != null)
            {
                int lineIndex = -1;
                for (int i = 0; i < 4; i++)
                {
                    if (state.Board.Lines[i].Player1Unit?.InstanceId == targets.TargetUnit.InstanceId ||
                        state.Board.Lines[i].Player2Unit?.InstanceId == targets.TargetUnit.InstanceId)
                    {
                        lineIndex = i;
                        break;
                    }
                }

               
                context.Events.Publish(new UnitSacrificedEvent(targets.TargetUnit, lineIndex));

            
                var deadUnit = targets.TargetUnit.TakeDamage(9999);
                return state.UpdateBoard(state.Board.UpdateUnit(deadUnit));
            }
            return state;
        }
    }
}