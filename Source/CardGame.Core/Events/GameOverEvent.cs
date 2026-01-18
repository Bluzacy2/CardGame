using CardGame.Core.Events.Interfaces;

namespace CardGame.Core.Events
{
    #region Game State Events

    /// <summary>
    /// Represents an event that occurs when the game ends.
    /// </summary>
    public class GameOverEvent : IGameEvent
    {
        /// <summary>
        /// Gets the ID of the winning player, or null if the game ended in a draw.
        /// </summary>
        public int? WinnerId { get; }

        /// <summary>
        /// Gets the ID of the player who caused the game to end (the winner, or 0 for draw).
        /// </summary>
        public int SourcePlayerId => WinnerId ?? 0;

        /// <summary>
        /// Initializes a new instance of the GameOverEvent class.
        /// </summary>
        /// <param name="winnerId">The ID of the winning player, or null for a draw.</param>
        public GameOverEvent(int? winnerId) => WinnerId = winnerId;
    }

    #endregion
}