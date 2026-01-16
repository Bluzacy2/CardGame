using CardGame.Core.Cards.Models;
using CardGame.Core.Events.Interfaces;

namespace CardGame.Core.Events
{
    #region Unit Events

    /// <summary>
    /// Represents an event that occurs when a unit is destroyed by a game effect.
    /// </summary>
    public class UnitDestroyedEvent : IGameEvent
    {
        /// <summary>
        /// Gets the unit that was destroyed.
        /// </summary>
        public CardInstance Unit { get; }

        /// <summary>
        /// Gets the ID of the player who owned the unit.
        /// </summary>
        public int OwnerId { get; }

        /// <summary>
        /// Gets the ID of the player who caused the destruction.
        /// </summary>
        public int SourcePlayerId => OwnerId;

        /// <summary>
        /// Initializes a new instance of the UnitDestroyedEvent class.
        /// </summary>
        /// <param name="unit">The unit instance that was destroyed.</param>
        /// <param name="ownerId">The ID of the player who owned the unit.</param>
        public UnitDestroyedEvent(CardInstance unit, int ownerId)
        {
            Unit = unit;
            OwnerId = ownerId;
        }
    }

    #endregion
}