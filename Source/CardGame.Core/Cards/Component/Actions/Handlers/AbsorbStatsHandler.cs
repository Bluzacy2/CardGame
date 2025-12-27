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
            if (targets.TargetUnit == null) return state;

            // Szukamy źródła (jednostki, która absorbuje) - może być na stole LUB w ręce
            CardInstance? me = state.Board.GetAllUnits().FirstOrDefault(u => u.InstanceId == sourceId);
            bool isOnBoard = me != null;

            if (me == null)
            {
                me = state.PlayerA.Hand.FirstOrDefault(c => c.InstanceId == sourceId)
                     ?? state.PlayerB.Hand.FirstOrDefault(c => c.InstanceId == sourceId);
            }

            if (me != null)
            {
                var victimStats = targets.TargetUnit.CurrentStats;
                // Tworzymy buffa z aktualnych statystyk ofiary
                var buff = new CardStats(victimStats.Attack, victimStats.Health, 0);
                var biggerMe = me.AddPermanentBuff(buff);

                Console.WriteLine($"[EFEKT] AbsorbStats: {biggerMe.Definition.Name} pochłania statystyki {targets.TargetUnit.Definition.Name}");

                if (isOnBoard)
                {
                    return state.UpdateBoard(state.Board.UpdateUnit(biggerMe));
                }
                else
                {
                    // Jeśli jednostka absorbująca jest w ręce (np. jakiś specyficzny efekt)
                    var owner = state.GetPlayer(biggerMe.OwnerPlayerId);
                    var newHand = owner.Hand.Select(c => c.InstanceId == sourceId ? biggerMe : c).ToList();
                    return state.UpdatePlayer(owner.With(hand: newHand));
                }
            }

            return state;
        }
    }
}