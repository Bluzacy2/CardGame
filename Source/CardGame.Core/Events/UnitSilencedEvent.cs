using CardGame.Core.Events.Interfaces;

/// <summary>
/// Represents an event that occurs when a unit is silenced, removing its abilities or status effects.
/// </summary>
public class UnitSilencedEvent : IGameEvent
{
    /// <summary>
    /// Gets the ID of the unit that was silenced.
    /// </summary>
    public int TargetUnitId { get; }

    /// <summary>
    /// Gets the optional source ID that caused the silence effect.
    /// </summary>
    public int? SourceId { get; }

    /// <summary>
    /// Gets the ID of the player who caused the silence effect.
    /// </summary>
    public int SourcePlayerId { get; }

    /// <summary>
    /// Initializes a new instance of the UnitSilencedEvent class.
    /// </summary>
    /// <param name="targetUnitId">The ID of the target unit being silenced.</param>
    /// <param name="sourceId">The optional source ID that caused the silence.</param>
    /// <param name="sourcePlayerId">The ID of the player who caused the silence.</param>
    public UnitSilencedEvent(int targetUnitId, int? sourceId, int sourcePlayerId)
    {
        TargetUnitId = targetUnitId;
        SourceId = sourceId;
        SourcePlayerId = sourcePlayerId;
    }
}