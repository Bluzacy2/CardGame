using CardGame.Core.Application;
using CardGame.Core.Cards.Component.Actions;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Logic;
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
            if (targets.TargetPlayer != null)
            {
                var newPlayer = targets.TargetPlayer.WithHealthRestored(action.Amount);
                Console.WriteLine($"[EFEKT] Uleczono gracza {newPlayer.PlayerId} o {action.Amount}");
                return state.UpdatePlayer(newPlayer);
            }

            foreach (var unit in targets.UnitTargets)
            {
                // Używamy nowej metody Heal w CardInstance (która operuje na DamageTaken)
                var healedUnit = unit.Heal(action.Amount);

                state = state.UpdateBoard(state.Board.UpdateUnit(healedUnit));

                Console.WriteLine($"[EFEKT] Uleczono jednostkę {unit.Definition.Name} o {action.Amount}. HP: {healedUnit.CurrentStats.Health}/{healedUnit.MaxHealth}");
            }

            return state;
        }
    }
}