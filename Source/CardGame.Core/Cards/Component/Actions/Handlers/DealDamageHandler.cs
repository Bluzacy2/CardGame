using CardGame.Core.Application;
using CardGame.Core.Cards.Component.Actions; 
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Logic;
using CardGame.Core.Cards.Models;
using CardGame.Core.Events.Interfaces;
using CardGame.Core.GameRules.Damage;
using CardGame.Core.State.Models;
using System.Linq;

namespace CardGame.Core.Cards.Components.Actions.Handlers
{
    public class DealDamageHandler : IActionHandler
    {
        public ActionType Type => ActionType.DealDamage;

        // Dodano parametr IGameEvent gameEvent na końcu
        public GameState Execute(GameState state, GameContext context, ActionData action, EffectTargets targets, int sourceId, IGameEvent gameEvent)
        {
            // Logika znalezienia źródła
            CardInstance? sourceCard = state.Board.GetAllUnits().FirstOrDefault(u => u.InstanceId == sourceId);
            if (sourceCard == null)
            {
                sourceCard = state.PlayerA.Hand.FirstOrDefault(u => u.InstanceId == sourceId)
                          ?? state.PlayerB.Hand.FirstOrDefault(u => u.InstanceId == sourceId);
            }

            // A. Obrażenia w gracza
            if (targets.TargetPlayer != null)
            {
                var newPlayer = targets.TargetPlayer.WithDamageTaken(action.Amount);
                // System.Console.WriteLine($"[EFEKT] {action.Amount} dmg w gracza {newPlayer.PlayerId}");
                return state.UpdatePlayer(newPlayer);
            }

            // B. Obrażenia w jednostkę
            foreach (var targetUnit in targets.UnitTargets)
            {
                var dmgContext = new DamageContext(
                    sourceCard,
                    targetUnit,
                    action.Amount,
                    DamageType.Effect);

                int finalDamage = context.DamageCalculator.CalculateFinalDamage(dmgContext);
                var damagedUnit = targetUnit.TakeDamage(finalDamage);

                state = state.UpdateBoard(state.Board.UpdateUnit(damagedUnit));

                // Console.WriteLine($"[EFEKT] {action.Amount} dmg w {damagedUnit.Definition.Name}");
            }


            return state;
        }
    }
}