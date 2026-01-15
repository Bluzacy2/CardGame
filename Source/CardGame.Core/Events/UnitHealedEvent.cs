using CardGame.Core.Events.Interfaces;

public class UnitHealedEvent : IGameEvent
{
    public int TargetUnitId { get; }
    public int? SourceId { get; }
    public int Amount { get; }
    public int HealthAfter { get; }
    public int SourcePlayerId { get; }

    public UnitHealedEvent(int targetId, int amount, int healthAfter, int? sourceId, int sourcePlayerId)
    {
        TargetUnitId = targetId;
        Amount = amount;
        HealthAfter = healthAfter;
        SourceId = sourceId;
        SourcePlayerId = sourcePlayerId;
    }
}