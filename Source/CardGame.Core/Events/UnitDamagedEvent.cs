using CardGame.Core.Cards.Models;
using CardGame.Core.Events.Interfaces;

namespace CardGame.Core.Events
{
    public class UnitDamagedEvent : IGameEvent
    {
        public CardInstance Unit { get; }
        public int Amount { get; }
        public CardInstance? Source { get; }
        public int SourcePlayerId => Source?.OwnerPlayerId ?? Unit.OwnerPlayerId;

        public UnitDamagedEvent(CardInstance unit, int amount, CardInstance? source)
        {
            Unit = unit;
            Amount = amount;
            Source = source;
        }
    }
}