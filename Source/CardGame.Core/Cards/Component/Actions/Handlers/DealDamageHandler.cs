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
    #region Damage and Combat Handlers

    /// <summary>
    /// Handles the DealDamage action, applying damage to player heroes or units.
    /// </summary>
    public class DealDamageHandler : IActionHandler
    {
        /// <summary>
        /// Gets the action type this handler processes.
        /// </summary>
        public ActionType Type => ActionType.DealDamage;

        /// <summary>
        /// Executes the DealDamage action, applying damage to target players or units with damage calculation.
        /// </summary>
        public GameState Execute(
            GameState state,
            GameContext context,
            ActionData action,
            EffectTargets targets,
            int sourceId,
            IGameEvent gameEvent)
        {
            // Retrieve source object for engine logic
            CardInstance? sourceCard = state.Board.GetAllUnits().FirstOrDefault(u => u.InstanceId == sourceId)
               ?? state.PlayerA.Hand.Concat(state.PlayerB.Hand).FirstOrDefault(c => c.InstanceId == sourceId)
               ?? state.SpellStack.FirstOrDefault(s => s.InstanceId == sourceId);

            // Damage player hero target
            if (targets.TargetPlayer != null)
            {
                var newPlayer = targets.TargetPlayer.WithDamageTaken(action.Amount);
                // Pass null for Unit to indicate Hero damage, but include healthAfter
                context.Events.Publish(new UnitDamagedEvent(
                    null,
                    action.Amount,
                    sourceCard,
                    newPlayer.Health));

                return state.UpdatePlayer(newPlayer);
            }

            // Damage unit targets
            var workingState = state;
            foreach (var targetUnit in targets.UnitTargets)
            {
                var damageContext = new DamageContext(sourceCard, targetUnit, action.Amount, DamageType.Effect);
                int finalDamage = context.DamageCalculator.CalculateFinalDamage(damageContext);
                var damagedUnit = targetUnit.TakeDamage(finalDamage);

                workingState = workingState.UpdateBoard(workingState.Board.UpdateUnit(damagedUnit));

                // Pass the objects AND the UI metadata
                context.Events.Publish(new UnitDamagedEvent(
                    damagedUnit,
                    finalDamage,
                    sourceCard,
                    damagedUnit.CurrentStats.Health));
            }

            return workingState;
        }
    }

    #endregion
}