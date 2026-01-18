using CardGame.Core.Application;
using CardGame.Core.Cards.Component.Actions;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Logic;
using CardGame.Core.Cards.Models;
using CardGame.Core.Events;
using CardGame.Core.Events.Interfaces;
using CardGame.Core.State.Models;
using System.Linq;

namespace CardGame.Core.Cards.Components.Actions.Handlers
{
    #region Buff and Stat Modification Handlers

    /// <summary>
    /// Handles the BuffStats action, applying permanent attack and health bonuses to units.
    /// </summary>
    public class BuffStatsHandler : IActionHandler
    {
        /// <summary>
        /// Gets the action type this handler processes.
        /// </summary>
        public ActionType Type => ActionType.BuffStats;

        /// <summary>
        /// Executes the BuffStats action, applying permanent stat bonuses to target units.
        /// </summary>
        public GameState Execute(
            GameState state,
            GameContext context,
            ActionData action,
            EffectTargets targets,
            int sourceId,
            IGameEvent gameEvent)
        {
            var workingState = state;

            foreach (var targetUnit in targets.UnitTargets)
            {
                var buffDelta = new CardStats(action.BuffAtk, action.BuffHp, action.Amount);
                var newUnit = targetUnit.AddPermanentBuff(buffDelta);
                var owner = workingState.GetPlayer(newUnit.OwnerPlayerId);

                // Update the unit in hand if it's there, otherwise update on board
                if (owner.Hand.Any(c => c.InstanceId == newUnit.InstanceId))
                {
                    var newHand = owner.Hand.Select(c =>
                        c.InstanceId == newUnit.InstanceId ? newUnit : c).ToList();
                    workingState = workingState.UpdatePlayer(owner.With(hand: newHand));
                }
                else
                {
                    workingState = workingState.UpdateBoard(workingState.Board.UpdateUnit(newUnit));
                }

                context.Events.Publish(new UnitStatsChangedEvent(
                    newUnit.InstanceId,
                    action.BuffAtk,
                    action.BuffHp,
                    newUnit.CurrentStats.Attack,
                    newUnit.CurrentStats.Health,
                    sourceId,
                    gameEvent.SourcePlayerId,
                    false
                ));
            }

            return workingState;
        }
    }

    #endregion
}