using CardGame.Core.Cards.Models;
using CardGame.Core.Events.Interfaces;

public class UnitDestroyedEvent : IGameEvent
{
    public CardInstance Unit { get; }
    public int OwnerId { get; }
    public UnitDestroyedEvent(CardInstance unit, int ownerId)
    {
        Unit = unit;
        OwnerId = ownerId;
    }
}
