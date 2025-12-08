using CardGame.Core.Cards.Models;
using CardGame.Core.Events.Interfaces;

namespace CardGame.Core.Events
{
    public class UnitDiedEvent : IGameEvent
    {
        public CardInstance Unit { get; }
        public int OwnerId { get; }
        public int LineIndex { get; }

        public UnitDiedEvent(CardInstance unit, int lineIndex = -1)
        {
            Unit = unit;
            OwnerId = unit.OwnerPlayerId;
            LineIndex = lineIndex;
        }
    }
}