using CardGame.Core.Application;
using CardGame.Core.Cards.Component.Actions;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Logic;
using CardGame.Core.Cards.Models;
using CardGame.Core.Events;
using CardGame.Core.Events.Interfaces;
using CardGame.Core.State.Models;
using System;
using System.Linq;

namespace CardGame.Core.Cards.Components.Actions.Handlers
{
    #region Stat Manipulation Handlers

    /// <summary>
    /// Handles the AbsorbStats action, transferring stats from one unit to another.
    /// </summary>
    public class AbsorbStatsHandler : IActionHandler
    {
        /// <summary>
        /// Gets the action type this handler processes.
        /// </summary>
        public ActionType Type => ActionType.AbsorbStats;

        /// <summary>
        /// Executes the AbsorbStats action, transferring stats from a target unit to the source unit.
        /// </summary>
        public GameState Execute(
            GameState state,
            GameContext context,
            ActionData action,
            EffectTargets targets,
            int sourceId,
            IGameEvent gameEvent)
        {
            if (targets.TargetUnit == null)
            {
                return state;
            }

            // Find the source unit (the one doing the absorption)
            CardInstance? me = state.Board.GetAllUnits().FirstOrDefault(u => u.InstanceId == sourceId);
            bool isOnBoard = me != null;

            if (me == null)
            {
                // Check if the source is in a player's hand
                me = state.PlayerA.Hand.FirstOrDefault(c => c.InstanceId == sourceId)
                     ?? state.PlayerB.Hand.FirstOrDefault(c => c.InstanceId == sourceId);
            }

            if (me != null)
            {
                var victimStats = targets.TargetUnit.CurrentStats;
                var buff = new CardStats(victimStats.Attack, victimStats.Health, 0);
                var biggerMe = me.AddPermanentBuff(buff);

                context.Events.Publish(new UnitStatsChangedEvent(
                    biggerMe.InstanceId,             // targetId
                    victimStats.Attack,              // atkDelta
                    victimStats.Health,              // hpDelta
                    biggerMe.CurrentStats.Attack,    // curAtk
                    biggerMe.CurrentStats.Health,    // curHp
                    targets.TargetUnit.InstanceId,   // sourceId (int?)
                    biggerMe.OwnerPlayerId,          // sourcePlayerId (int)
                    false                            // isAura (bool)
                ));

                if (isOnBoard)
                {
                    return state.UpdateBoard(state.Board.UpdateUnit(biggerMe));
                }
                else
                {
                    var owner = state.GetPlayer(biggerMe.OwnerPlayerId);
                    var newHand = owner.Hand.Select(c =>
                        c.InstanceId == sourceId ? biggerMe : c).ToList();
                    return state.UpdatePlayer(owner.With(hand: newHand));
                }
            }

            return state;
        }
    }

    #endregion
}