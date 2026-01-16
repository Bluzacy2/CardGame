using CardGame.Core.Cards.Models;
using CardGame.Core.Events.Interfaces;

namespace CardGame.Core.Events
{
    #region Unit Events

    /// <summary>
    /// Represents an event that occurs when a unit is sacrificed (destroyed by its owner for an effect).
    /// </summary>
    public class UnitSacrificedEvent : IGameEvent
    {
        /// <summary>
        /// Gets the unit that was sacrificed.
        /// </summary>
        public CardInstance Unit { get; }

        /// <summary>
        /// Gets the ID of the player who owns (sacrificed) the unit.
        /// </summary>
        public int OwnerId { get; }

        /// <summary>
        /// Gets the ID of the player who performed the sacrifice (same as OwnerId).
        /// </summary>
        public int SourcePlayerId => OwnerId;

        /// <summary>
        /// Gets the index of the board lane where the unit was sacrificed.
        /// </summary>
        public int LineIndex { get; }

        /// <summary>
        /// Initializes a new instance of the UnitSacrificedEvent class.
        /// </summary>
        /// <param name="unit">The unit instance being sacrificed.</param>
        /// <param name="lineIndex">The board lane index where the unit was located.</param>
        public UnitSacrificedEvent(CardInstance unit, int lineIndex)
        {
            Unit = unit;
            OwnerId = unit.OwnerPlayerId;
            LineIndex = lineIndex;
        }
    }

    #endregion
}