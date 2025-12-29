using CardGame.Core.Application;
using CardGame.Core.Cards.Component.Actions;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Logic;
using CardGame.Core.Cards.Models;
using CardGame.Core.Events;
using CardGame.Core.Events.Interfaces;
using CardGame.Core.GameRules.Damage;
using CardGame.Core.State.Models;
using System.Linq;

namespace CardGame.Core.Cards.Components.Actions.Handlers
{
    public class DealDamageHandler : IActionHandler
    {
        public ActionType Type => ActionType.DealDamage;

        public GameState Execute(GameState state, GameContext context, ActionData action, EffectTargets targets, int sourceId, IGameEvent gameEvent)
        {
            CardInstance? sourceCard = state.Board.GetAllUnits().FirstOrDefault(u => u.InstanceId == sourceId)
               ?? state.PlayerA.Hand.Concat(state.PlayerB.Hand).FirstOrDefault(c => c.InstanceId == sourceId)
               ?? state.SpellStack.FirstOrDefault(s => s.InstanceId == sourceId);

            if (targets.TargetPlayer != null)
            {
                var newPlayer = targets.TargetPlayer.WithDamageTaken(action.Amount);

                context.Events.Publish(new UnitDamagedEvent(null!, action.Amount, sourceCard));
                return state.UpdatePlayer(newPlayer);
            }

            var workingState = state;
            foreach (var targetUnit in targets.UnitTargets)
            {
                var dmgContext = new DamageContext(sourceCard, targetUnit, action.Amount, DamageType.Effect);
                int finalDamage = context.DamageCalculator.CalculateFinalDamage(dmgContext);
                var damagedUnit = targetUnit.TakeDamage(finalDamage);

                workingState = workingState.UpdateBoard(workingState.Board.UpdateUnit(damagedUnit));
                context.Events.Publish(new UnitDamagedEvent(damagedUnit, finalDamage, sourceCard));
            }

            return workingState;
        }
    }
}