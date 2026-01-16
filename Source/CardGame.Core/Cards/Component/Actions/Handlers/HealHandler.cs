using CardGame.Core.Application;
using CardGame.Core.Cards.Component.Actions;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Logic;
using CardGame.Core.Cards.Models;
using CardGame.Core.Events;
using CardGame.Core.Events.Interfaces;
using CardGame.Core.State.Models;
using System;
using System.Linq;

namespace CardGame.Core.Cards.Components.Actions.Handlers
{
    #region Healing and Restoration Handlers

    /// <summary>
    /// Handles the Heal action, restoring health to players and units.
    /// </summary>
    public class HealHandler : IActionHandler
    {
        /// <summary>
        /// Gets the action type this handler processes.
        /// </summary>
        public ActionType Type => ActionType.Heal;

        /// <summary>
        /// Executes the Heal action, restoring health to target players or units.
        /// </summary>
        public GameState Execute(
            GameState state,
            GameContext context,
            ActionData action,
            EffectTargets targets,
            int sourceId,
            IGameEvent gameEvent)
        {
            CardInstance? sourceCard = state.Board.GetAllUnits().FirstOrDefault(u => u.InstanceId == sourceId)
               ?? state.SpellStack.FirstOrDefault(s => s.InstanceId == sourceId);

            // Heal player target
            if (targets.TargetPlayer != null)
            {
                var newPlayer = targets.TargetPlayer.WithHealthRestored(action.Amount);
                return state.UpdatePlayer(newPlayer);
            }

            // Heal unit targets
            var workingState = state;
            foreach (var unit in targets.UnitTargets)
            {
                var healedUnit = unit.Heal(action.Amount);
                workingState = workingState.UpdateBoard(workingState.Board.UpdateUnit(healedUnit));

                // Publish UnitHealedEvent for each healed unit
                context.Events.Publish(new UnitHealedEvent(
                    unit.InstanceId,
                    action.Amount,
                    healedUnit.CurrentStats.Health,
                    sourceId,
                    gameEvent.SourcePlayerId));
            }

            return workingState;
        }
    }

    #endregion
}