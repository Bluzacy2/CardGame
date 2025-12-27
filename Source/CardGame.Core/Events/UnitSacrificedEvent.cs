using CardGame.Core.Cards.Models;
using CardGame.Core.Events.Interfaces;

namespace CardGame.Core.Events
{
    public class UnitSacrificedEvent : IGameEvent
    {
        public CardInstance Unit { get; }
        public int OwnerId { get; }

        // Implementacja interfejsu: Podmiotem jest gracz poświęcający jednostkę
        public int SourcePlayerId => OwnerId;

        public int LineIndex { get; }

        public UnitSacrificedEvent(CardInstance unit, int lineIndex)
        {
            Unit = unit;
            OwnerId = unit.OwnerPlayerId;
            LineIndex = lineIndex;
        }
    }
}