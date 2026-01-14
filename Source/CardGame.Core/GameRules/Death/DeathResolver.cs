using System;
using System.Linq;
using CardGame.Core.Cards.Models;
using CardGame.Core.State.Models;
using CardGame.Core.Events;
using CardGame.Core.Application;

namespace CardGame.Core.GameRules.Death
{
    public class DeathResolver
    {
        public GameState ResolveDeaths(GameState state, EventBus eventBus, GameContext context)
        {
            var workingState = state;
            bool changed = true;

            while (changed)
            {
                changed = false;
                var allUnits = workingState.Board.GetAllUnits();

              
                var unitToKill = allUnits.FirstOrDefault(u => u.CurrentStats.Health <= 0);

                if (unitToKill != null)
                {
               
                    int lineIndex = -1;
                    for (int i = 0; i < 4; i++)
                    {
                        if (workingState.Board.Lines[i].Player1Unit?.InstanceId == unitToKill.InstanceId ||
                            workingState.Board.Lines[i].Player2Unit?.InstanceId == unitToKill.InstanceId)
                        {
                            lineIndex = i;
                            break;
                        }
                    }

                    var history = eventBus.GetHistory().ToList();

                  
                    bool isSacrifice = history.OfType<UnitSacrificedEvent>()
                        .Any(e => e.Unit.InstanceId == unitToKill.InstanceId);

                 
                    if (context.Keywords.TryPreventDeath(ref workingState, unitToKill, context, isSacrifice))
                    {
                    
                        changed = true;
                        continue;
                    }

                 
                    var lastDmg = history.OfType<UnitDamagedEvent>()
                        .LastOrDefault(e => e.Unit?.InstanceId == unitToKill.InstanceId && e.Source != null);

                 
                    workingState = KillInstantly(workingState, unitToKill, lineIndex, eventBus, lastDmg?.Source?.InstanceId);
                    changed = true;
                }
            }
            return workingState;
        }

        public GameState KillInstantly(GameState state, CardInstance unit, int lineIndex, EventBus events, int? killerId)
        {
            var board = state.Board;
        
            for (int i = 0; i < 4; i++)
            {
                if (board.Lines[i].Player1Unit?.InstanceId == unit.InstanceId)
                    board = board.WithUnitPlacedAt(i, 1, null);
                else if (board.Lines[i].Player2Unit?.InstanceId == unit.InstanceId)
                    board = board.WithUnitPlacedAt(i, 2, null);
            }

        
            var owner = state.GetPlayer(unit.OwnerPlayerId);

          
            var newState = state.UpdateBoard(board).UpdatePlayer(owner.WithCardAddedToDiscard(unit));

         
            events.Publish(new UnitDiedEvent(unit, lineIndex, killerId));

            return newState;
        }
    }
}