using CardGame.Core.Application;
using CardGame.Core.Cards.Component.Actions;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Logic;
using CardGame.Core.Cards.Models;
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
            // Nie musimy już parsując stringa - mamy gotowy enum!
            if (targets.TargetUnit != null && action.StatusKeyword.HasValue)
            {
                Keyword k = action.StatusKeyword.Value;
                Console.WriteLine($"[EFEKT] Nadawanie statusu {k} dla {targets.TargetUnit.Definition.Name}");

                var statusBuff = new CardStats(0, 0, 0, new List<Keyword> { k });
                var unitWithStatus = targets.TargetUnit.AddPermanentBuff(statusBuff);

                return state.UpdateBoard(state.Board.UpdateUnit(unitWithStatus));
            }
            return state;
        }
    }
}