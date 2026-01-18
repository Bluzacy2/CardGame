using CardGame.Core.Events.Interfaces;

namespace CardGame.Core.Events
{
    #region Card Events

    /// <summary>
    /// Represents the game zones where a card can be located.
    /// </summary>
    public enum CardZone { Deck, Hand, Board, Graveyard, Stack }

    /// <summary>
    /// Represents an event that occurs when a card moves between game zones.
    /// </summary>
    public class CardMovedEvent : IGameEvent
    {
        /// <summary>
        /// Initializes a new instance of the CardMovedEvent class.
        /// </summary>
        /// <param name="cardId">The instance ID of the card that moved.</param>
        /// <param name="ownerId">The ID of the player who owns the card.</param>
        /// <param name="from">The zone the card moved from.</param>
        /// <param name="to">The zone the card moved to.</param>
        /// <param name="toIndex">The optional index position within the destination zone.</param>
        public CardMovedEvent(int cardId, int ownerId, CardZone from, CardZone to, int toIndex = -1)
        {
            CardId = cardId;
            OwnerId = ownerId;
            From = from;
            To = to;
            ToIndex = toIndex;
        }

        /// <summary>
        /// Gets the instance ID of the card that moved.
        /// </summary>
        public int CardId { get; }

        /// <summary>
        /// Gets the ID of the player who owns the card.
        /// </summary>
        public int OwnerId { get; }

        /// <summary>
        /// Gets the zone the card moved from.
        /// </summary>
        public CardZone From { get; }

        /// <summary>
        /// Gets the zone the card moved to.
        /// </summary>
        public CardZone To { get; }

        /// <summary>
        /// Gets the optional index position within the destination zone.
        /// </summary>
        public int ToIndex { get; }

        /// <summary>
        /// Gets the ID of the player who owns the card.
        /// </summary>
        public int SourcePlayerId => OwnerId;
    }

    #endregion
}