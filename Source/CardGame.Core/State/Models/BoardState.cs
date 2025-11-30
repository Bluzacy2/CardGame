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
    }
}
