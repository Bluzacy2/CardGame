using CardGame.Core.Events.Interfaces;

namespace CardGame.Core.Events
{
    #region Turn Events

    /// <summary>
    /// Represents an event that occurs when a player's turn starts.
    /// </summary>
    public class TurnStartedEvent : IGameEvent
    {
        /// <summary>
        /// Gets the current turn number.
        /// </summary>
        public int TurnNumer { get; }

        /// <summary>
        /// Gets the ID of the player whose turn is starting.
        /// </summary>
        public int ActivePlayerId { get; }

        /// <summary>
        /// Gets the ID of the player who caused the event (the player whose turn is starting).
        /// </summary>
        public int SourcePlayerId => ActivePlayerId;

        /// <summary>
        /// Initializes a new instance of the TurnStartedEvent class.
        /// </summary>
        /// <param name="turnNumer">The current turn number.</param>
        /// <param name="activePlayerId">The ID of the active player.</param>
        public TurnStartedEvent(int turnNumer, int activePlayerId)
        {
            TurnNumer = turnNumer;
            ActivePlayerId = activePlayerId;
        }
    }

    #endregion
}