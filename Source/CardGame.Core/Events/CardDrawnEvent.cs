using CardGame.Core.Events.Interfaces;

namespace CardGame.Core.Events
{
    #region Card Events

    /// <summary>
    /// Represents an event that occurs when a player draws a card.
    /// </summary>
    public class CardDrawnEvent : IGameEvent
    {
        /// <summary>
        /// Initializes a new instance of the CardDrawnEvent class.
        /// </summary>
        /// <param name="playerId">The ID of the player who drew a card.</param>
        public CardDrawnEvent(int playerId) => PlayerId = playerId;

        /// <summary>
        /// Gets the ID of the player who drew a card.
        /// </summary>
        public int PlayerId { get; }

        /// <summary>
        /// Gets the ID of the player who drew the card.
        /// </summary>
        public int SourcePlayerId => PlayerId;
    }

    #endregion
}