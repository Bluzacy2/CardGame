using CardGame.Core.Application;
using CardGame.Core.Cards.Component.Actions;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Logic;
using CardGame.Core.Cards.Models;
using CardGame.Core.Events.Interfaces;
using CardGame.Core.State.Models;
using System.Linq;

namespace CardGame.Core.Cards.Components.Actions.Handlers
{
    public class BuffStatsHandler : IActionHandler
    {
        public ActionType Type => ActionType.BuffStats;

        public GameState Execute(GameState state, GameContext context, ActionData action, EffectTargets targets, int sourceId, IGameEvent gameEvent)
        {
            foreach (var targetUnit in targets.UnitTargets)
            {
                var buffDelta = new CardStats(action.BuffAtk, action.BuffHp, action.Amount); // Amount jako CostModifier

               
                var newUnit = targetUnit.AddPermanentBuff(buffDelta);

                var owner = state.GetPlayer(newUnit.OwnerPlayerId);
                if (owner.Hand.Any(c => c.InstanceId == newUnit.InstanceId))
                {
                    var p = owner.WithCardRemovedFromHand(targetUnit).WithCardAddedToHand(newUnit);
                    state = state.UpdatePlayer(p);
                }
                else
                {
                    state = state.UpdateBoard(state.Board.UpdateUnit(newUnit));
                }
            }
            return state;
        }
    }
}
