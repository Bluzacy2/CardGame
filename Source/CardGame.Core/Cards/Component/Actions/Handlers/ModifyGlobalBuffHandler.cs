using CardGame.Core.Application;
using CardGame.Core.Cards.Component.Actions;
using CardGame.Core.Cards.Components.Actions;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Logic;
using CardGame.Core.Cards.Models;
using CardGame.Core.Events.Interfaces;
using CardGame.Core.State.Models;
using System.Linq;

namespace CardGame.Core.Cards.Components.Actions.Handlers
{
    public class ModifyGlobalBuffHandler : IActionHandler
    {
        public ActionType Type => ActionType.ModifyGlobalBuff;

        public GameState Execute(GameState state, GameContext context, ActionData action, EffectTargets targets, int sourceId, IGameEvent gameEvent)
        {
          
            if (targets.TargetPlayer != null)
            {
                var statDelta = new CardStats(action.BuffAtk, action.BuffHp, 0);

                var newPlayer = targets.TargetPlayer.WithGlobalBuffModifier(action.BuffAtk, action.BuffHp);
                state = state.UpdatePlayer(newPlayer);

                var unitsOnBoard = state.Board.GetAllUnits().Where(u => u.OwnerPlayerId == newPlayer.PlayerId);
               

                foreach (var unit in unitsOnBoard)
                {
                    var buffedUnit = unit.AddPermanentBuff(statDelta);
                    state = state.UpdateBoard(state.Board.UpdateUnit(buffedUnit));
                }

                return state;
            }
            return state;
        }
    }
}