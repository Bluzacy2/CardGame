using CardGame.Core.Events.Interfaces;

namespace CardGame.Core.Events
{
    #region Gameplay Events

    /// <summary>
    /// Represents an event that occurs when a card's trigger condition is activated.
    /// </summary>
    public class TriggerActivatedEvent : IGameEvent
    {
        /// <summary>
        /// Gets the ID of the card that triggered the event.
        /// </summary>
        public int SourceCardId { get; }

        /// <summary>
        /// Gets the ID of the player who owns the triggering card.
        /// </summary>
        public int SourcePlayerId { get; }

        /// <summary>
        /// Initializes a new instance of the TriggerActivatedEvent class.
        /// </summary>
        /// <param name="sourceCardId">The ID of the card that triggered the event.</param>
        /// <param name="sourcePlayerId">The ID of the player who owns the card.</param>
        public TriggerActivatedEvent(int sourceCardId, int sourcePlayerId)
        {
            SourceCardId = sourceCardId;
            SourcePlayerId = sourcePlayerId;
        }
    }

    #endregion
}