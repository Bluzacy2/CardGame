using CardGame.Core.Cards.Models;
using CardGame.Core.Events.Interfaces;

public class CardCreatedEvent : IGameEvent
{
    public CardInstance Card { get; }
    public int OwnerId { get; }
    public int SourcePlayerId => OwnerId;

    public CardCreatedEvent(CardInstance card, int ownerId)
    {
        Card = card;
        OwnerId = ownerId;
    }
}