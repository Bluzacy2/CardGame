using CardGame.Core.Cards.Models;
using CardGame.Core.Events.Interfaces;

namespace CardGame.Core.Events
{
    #region Unit Events

    /// <summary>
    /// Represents an event that occurs when a unit dies.
    /// </summary>
    public class UnitDiedEvent : IGameEvent
    {
        /// <summary>
        /// Gets the unit that died.
        /// </summary>
        public CardInstance Unit { get; }

        /// <summary>
        /// Gets the ID of the player who owned the unit.
        /// </summary>
        public int OwnerId { get; }

        /// <summary>
        /// Gets the ID of the player who caused the death (same as owner for self-destruction).
        /// </summary>
        public int SourcePlayerId => OwnerId;

        /// <summary>
        /// Gets the index of the board lane where the unit was located when it died.
        /// </summary>
        public int LineIndex { get; }

        /// <summary>
        /// Gets the optional instance ID of the unit that killed this unit.
        /// </summary>
        public int? KillerInstanceId { get; }

        /// <summary>
        /// Initializes a new instance of the UnitDiedEvent class.
        /// </summary>
        /// <param name="unit">The unit instance that died.</param>
        /// <param name="lineIndex">The board lane index where the unit was located.</param>
        /// <param name="killerId">The optional instance ID of the killer unit.</param>
        public UnitDiedEvent(CardInstance unit, int lineIndex = -1, int? killerId = null)
        {
            Unit = unit;
            OwnerId = unit.OwnerPlayerId;
            LineIndex = lineIndex;
            KillerInstanceId = killerId;
        }
    }

    #endregion
}