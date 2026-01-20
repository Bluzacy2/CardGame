using CardGame.Core.Application;
using CardGame.Core.Cards.Component.Actions;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Logic;
using CardGame.Core.Cards.Models;
using CardGame.Core.Events;
using CardGame.Core.Events.Interfaces;
using CardGame.Core.State.Models;
using System;
using System.Collections.Generic;

namespace CardGame.Core.Cards.Components.Actions.Handlers
{
    #region Status Effect Handlers

    /// <summary>
    /// Handles the ApplyStatus action, applying keyword status effects to units.
    /// </summary>
    public class ApplyStatusHandler : IActionHandler
    {
        /// <summary>
        /// Gets the action type this handler processes.
        /// </summary>
        public ActionType Type => ActionType.ApplyStatus;

        /// <summary>
        /// Executes the ApplyStatus action, applying a keyword status effect to target units.
        /// </summary>
        public GameState Execute(
            GameState state,
            GameContext context,
            ActionData action,
            EffectTargets targets,
            int sourceId,
            IGameEvent gameEvent)
        {
            // 1. Validation: Ensure we have a status to apply
            if (!action.StatusKeyword.HasValue) return state;

            var statusKeyword = action.StatusKeyword.Value;
            var workingState = state;

            var unitsToProcess = new List<CardInstance>();
            if (targets.UnitTargets != null && targets.UnitTargets.Any())
            {
                unitsToProcess.AddRange(targets.UnitTargets);
            }
            else if (targets.TargetUnit != null)
            {
                unitsToProcess.Add(targets.TargetUnit);
            }

            foreach (var targetStub in unitsToProcess)
            {

                var liveUnit = workingState.Board.GetAllUnits()
                    .FirstOrDefault(u => u.InstanceId == targetStub.InstanceId);

                if (liveUnit == null) continue;

                var unitWithStatus = liveUnit.AddPermanentBuff(
                    new CardStats(0, 0, 0, new List<Keyword> { statusKeyword }));

                context.Events.Publish(new StatusAppliedEvent(
                    gameEvent.SourcePlayerId,
                    liveUnit.InstanceId,
                    statusKeyword,
                    sourceId));

                workingState = workingState.UpdateBoard(workingState.Board.UpdateUnit(unitWithStatus));
            }

            return workingState;
        }
    }


    #endregion
}