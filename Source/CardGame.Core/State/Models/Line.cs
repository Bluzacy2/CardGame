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
    }
}
