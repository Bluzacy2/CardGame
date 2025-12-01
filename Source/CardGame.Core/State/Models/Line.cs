using CardGame.Core.Cards.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CardGame.Core.State.Models
{
    public class Line
    {
        public int Index { get; }
        public CardInstance? Player1Unit { get; }
        public CardInstance? Player2Unit { get; }
        public Line(int index, CardInstance? player1Unit, CardInstance? player2Unit)
        {
            Index = index;
            Player1Unit = player1Unit;
            Player2Unit = player2Unit;
        }

        public static Line Empty(int index)
        {
            return new Line(index, null, null);
        }

        public bool IsSlotEmpty(int playerId)
        {
            return playerId == 1 ? Player1Unit == null : Player2Unit == null;
        }

        public Line WithUnitPlaced(int playerId, CardInstance unit)
        {
            if (playerId == 1)
            {
                return new Line(Index, unit, Player2Unit); // Gracz 1 stawia jednostkę; Gracz 2 pozostaje bez zmian
            }
            else
            {
                return new Line(Index, Player1Unit, unit); // Gracz 2 stawia jednostkę; Gracz 1 pozostaje bez zmian
            }
        }

        // Dodatek dla Combat Phase'a: Aktualizowanie obu jednostek jednocześnie. - B.
        public Line UpdateUnits(CardInstance ? newP1Unit, CardInstance ? newP2Unit)
        {
            return new Line(Index, newP1Unit, newP2Unit);
        }
    }
}
