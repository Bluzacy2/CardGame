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
            while (true)
            {
                var allUnits = workingState.Board.GetAllUnits();
                var unitToKill = allUnits.FirstOrDefault(u => u.CurrentStats.Health <= 0);
                if (unitToKill == null) break;

                int lineIndex = -1;
                for (int i = 0; i < 4; i++)
                {
                    if (workingState.Board.Lines[i].Player1Unit?.InstanceId == unitToKill.InstanceId ||
                        workingState.Board.Lines[i].Player2Unit?.InstanceId == unitToKill.InstanceId)
                    { lineIndex = i; break; }
                }

                var history = eventBus.GetHistory().ToList();
                bool isSacrifice = history.Any(e => e is UnitSacrificedEvent sac && sac.Unit.InstanceId == unitToKill.InstanceId);

                // Sprawdź keywordy chroniące przed śmiercią (tylko jeśli to nie poświęcenie)
                if (!isSacrifice && context.Keywords.TryPreventDeath(ref workingState, unitToKill, context))
                {
                    // Jeśli TryPreventDeath zmieniło stan (np. SoulGuard), kontynuuj pętlę
                    continue;
                }

                var lastDmg = history.OfType<UnitDamagedEvent>()
                    .LastOrDefault(e => e.Unit?.InstanceId == unitToKill.InstanceId && e.Source != null);

                workingState = KillInstantly(workingState, unitToKill, lineIndex, eventBus, lastDmg?.Source?.InstanceId);
            }
            return workingState;
        }

        public GameState KillInstantly(GameState state, CardInstance unit, int lineIndex, EventBus events, int? killerId)
        {
            var board = state.Board;
            for (int i = 0; i < 4; i++)
            {
                if (board.Lines[i].Player1Unit?.InstanceId == unit.InstanceId) board = board.WithUnitPlacedAt(i, 1, null);
                else if (board.Lines[i].Player2Unit?.InstanceId == unit.InstanceId) board = board.WithUnitPlacedAt(i, 2, null);
            }

            // Pobieramy najświeższego właściciela ze stanu przekazanego do metody
            var owner = state.GetPlayer(unit.OwnerPlayerId);
            var newState = state.UpdateBoard(board).UpdatePlayer(owner.WithCardAddedToDiscard(unit));

            events.Publish(new UnitDiedEvent(unit, lineIndex, killerId));
            return newState;
        }
    }
}