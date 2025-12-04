using CardGame.Core.Cards.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CardGame.Core.State.Models
{
    public class BoardState
    {
        public IReadOnlyList<Line> Lines { get; }
        public BoardState(IEnumerable<Line> lines)
        {
            Lines = new List<Line>(lines);
        }

        public static BoardState Empty()
        { 
            var emptyLines = new List<Line>();
            for (int i = 0; i < 4; i++)
            {
                emptyLines.Add(Line.Empty(i));
            }
            return new BoardState(emptyLines);
        }

        public BoardState WithUnitPlacedAt(int lineIndex, int playerId, CardInstance unit)
        {
            var newLines = new List<Line>(Lines);

            var oldLine = newLines[lineIndex];
            var newLine = oldLine.WithUnitPlaced(playerId, unit);

            newLines[lineIndex] = newLine;

            return new BoardState(newLines);

        }

        public List<CardInstance> GetAllUnits()
        {
            var list = new List<CardInstance>();
            foreach (var line in Lines)
            {
                if (line.Player1Unit != null) list.Add(line.Player1Unit);
                if (line.Player2Unit != null) list.Add(line.Player2Unit);
            }
            return list;
        }

        public BoardState UpdateUnit(CardInstance updatedUnit)
        {
            var newLines = new List<Line>();
            bool found = false;

            Console.WriteLine($"[BOARD DEBUG] Próba aktualizacji jednostki ID: {updatedUnit.InstanceId} ({updatedUnit.Definition.Name})");


            foreach (var line in Lines)
            {
                if (line.Player1Unit != null) Console.WriteLine($"  - Linia {line.Index} P1: ID {line.Player1Unit.InstanceId}");
                if (line.Player2Unit != null) Console.WriteLine($"  - Linia {line.Index} P2: ID {line.Player2Unit.InstanceId}");
                // Sprawdzamy, czy ta linia zawiera naszą jednostkę
                if (line.Player1Unit?.InstanceId == updatedUnit.InstanceId)
                {
                    // Podmieniamy jednostkę gracza 1
                    newLines.Add(line.WithUnitPlaced(1, updatedUnit));
                    found = true;
                    Console.WriteLine("  -> ZNALEZIONO u Gracza 1! Aktualizuję.");
                }
                else if (line.Player2Unit?.InstanceId == updatedUnit.InstanceId)
                {
                    // Podmieniamy jednostkę gracza 2
                    newLines.Add(line.WithUnitPlaced(2, updatedUnit));
                    found = true;
                    Console.WriteLine("  -> ZNALEZIONO u Gracza 2! Aktualizuję.");
                }
                else
                {
                    // Bez zmian
                    newLines.Add(line);
                }
            }

            if (!found)
            {
                Console.WriteLine("[BOARD DEBUG] BŁĄD: Nie znaleziono jednostki do aktualizacji! Zwracam stary stan.");
                // Jeśli nie znaleźliśmy (np. jednostka zginęła w reakcji na coś innego), zwracamy stan bez zmian
                return this;
            }

            return new BoardState(newLines);
        }
    }
}
