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
            var workingState = state;
            foreach (var targetUnit in targets.UnitTargets)
            {
                var buffDelta = new CardStats(action.BuffAtk, action.BuffHp, action.Amount);
                var newUnit = targetUnit.AddPermanentBuff(buffDelta);

                // Pobieramy aktualny stan właściciela
                var owner = workingState.GetPlayer(newUnit.OwnerPlayerId);

                if (owner.Hand.Any(c => c.InstanceId == newUnit.InstanceId))
                {
                    var newHand = owner.Hand.Select(c => c.InstanceId == newUnit.InstanceId ? newUnit : c).ToList();
                    workingState = workingState.UpdatePlayer(owner.With(hand: newHand));
                }
                else
                {
                    workingState = workingState.UpdateBoard(workingState.Board.UpdateUnit(newUnit));
                }
            }
            return workingState;
        }
    }
}