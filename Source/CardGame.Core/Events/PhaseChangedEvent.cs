using CardGame.Core.Events.Interfaces;
using CardGame.Core.State.Enums;

namespace CardGame.Core.Events
{
    #region Game State Events

    /// <summary>
    /// Represents an event that occurs when the game phase changes.
    /// </summary>
    public class PhaseChangedEvent : IGameEvent
    {
        /// <summary>
        /// Gets the new game phase.
        /// </summary>
        public GamePhase NewPhase { get; }

        /// <summary>
        /// Gets the ID of the active player during the phase change.
        /// </summary>
        public int ActivePlayerId { get; }

        /// <summary>
        /// Gets the ID of the player who caused the phase change.
        /// </summary>
        public int SourcePlayerId => ActivePlayerId;

        /// <summary>
        /// Initializes a new instance of the PhaseChangedEvent class.
        /// </summary>
        /// <param name="newPhase">The new game phase.</param>
        /// <param name="activePlayerId">The ID of the active player.</param>
        public PhaseChangedEvent(GamePhase newPhase, int activePlayerId)
        {
            NewPhase = newPhase;
            ActivePlayerId = activePlayerId;
        }
    }

    #endregion
}