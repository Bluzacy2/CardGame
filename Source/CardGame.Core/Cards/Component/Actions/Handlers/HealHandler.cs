using CardGame.Core.Application;
using CardGame.Core.Cards.Component.Actions;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Logic;
using CardGame.Core.Cards.Models;
using CardGame.Core.Events;
using CardGame.Core.Events.Interfaces;
using CardGame.Core.State.Models;
using System;

namespace CardGame.Core.Cards.Components.Actions.Handlers
{
    public class HealHandler : IActionHandler
    {
        public ActionType Type => ActionType.Heal;

        public GameState Execute(GameState state, GameContext context, ActionData action, EffectTargets targets, int sourceId, IGameEvent gameEvent)
        {
            CardInstance? sourceCard = state.Board.GetAllUnits().FirstOrDefault(u => u.InstanceId == sourceId)
               ?? state.SpellStack.FirstOrDefault(s => s.InstanceId == sourceId);

            if (targets.TargetPlayer != null)
            {
                var newPlayer = targets.TargetPlayer.WithHealthRestored(action.Amount);
                // We can reuse UnitDamagedEvent with negative amount or create UnitHealedEvent
                // Assuming we stay simple:
                return state.UpdatePlayer(newPlayer);
            }

            var workingState = state;
            foreach (var unit in targets.UnitTargets)
            {
                var healedUnit = unit.Heal(action.Amount);
                workingState = workingState.UpdateBoard(workingState.Board.UpdateUnit(healedUnit));

                // If you created a UnitHealedEvent, publish it here similarly
            }

            return workingState;
        }
    }
}