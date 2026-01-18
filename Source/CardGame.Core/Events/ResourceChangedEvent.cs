using CardGame.Core.Events.Interfaces;

namespace CardGame.Core.Events
{
    #region Resource Events

    /// <summary>
    /// Represents an event that occurs when a player's resource count changes.
    /// </summary>
    public class ResourceChangedEvent : IGameEvent
    {
        /// <summary>
        /// Gets the ID of the player whose resources changed.
        /// </summary>
        public int PlayerId { get; }

        /// <summary>
        /// Gets the resource value before the change.
        /// </summary>
        public int OldValue { get; }

        /// <summary>
        /// Gets the resource value after the change.
        /// </summary>
        public int NewValue { get; }

        /// <summary>
        /// Gets the ID of the player who caused the resource change.
        /// </summary>
        public int SourcePlayerId => PlayerId;

        /// <summary>
        /// Initializes a new instance of the ResourceChangedEvent class.
        /// </summary>
        /// <param name="playerId">The ID of the player whose resources changed.</param>
        /// <param name="oldValue">The previous resource value.</param>
        /// <param name="newValue">The new resource value.</param>
        public ResourceChangedEvent(int playerId, int oldValue, int newValue)
        {
            PlayerId = playerId;
            OldValue = oldValue;
            NewValue = newValue;
        }
    }

    #endregion
}