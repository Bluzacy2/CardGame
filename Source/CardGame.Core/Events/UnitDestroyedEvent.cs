using CardGame.Core.Cards.Models;
using CardGame.Core.Events.Interfaces;

namespace CardGame.Core.Events
{
    public class UnitDestroyedEvent : IGameEvent
    {
        public CardInstance Unit { get; }
        public int OwnerId { get; }

        // Implementacja interfejsu
        public int SourcePlayerId => OwnerId;

        public UnitDestroyedEvent(CardInstance unit, int ownerId)
        {
            Unit = unit;
            OwnerId = ownerId;
        }
    }
}