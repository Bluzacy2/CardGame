using CardGame.Core.Cards.Data;
using CardGame.Core.Events.Interfaces;

/// <summary>
/// Represents an event that occurs when a status effect is applied to a unit.
/// </summary>
public class StatusAppliedEvent : IGameEvent
{
    #region Properties

    /// <summary>
    /// Gets the ID of the unit that received the status effect.
    /// </summary>
    public int TargetUnitId { get; }

    /// <summary>
    /// Gets the type of status keyword that was applied.
    /// </summary>
    public Keyword Status { get; }

    /// <summary>
    /// Gets the ID of the player who applied the status.
    /// </summary>
    public int SourcePlayerId { get; }

    /// <summary>
    /// Gets the optional ID of the source that caused the status application.
    /// </summary>
    public int? SourceId { get; }

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the StatusAppliedEvent class.
    /// </summary>
    /// <param name="sourcePlayerId">The ID of the player applying the status.</param>
    /// <param name="targetUnitId">The ID of the target unit.</param>
    /// <param name="status">The status keyword being applied.</param>
    /// <param name="sourceId">The optional source ID that caused the status.</param>
    public StatusAppliedEvent(int sourcePlayerId, int targetUnitId, Keyword status, int? sourceId = null)
    {
        SourcePlayerId = sourcePlayerId;
        TargetUnitId = targetUnitId;
        Status = status;
        SourceId = sourceId;
    }

    #endregion
}