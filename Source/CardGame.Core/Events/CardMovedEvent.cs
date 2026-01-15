using CardGame.Core.Events.Interfaces;

namespace CardGame.Core.Events
{
    public enum CardZone { Deck, Hand, Board, Graveyard, Stack }

    public class CardMovedEvent : IGameEvent
    {
        public int CardId { get; }
        public int OwnerId { get; }
        public CardZone From { get; }
        public CardZone To { get; }
        public int ToIndex { get; }

        public int SourcePlayerId => OwnerId;

        public CardMovedEvent(int cardId, int ownerId, CardZone from, CardZone to, int toIndex = -1)
        {
            CardId = cardId;
            OwnerId = ownerId;
            From = from;
            To = to;
            ToIndex = toIndex;
        }
    }
}