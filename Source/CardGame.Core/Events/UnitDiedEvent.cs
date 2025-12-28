using CardGame.Core.Cards.Models;
using CardGame.Core.Events.Interfaces;

namespace CardGame.Core.Events
{
    public class UnitDiedEvent : IGameEvent
    {
        public CardInstance Unit { get; }
        public int OwnerId { get; }
        public int SourcePlayerId => OwnerId;
        public int LineIndex { get; }
        public int? KillerInstanceId { get; } // NOWE

        public UnitDiedEvent(CardInstance unit, int lineIndex = -1, int? killerId = null)
        {
            Unit = unit;
            OwnerId = unit.OwnerPlayerId;
            LineIndex = lineIndex;
            KillerInstanceId = killerId;
        }
    }
}