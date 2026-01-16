using CardGame.Core.Events.Interfaces;

namespace CardGame.Core.Events
{
    /// <summary>
    /// Represents an event that occurs when a unit's statistics (attack/health) change.
    /// </summary>
    public class UnitStatsChangedEvent : IGameEvent
    {
        /// <summary>
        /// Gets the ID of the unit whose statistics changed.
        /// </summary>
        public int TargetUnitId { get; }

        /// <summary>
        /// Gets the optional source ID that caused the stat change.
        /// </summary>
        public int? SourceId { get; }

        /// <summary>
        /// Gets the change in attack value (positive for increase, negative for decrease).
        /// </summary>
        public int AttackDelta { get; }

        /// <summary>
        /// Gets the change in health value (positive for increase, negative for decrease).
        /// </summary>
        public int HealthDelta { get; }

        /// <summary>
        /// Gets the current attack value after the change.
        /// </summary>
        public int CurrentAttack { get; }

        /// <summary>
        /// Gets the current health value after the change.
        /// </summary>
        public int CurrentHealth { get; }

        /// <summary>
        /// Gets a value indicating whether the change was caused by an aura effect.
        /// </summary>
        public bool IsAura { get; }

        /// <summary>
        /// Gets the ID of the player who caused the stat change.
        /// </summary>
        public int SourcePlayerId { get; }

        /// <summary>
        /// Initializes a new instance of the UnitStatsChangedEvent class.
        /// </summary>
        /// <param name="targetId">The ID of the target unit.</param>
        /// <param name="atkDelta">The change in attack value.</param>
        /// <param name="hpDelta">The change in health value.</param>
        /// <param name="curAtk">The current attack value after change.</param>
        /// <param name="curHp">The current health value after change.</param>
        /// <param name="sourceId">The optional source ID that caused the change.</param>
        /// <param name="sourcePlayerId">The ID of the player who caused the change.</param>
        /// <param name="isAura">Whether the change was caused by an aura effect.</param>
        public UnitStatsChangedEvent(
            int targetId,
            int atkDelta,
            int hpDelta,
            int curAtk,
            int curHp,
            int? sourceId,
            int sourcePlayerId,
            bool isAura = false)
        {
            TargetUnitId = targetId;
            SourceId = sourceId;
            AttackDelta = atkDelta;
            HealthDelta = hpDelta;
            CurrentAttack = curAtk;
            CurrentHealth = curHp;
            IsAura = isAura;
            SourcePlayerId = sourcePlayerId;
        }
    }
}