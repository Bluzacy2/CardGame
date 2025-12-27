using System.Collections.Generic;
using CardGame.Core.Cards.Models;
using CardGame.Core.State.Models;
using CardGame.Core.Events;
using CardGame.Core.GameRules.Death.Prevention;
using System.Linq;
using System;

namespace CardGame.Core.GameRules.Death
{
    public class DeathResolver
    {
        private readonly List<IDeathPrevention> _preventions;

        public DeathResolver()
        {
            _preventions = new List<IDeathPrevention> { new SoulGuardPrevention(), new UnkillablePrevention() };
        }

        public GameState ResolveDeaths(GameState state, EventBus eventBus)
        {
            var workingState = state;
            var history = eventBus.GetHistory().ToList();

            while (true)
            {
                var allUnits = workingState.Board.GetAllUnits();
                // Szukamy jednostki, która ma 0 lub mniej życia
                var unitToKill = allUnits.FirstOrDefault(u => u.CurrentStats.Health <= 0);

                if (unitToKill == null) break;

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

                bool isSacrifice = history.Any(e => e is UnitSacrificedEvent use && use.Unit.InstanceId == unitToKill.InstanceId);

                // Próbujemy obsłużyć śmierć (może zostać zapobiegnięta)
                workingState = HandleDeath(unitToKill, lineIndex, workingState, eventBus, isSacrifice);
            }

            return workingState;
        }

        public GameState KillInstantly(GameState state, CardInstance unit, int lineIndex, EventBus events, bool isSacrifice)
        {
            Console.WriteLine($"[DEATH] {unit.Definition.Name} (ID:{unit.InstanceId}) ginie.");
            events.Publish(new UnitDiedEvent(unit, lineIndex, isSacrifice));

            var newBoard = RemoveUnitFromBoard(state.Board, unit);
            var owner = state.GetPlayer(unit.OwnerPlayerId);

            return state.UpdateBoard(newBoard).UpdatePlayer(owner.WithCardAddedToDiscard(unit));
        }

        private GameState HandleDeath(CardInstance unit, int lineIndex, GameState state, EventBus eventBus, bool isSacrifice)
        {
            if (!isSacrifice)
            {
                foreach (var p in _preventions)
                {
                    if (p.CanPreventDeath(unit, state))
                    {
                        return p.PreventDeath(unit, state);
                    }
                }
            }
            return KillInstantly(state, unit, lineIndex, eventBus, isSacrifice);
        }

        private BoardState RemoveUnitFromBoard(BoardState board, CardInstance unit)
        {
            var newLines = board.Lines.Select(l => {
                var p1 = l.Player1Unit?.InstanceId == unit.InstanceId ? null : l.Player1Unit;
                var p2 = l.Player2Unit?.InstanceId == unit.InstanceId ? null : l.Player2Unit;
                return l.UpdateUnits(p1, p2);
            }).ToList();
            return new BoardState(newLines);
        }
    }
}