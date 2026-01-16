using CardGame.Core.Cards.Models;
using CardGame.Core.Events.Interfaces;

namespace CardGame.Core.Events
{
    #region Card Events

    /// <summary>
    /// Represents an event that occurs when a new card instance is created.
    /// </summary>
    public class CardCreatedEvent : IGameEvent
    {
        /// <summary>
        /// Initializes a new instance of the CardCreatedEvent class.
        /// </summary>
        /// <param name="card">The card instance that was created.</param>
        /// <param name="ownerId">The ID of the player who owns the card.</param>
        public CardCreatedEvent(CardInstance card, int ownerId)
        {
            Card = card;
            OwnerId = ownerId;
        }

        /// <summary>
        /// Gets the card instance that was created.
        /// </summary>
        public CardInstance Card { get; }

        /// <summary>
        /// Gets the ID of the player who owns the card.
        /// </summary>
        public int OwnerId { get; }

        /// <summary>
        /// Gets the ID of the player who owns the card.
        /// </summary>
        public int SourcePlayerId => OwnerId;
    }

    #endregion
}