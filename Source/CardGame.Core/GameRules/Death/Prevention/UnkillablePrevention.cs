using System;
using System.Linq;
using System.Collections.Generic;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Models;
using CardGame.Core.State.Models;

namespace CardGame.Core.GameRules.Death.Prevention
{
    public class UnkillablePrevention : IDeathPrevention
    {
        public bool CanPreventDeath(CardInstance unit, GameState state)
        {
            return unit.CurrentStats.Keywords.Contains(Keyword.Unkillable);
        }

        public GameState PreventDeath(CardInstance unit, GameState currentState)
        {
            // 1. Usuń jednostkę ze stołu
            var newBoard = RemoveUnitFromBoard(currentState.Board, unit);

            // 2. Przygotuj "świeżą" kopię karty (bez obrażeń i buffów)
            var owner = currentState.GetPlayer(unit.OwnerPlayerId);
            var returnedCard = new CardInstance(unit.InstanceId, unit.OwnerPlayerId, unit.Definition);

            // 3. Dodaj ją do ręki właściciela
            var newOwnerState = owner.WithCardAddedToHand(returnedCard);

            Console.WriteLine($"[UNKILLABLE] {unit.Definition.Name} (ID:{unit.InstanceId}) wraca do ręki.");

            return currentState.UpdateBoard(newBoard).UpdatePlayer(newOwnerState);
        }

        private BoardState RemoveUnitFromBoard(BoardState board, CardInstance unit)
        {
            var newLines = board.Lines.Select(line =>
            {
                // Jeśli ID instancji pasuje, ustawiamy null w tym slocie
                var p1 = line.Player1Unit?.InstanceId == unit.InstanceId ? null : line.Player1Unit;
                var p2 = line.Player2Unit?.InstanceId == unit.InstanceId ? null : line.Player2Unit;

                return line.UpdateUnits(p1, p2);
            }).ToList();

            return new BoardState(newLines);
        }
    }
}