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
            return unit.Definition.Keywords.Contains(Keyword.Unkillable);
        }

        public GameState PreventDeath(CardInstance unit, GameState currentState)
        {
            var newBoard = RemoveUnitFromBoard(currentState.Board, unit);

            var owner = currentState.GetPlayer(unit.OwnerPlayerId);

            var returnedCard = new CardInstance(unit.InstanceId, unit.OwnerPlayerId, unit.Definition);

            var newOwnerState = owner.WithCardAddedToHand(returnedCard);

            Console.WriteLine($"[UNKILLABLE] {unit.Definition.Name} wraca do ręki zamiast zginąć!");

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