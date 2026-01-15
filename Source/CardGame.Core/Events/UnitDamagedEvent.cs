using CardGame.Core.Cards.Models;
using CardGame.Core.Events.Interfaces;

public class UnitDamagedEvent : IGameEvent
{
    public CardInstance? Unit { get; }
    public CardInstance? Source { get; } 
    public int Amount { get; }
    public int? HealthAfter { get; } 

    // UI Helpers
    public int TargetId => Unit?.InstanceId ?? 0;
    public int? SourceId => Source?.InstanceId;
    public int SourcePlayerId => Source?.OwnerPlayerId ?? (Unit?.OwnerPlayerId ?? 0);

    public UnitDamagedEvent(CardInstance? unit, int amount, CardInstance? source, int? healthAfter = null)
    {
        Unit = unit;
        Amount = amount;
        Source = source;
        HealthAfter = healthAfter;
    }
}