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
            if (targets.TargetUnit != null && action.StatusKeyword.HasValue)
            {
                Keyword statusKeyword = action.StatusKeyword.Value;
                var unitWithStatus = targets.TargetUnit.AddPermanentBuff(
                    new CardStats(0, 0, 0, new List<Keyword> { statusKeyword }));

                // Pass original parameters + optional sourceId for UI
                context.Events.Publish(new StatusAppliedEvent(
                    gameEvent.SourcePlayerId,
                    targets.TargetUnit.InstanceId,
                    statusKeyword,
                    sourceId));

                return state.UpdateBoard(state.Board.UpdateUnit(unitWithStatus));
            }

            return state;
        }
    }

    #endregion
}