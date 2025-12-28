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
    public class ApplyStatusHandler : IActionHandler
    {
        public ActionType Type => ActionType.ApplyStatus;

        public GameState Execute(GameState state, GameContext context, ActionData action, EffectTargets targets, int sourceId, IGameEvent gameEvent)
        {
            if (targets.TargetUnit != null && action.StatusKeyword.HasValue)
            {
                Keyword k = action.StatusKeyword.Value;
                Console.WriteLine($"[EFEKT] Nadawanie statusu {k} dla {targets.TargetUnit.Definition.Name}");

                var unitWithStatus = targets.TargetUnit.AddPermanentBuff(new CardStats(0, 0, 0, new List<Keyword> { k }));

                context.Events.Publish(new StatusAppliedEvent(gameEvent.SourcePlayerId, targets.TargetUnit.InstanceId, k));

                return state.UpdateBoard(state.Board.UpdateUnit(unitWithStatus));
            }
            return state;
        }
    }
}