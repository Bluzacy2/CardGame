using CardGame.Core.Cards.Models;
using System;
using System.Collections.Generic;
using System.Linq;

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

        public BoardState WithUnitPlacedAt(int lineIndex, int playerId, CardInstance? unit)
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
            if (updatedUnit == null) return this;

            var newLines = new List<Line>();
            bool found = false;

            foreach (var line in Lines)
            {
                if (line.Player1Unit?.InstanceId == updatedUnit.InstanceId)
                {
                    newLines.Add(line.WithUnitPlaced(1, updatedUnit));
                    found = true;
                }
                else if (line.Player2Unit?.InstanceId == updatedUnit.InstanceId)
                {
                    newLines.Add(line.WithUnitPlaced(2, updatedUnit));
                    found = true;
                }
                else
                {
                    newLines.Add(line);
                }
            }

            return found ? new BoardState(newLines) : this;
        }
    }
}