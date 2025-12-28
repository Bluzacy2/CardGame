using CardGame.Core.Application;
using CardGame.Core.Cards.Component.Actions;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Logic;
using CardGame.Core.Events.Interfaces;
using CardGame.Core.State.Models;
using System;
using System.Linq;

namespace CardGame.Core.Cards.Components.Actions.Handlers
{
    public class SilenceHandler : IActionHandler
    {
        public ActionType Type => ActionType.Silence;

        public GameState Execute(GameState state, GameContext context, ActionData action, EffectTargets targets, int sourceId, IGameEvent gameEvent)
        {
            var workingState = state;
            foreach (var unit in targets.UnitTargets)
            {
                // Re-fetch jednostki z planszy, aby mieć pewność, że operujemy na najnowszym stanie
                var latestUnit = workingState.Board.GetAllUnits().FirstOrDefault(u => u.InstanceId == unit.InstanceId);
                if (latestUnit == null) continue;

                Console.WriteLine($"[AKCJA] Silence na: {latestUnit.Definition.Name}");
                var silencedUnit = latestUnit.Silence();
                workingState = workingState.UpdateBoard(workingState.Board.UpdateUnit(silencedUnit));
            }
            return workingState;
        }
    }
}