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
                new UnkillablePrevention()
                // Tu w przyszłości dodasz: new SoulGuardPrevention()
            };
        }

        // ZMIANA: Zwracamy GameState, bo śmierć zmienia też gracza (Cmentarz/Ręka)
        public GameState ResolveDeaths(GameState currentState, EventBus eventBus)
        {
            var workingState = currentState;

            // Pobieramy listę wszystkich jednostek na planszy
            // Musimy zrobić to na początku, bo będziemy modyfikować planszę w pętli
            // i nie chcemy, żeby indeksy nam uciekły.
            var unitsToCheck = GetAllUnits(workingState.Board);

            foreach (var unit in unitsToCheck)
            {
                // Sprawdzamy czy jednostka powinna zginąć
                if (unit.CurrentStats.Health <= 0)
                {
                    workingState = HandleSingleUnitDeath(unit, workingState, eventBus);
                }
            }

            return workingState;
        }

        private GameState HandleSingleUnitDeath(CardInstance unit, GameState state, EventBus eventBus)
        {
            // 1. Sprawdź Prewencję (Unkillable, Soul Guard)
            foreach (var prevention in _preventions)
            {
                if (prevention.CanPreventDeath(unit, state))
                {
                    // Jeśli zadziałała prewencja, ona zwraca nowy stan i kończymy temat dla tej jednostki
                    return prevention.PreventDeath(unit, state);
                }
            }
            eventBus.Publish(new UnitDiedEvent(unit));

            // 2. Jeśli brak prewencji -> Prawdziwa Śmierć

            // A. Usuń z planszy
            var newBoard = RemoveUnitFromBoard(state.Board, unit);

            // B. Dodaj do Cmentarza (DiscardPile) właściciela
            var owner = state.GetPlayer(unit.OwnerPlayerId);

            // Musisz dodać metodę WithCardAddedToDiscardPile do PlayerState!
            // Zakładam, że działa analogicznie do WithCardAddedToHand
            var newOwnerState = owner.WithCardAddedToDiscard(unit);

            // C. Zwróć nowy stan
            if (unit.OwnerPlayerId == 1)
                return state.With(board: newBoard, playerA: newOwnerState);
            else
                return state.With(board: newBoard, playerB: newOwnerState);
        }

        // Metody pomocnicze
        private List<CardInstance> GetAllUnits(BoardState board)
        {
            var list = new List<CardInstance>();
            foreach (var line in board.Lines)
            {
                if (line.Player1Unit != null) list.Add(line.Player1Unit);
                if (line.Player2Unit != null) list.Add(line.Player2Unit);
            }
            return list;
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