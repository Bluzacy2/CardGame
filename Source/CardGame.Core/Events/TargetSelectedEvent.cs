using CardGame.Core.Events.Interfaces;

namespace CardGame.Core.Events
{
    #region Gameplay Events

    /// <summary>
    /// Represents an event that occurs when a player selects a target for an effect or ability.
    /// </summary>
    public class TargetSelectedEvent : IGameEvent
    {
        /// <summary>
        /// Gets the ID of the player who made the selection.
        /// </summary>
        public int PlayerId { get; }

        /// <summary>
        /// Gets the ID of the player who initiated the selection (same as PlayerId).
        /// </summary>
        public int SourcePlayerId => PlayerId;

        /// <summary>
        /// Gets the ID of the card that requires target selection.
        /// </summary>
        public int SourceCardId { get; }

        /// <summary>
        /// Gets the ID of the selected target.
        /// </summary>
        public int SelectedTargetId { get; }

        /// <summary>
        /// Initializes a new instance of the TargetSelectedEvent class.
        /// </summary>
        /// <param name="playerId">The ID of the player making the selection.</param>
        /// <param name="sourceCardId">The ID of the card requiring target selection.</param>
        /// <param name="selectedTargetId">The ID of the selected target.</param>
        public TargetSelectedEvent(int playerId, int sourceCardId, int selectedTargetId)
        {
            PlayerId = playerId;
            SourceCardId = sourceCardId;
            SelectedTargetId = selectedTargetId;
        }
    }

    #endregion
}