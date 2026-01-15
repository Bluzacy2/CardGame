using CardGame.Core.Events.Interfaces;

public class UnitSilencedEvent : IGameEvent
{
    public int TargetUnitId { get; }
    public int? SourceId { get; }
    public int SourcePlayerId { get; }

    public UnitSilencedEvent(int targetUnitId, int? sourceId, int sourcePlayerId)
    {
        TargetUnitId = targetUnitId;
        SourceId = sourceId;
        SourcePlayerId = sourcePlayerId;
    }
}