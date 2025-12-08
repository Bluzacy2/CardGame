using CardGame.Core.Application;
using CardGame.Core.Cards.Component.Actions;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Logic;
using CardGame.Core.Cards.Models;
using CardGame.Core.Events.Interfaces;
using CardGame.Core.State.Models;
using System;

namespace CardGame.Core.Cards.Components.Actions.Handlers
{
    public class ApplyStatusHandler : IActionHandler
    {
        public ActionType Type => ActionType.ApplyStatus;

        public GameState Execute(GameState state, GameContext context, ActionData action, EffectTargets targets, int sourceId, IGameEvent gameEvent)
        {
            if (targets.TargetUnit != null && Enum.TryParse<Keyword>(action.StringParam, out var k))
            {
                Console.WriteLine($"[EFEKT] Status {k} dla {targets.TargetUnit.Definition.Name}");

                var statusBuff = new CardStats(0, 0, 0, new List<Keyword> { k });

                var unitWithStatus = targets.TargetUnit.AddPermanentBuff(statusBuff);

                return state.UpdateBoard(state.Board.UpdateUnit(unitWithStatus));
              
            }
            return state;
        }
    }
}