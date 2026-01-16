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
    #region Combat and Utility Handlers

    /// <summary>
    /// Handles the SacrificeUnit action, voluntarily destroying a friendly unit for an effect.
    /// </summary>
    public class SacrificeUnitHandler : IActionHandler
    {
        /// <summary>
        /// Gets the action type this handler processes.
        /// </summary>
        public ActionType Type => ActionType.SacrificeUnit;

        /// <summary>
        /// Executes the SacrificeUnit action, destroying a friendly unit and triggering sacrifice effects.
        /// </summary>
        public GameState Execute(
            GameState state,
            GameContext context,
            ActionData action,
            EffectTargets targets,
            int sourceId,
            IGameEvent gameEvent)
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

                // Publish the sacrifice event before dealing damage
                context.Events.Publish(new UnitSacrificedEvent(targets.TargetUnit, lineIndex));

                // Apply lethal damage to the sacrificed unit
                var deadUnit = targets.TargetUnit.TakeDamage(9999);
                return state.UpdateBoard(state.Board.UpdateUnit(deadUnit));
            }

            return state;
        }
    }

    #endregion
}