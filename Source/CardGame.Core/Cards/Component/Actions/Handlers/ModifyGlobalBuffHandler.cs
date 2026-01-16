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
    /// Handles the ModifyGlobalBuff action, applying permanent stat bonuses to all units of a player.
    /// </summary>
    public class ModifyGlobalBuffHandler : IActionHandler
    {
        #region Properties

        /// <summary>
        /// Gets the action type this handler processes.
        /// </summary>
        public ActionType Type => ActionType.ModifyGlobalBuff;

        #endregion

        #region Public Methods

        /// <summary>
        /// Executes the ModifyGlobalBuff action, applying permanent stat bonuses to a player's units.
        /// </summary>
        public GameState Execute(
            GameState state,
            GameContext context,
            ActionData action,
            EffectTargets targets,
            int sourceId,
            IGameEvent gameEvent)
        {
            if (targets.TargetPlayer != null)
            {
                var statDelta = new CardStats(action.BuffAtk, action.BuffHp, action.Amount);
                var newPlayer = targets.TargetPlayer.WithGlobalBuffModifier(action.BuffAtk, action.BuffHp);
                state = state.UpdatePlayer(newPlayer);

                var unitsOnBoard = state.Board.GetAllUnits()
                    .Where(u => u.OwnerPlayerId == newPlayer.PlayerId);

                foreach (var unit in unitsOnBoard)
                {
                    var buffedUnit = unit.AddPermanentBuff(statDelta);
                    state = state.UpdateBoard(state.Board.UpdateUnit(buffedUnit));

                    context.Events.Publish(new UnitStatsChangedEvent(
                        buffedUnit.InstanceId,
                        action.BuffAtk,
                        action.BuffHp,
                        buffedUnit.CurrentStats.Attack,
                        buffedUnit.CurrentStats.Health,
                        sourceId,
                        gameEvent.SourcePlayerId,
                        false
                    ));
                }

                return state;
            }

            return state;
        }

        #endregion
    }

    #endregion
}