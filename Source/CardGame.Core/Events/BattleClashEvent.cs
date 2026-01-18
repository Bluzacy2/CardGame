using CardGame.Core.Events.Interfaces;

namespace CardGame.Core.Events
{
    #region Combat Events

    /// <summary>
    /// Represents an event that occurs during combat when units clash.
    /// </summary>
    public class BattleClashEvent : IGameEvent
    {
        /// <summary>
        /// Initializes a new instance of the BattleClashEvent class.
        /// </summary>
        /// <param name="attackerId">The ID of the attacking unit.</param>
        /// <param name="defenderId">The optional ID of the defending unit.</param>
        public BattleClashEvent(int attackerId, int? defenderId)
        {
            AttackerId = attackerId;
            DefenderId = defenderId;
        }

        /// <summary>
        /// Gets the ID of the attacking unit.
        /// </summary>
        public int AttackerId { get; }

        /// <summary>
        /// Gets the optional ID of the defending unit.
        /// </summary>
        public int? DefenderId { get; }

        /// <summary>
        /// Gets the ID of the player who caused the event (0 indicates a system event).
        /// </summary>
        public int SourcePlayerId => 0;
    }

    #endregion
}