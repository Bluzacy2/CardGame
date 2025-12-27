using CardGame.Core.Cards.Models;

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

        public static Line Empty(int index) => new Line(index, null, null);

        public bool IsSlotEmpty(int playerId) => playerId == 1 ? Player1Unit == null : Player2Unit == null;

        public Line WithUnitPlaced(int playerId, CardInstance? unit)
        {
            return playerId == 1 ? new Line(Index, unit, Player2Unit) : new Line(Index, Player1Unit, unit);
        }

        public Line UpdateUnits(CardInstance? newP1Unit, CardInstance? newP2Unit) => new Line(Index, newP1Unit, newP2Unit);
    }
}