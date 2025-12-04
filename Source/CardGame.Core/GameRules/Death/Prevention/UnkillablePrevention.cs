using System.Linq;
using CardGame.Core.Cards.Data; // Tu musi być enum Keyword
using CardGame.Core.Cards.Models;
using CardGame.Core.State.Models;

namespace CardGame.Core.GameRules.Death.Prevention
{
    public class UnkillablePrevention : IDeathPrevention
    {
        public bool CanPreventDeath(CardInstance unit, GameState state)
        {
            // Sprawdzamy, czy karta ma keyword Unkillable
            // Zakładam, że CardStats ma listę Keywords lub CardDefinition ją ma
            // Dostosuj to do swojego modelu danych!
            return unit.Definition.Keywords.Contains(Keyword.Unkillable);
        }

        public GameState PreventDeath(CardInstance unit, GameState currentState)
        {
            // 1. Zdejmij z planszy (ustaw null w linii)
            var newBoard = RemoveUnitFromBoard(currentState.Board, unit);

            // 2. Dodaj do ręki właściciela (zresetowaną lub obecną)
            var owner = currentState.GetPlayer(unit.OwnerPlayerId);

            // Resetujemy statystyki do bazowych przy powrocie do ręki
            var returnedCard = unit.WithStats(unit.Definition.BaseStats);

            var newOwnerState = owner.WithCardAddedToHand(returnedCard);

            // 3. Zwróć zaktualizowany stan
            if (unit.OwnerPlayerId == 1)
                return currentState.With(board: newBoard, playerA: newOwnerState);
            else
                return currentState.With(board: newBoard, playerB: newOwnerState);
        }

        private BoardState RemoveUnitFromBoard(BoardState board, CardInstance unit)
        {
            // Znajdujemy linię i czyścimy slot
            var newLines = board.Lines.Select(line =>
            {
                if (line.Player1Unit?.InstanceId == unit.InstanceId)
                    return line.WithUnitPlaced(1, null);
                if (line.Player2Unit?.InstanceId == unit.InstanceId)
                    return line.WithUnitPlaced(2, null);
                return line;
            }).ToList();

            return new BoardState(newLines);
        }
    }
}