using System.Collections.Generic;
using CardGame.Core.Cards.Models;
using CardGame.Core.State.Models;
using CardGame.Core.Events;
using CardGame.Core.GameRules.Death.Prevention;

namespace CardGame.Core.GameRules.Death
{
    public class DeathResolver
    {
        private readonly List<IDeathPrevention> _preventions;

        public DeathResolver()
        {
            // Rejestrujemy wszystkie mechaniki ratunkowe
            _preventions = new List<IDeathPrevention>
            {
                new SoulGuardPrevention(),
                new UnkillablePrevention()     
            };
        }

      
        public GameState ResolveDeaths(GameState currentState, EventBus eventBus)
        {
            var workingState = currentState;

            for (int i = 0; i < 4; i++)
            {
                // Musimy pobierać linię z 'workingState' w każdej iteracji, 
                // bo poprzednia śmierć mogła zmienić stan (np. Unkillable cofnął kartę)
                var line = workingState.Board.Lines[i];

                // Sprawdzamy P1
                if (line.Player1Unit != null && line.Player1Unit.CurrentStats.Health <= 0)
                {
                    workingState = HandleDeath(line.Player1Unit, i, workingState, eventBus);
                }

                // Pobieramy linię ponownie, bo HandleDeath mogło ją zmienić
                line = workingState.Board.Lines[i];

                // Sprawdzamy P2
                if (line.Player2Unit != null && line.Player2Unit.CurrentStats.Health <= 0)
                {
                    workingState = HandleDeath(line.Player2Unit, i, workingState, eventBus);
                }
            }

            return workingState;

        }

        private GameState HandleDeath(CardInstance unit, int lineIndex, GameState state, EventBus eventBus)
        {
            // 1. Prewencja (Unkillable) - bez zmian
            foreach (var prevention in _preventions)
            {
                if (prevention.CanPreventDeath(unit, state))
                    return prevention.PreventDeath(unit, state);
            }

            // 2. Prawdziwa Śmierć

            // PUBLIKUJEMY EVENT Z INDEKSEM LINII!
            // (Musisz zaktualizować UnitDiedEvent w GameEvents.cs, żeby przyjmował int lineIndex)
            eventBus.Publish(new UnitDiedEvent(unit, lineIndex));

            var newBoard = RemoveUnitFromBoard(state.Board, unit);
            var owner = state.GetPlayer(unit.OwnerPlayerId);
            var newOwnerState = owner.WithCardAddedToDiscard(unit);

            if (unit.OwnerPlayerId == 1) return state.With(board: newBoard, playerA: newOwnerState);
            else return state.With(board: newBoard, playerB: newOwnerState);
        }
        

        private BoardState RemoveUnitFromBoard(BoardState board, CardInstance unit)
        {
            // Kopia metody z UnkillablePrevention - można to wynieść do wspólnego utils
            var newLines = new List<Line>();
            foreach (var line in board.Lines)
            {
                var l = line;
                if (l.Player1Unit?.InstanceId == unit.InstanceId) l = l.WithUnitPlaced(1, null);
                if (l.Player2Unit?.InstanceId == unit.InstanceId) l = l.WithUnitPlaced(2, null);
                newLines.Add(l);
            }
            return new BoardState(newLines);
        }
    }
}