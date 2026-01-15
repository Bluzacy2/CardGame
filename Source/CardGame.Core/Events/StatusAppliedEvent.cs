using CardGame.Core.Cards.Data;
using CardGame.Core.Events.Interfaces;

public class StatusAppliedEvent : IGameEvent
{
    public int TargetUnitId { get; }
    public Keyword Status { get; }
    public int SourcePlayerId { get; }

    // UI Helpers
    public int? SourceId { get; } 

    public StatusAppliedEvent(int sourcePlayerId, int targetUnitId, Keyword status, int? sourceId = null)
    {
        SourcePlayerId = sourcePlayerId;
        TargetUnitId = targetUnitId;
        Status = status;
        SourceId = sourceId;
    }
}