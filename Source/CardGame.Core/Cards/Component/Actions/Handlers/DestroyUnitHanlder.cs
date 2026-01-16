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
    #region Combat and Removal Handlers

    /// <summary>
    /// Handles the DestroyUnit action, immediately removing units from the board regardless of health.
    /// </summary>
    public class DestroyUnitHandler : IActionHandler
    {
        /// <summary>
        /// Gets the action type this handler processes.
        /// </summary>
        public ActionType Type => ActionType.DestroyUnit;

        /// <summary>
        /// Executes the DestroyUnit action, instantly destroying target units.
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

            foreach (var targetUnit in targets.UnitTargets)
            {
                // If the unit belongs to the player who caused the event, treat as sacrifice
                if (targetUnit.OwnerPlayerId == gameEvent.SourcePlayerId)
                {
                    context.Events.Publish(new UnitSacrificedEvent(targetUnit, -1));
                }

                // Apply lethal damage to destroy the unit
                var deadUnit = targetUnit.TakeDamage(99999);
                workingState = workingState.UpdateBoard(workingState.Board.UpdateUnit(deadUnit));
            }

            return workingState;
        }
    }

    #endregion
}