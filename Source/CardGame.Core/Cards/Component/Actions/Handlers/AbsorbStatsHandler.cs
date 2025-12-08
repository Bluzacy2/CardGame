using CardGame.Core.Application;
using CardGame.Core.Cards.Component.Actions;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Logic;
using CardGame.Core.Cards.Models;
using CardGame.Core.Events.Interfaces;
using CardGame.Core.State.Models;
using System;
using System.Linq;

namespace CardGame.Core.Cards.Components.Actions.Handlers
{
    public class AbsorbStatsHandler : IActionHandler
    {
        public ActionType Type => ActionType.AbsorbStats;

        public GameState Execute(GameState state, GameContext context, ActionData action, EffectTargets targets, int sourceId, IGameEvent gameEvent)
        {
            if (targets.TargetUnit != null)
            {
                // Musimy znaleźć SIEBIE (jednostkę, która wywołała efekt) na planszy
                var me = state.Board.GetAllUnits().FirstOrDefault(u => u.InstanceId == sourceId);

                if (me != null)
                {
                    var victimStats = targets.TargetUnit.CurrentStats;
                    var newStats = new CardStats(
                        me.CurrentStats.Attack + victimStats.Attack,
                        me.CurrentStats.Health + victimStats.Health,
                        me.CurrentStats.BloodCost,
                        me.CurrentStats.Keywords);

                    var buff = new CardStats(victimStats.Attack, victimStats.Health, 0);

                    var biggerMe = me.AddPermanentBuff(buff);
                    Console.WriteLine($"[EFEKT] AbsorbStats: {biggerMe.Definition.Name} rośnie!");
                    return state.UpdateBoard(state.Board.UpdateUnit(biggerMe));
                }
            }
            return state;
        }
    }
}