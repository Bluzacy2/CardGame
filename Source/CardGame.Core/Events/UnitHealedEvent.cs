using CardGame.Core.Events.Interfaces;

/// <summary>
/// Represents an event that occurs when a unit is healed.
/// </summary>
public class UnitHealedEvent : IGameEvent
{
    /// <summary>
    /// Gets the ID of the unit that was healed.
    /// </summary>
    public int TargetUnitId { get; }

    /// <summary>
    /// Gets the optional source ID that caused the healing.
    /// </summary>
    public int? SourceId { get; }

    /// <summary>
    /// Gets the amount of health restored.
    /// </summary>
    public int Amount { get; }

    /// <summary>
    /// Gets the health value after healing.
    /// </summary>
    public int HealthAfter { get; }

    /// <summary>
    /// Gets the ID of the player who performed the healing.
    /// </summary>
    public int SourcePlayerId { get; }

    /// <summary>
    /// Initializes a new instance of the UnitHealedEvent class.
    /// </summary>
    /// <param name="targetId">The ID of the target unit being healed.</param>
    /// <param name="amount">The amount of health restored.</param>
    /// <param name="healthAfter">The resulting health value after healing.</param>
    /// <param name="sourceId">The optional source ID that caused the healing.</param>
    /// <param name="sourcePlayerId">The ID of the player who performed the healing.</param>
    public UnitHealedEvent(int targetId, int amount, int healthAfter, int? sourceId, int sourcePlayerId)
    {
        TargetUnitId = targetId;
        Amount = amount;
        HealthAfter = healthAfter;
        SourceId = sourceId;
        SourcePlayerId = sourcePlayerId;
    }
}