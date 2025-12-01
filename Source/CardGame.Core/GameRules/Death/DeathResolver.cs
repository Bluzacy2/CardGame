using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Collections.Generic;
using CardGame.Core.Cards.Models;
using CardGame.Core.State.Models;

namespace CardGame.Core.GameRules.Death
{
    public class DeathResolver
    {
        // Zwracamy nowy stan planszy po usunięciu pokonanych jednostek
        public BoardState ResolveDeaths(BoardState currentBoard, PlayerState playerA, PlayerState playerB)
        {
            var newLines = new List<Line>();

            foreach (var line in currentBoard.Lines)
            {
                CardInstance? unit1 = line.Player1Unit;
                if (unit1 != null && unit1.CurrentStats.Health <= 0)
                {
                    // Tutaj będzie implementacja taki śmiesznych rzeczy jak Soul Guard (jeżeli czasu starczy)
                    // Trzeba dodać później, że po śmierci jednostki trafia na discardPile'a gracza
                    unit1 = null; // Usuwamy jednostkę gracza 1
                }
                CardInstance? unit2 = line.Player2Unit;
                if (unit2 != null && unit2.CurrentStats.Health <= 0)
                {
                    // Tutaj będzie implementacja taki śmiesznych rzeczy jak Soul Guard (jeżeli czasu starczy)
                    // Trzeba dodać później, że po śmierci jednostki trafia na discardPile'a gracza
                    unit2 = null; // Usuwamy jednostkę gracza 2
                }

                newLines.Add(new Line(line.Index, unit1, unit2));
            }
            return new BoardState(newLines);
        }
    }
}
