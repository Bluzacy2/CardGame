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
    #region Status Effect Handlers

    /// <summary>
    /// Handles the Silence action, removing abilities and status effects from units.
    /// </summary>
    public class SilenceHandler : IActionHandler
    {
        /// <summary>
        /// Gets the action type this handler processes.
        /// </summary>
        public ActionType Type => ActionType.Silence;

        /// <summary>
        /// Executes the Silence action, removing keywords and statuses from target units.
        /// </summary>
        public GameState Execute(
            GameState state,
            GameContext context,
            ActionData action,
            EffectTargets targets,
            int sourceId,
            IGameEvent gameEvent)
        {
            var workingState = state;

            foreach (var unit in targets.UnitTargets)
            {
                var latestUnit = workingState.Board.GetAllUnits().FirstOrDefault(u => u.InstanceId == unit.InstanceId);
                if (latestUnit == null)
                {
                    continue;
                }

                var silencedUnit = latestUnit.Silence();
                context.Events.Publish(new UnitSilencedEvent(silencedUnit.InstanceId, sourceId, gameEvent.SourcePlayerId));
                workingState = workingState.UpdateBoard(workingState.Board.UpdateUnit(silencedUnit));
            }

            return workingState;
        }
    }

    #endregion
}