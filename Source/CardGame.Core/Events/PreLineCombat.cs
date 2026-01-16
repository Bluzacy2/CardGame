using CardGame.Core.Events.Interfaces;

namespace CardGame.Core.Events
{
    #region Combat Events

    /// <summary>
    /// Represents an event that occurs before combat is resolved in a specific board lane.
    /// </summary>
    public class PreLineCombatEvent : IGameEvent
    {
        /// <summary>
        /// Gets the index of the board lane where combat is about to occur.
        /// </summary>
        public int LineIndex { get; }

        /// <summary>
        /// Gets the ID of the player who caused the event (0 indicates a system event).
        /// </summary>
        public int SourcePlayerId => 0;

        /// <summary>
        /// Initializes a new instance of the PreLineCombatEvent class.
        /// </summary>
        /// <param name="lineIndex">The index of the board lane (0-3).</param>
        public PreLineCombatEvent(int lineIndex)
        {
            LineIndex = lineIndex;
        }
    }

    #endregion
}