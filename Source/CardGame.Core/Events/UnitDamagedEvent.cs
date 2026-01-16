using CardGame.Core.Cards.Models;
using CardGame.Core.Events.Interfaces;

/// <summary>
/// Represents an event that occurs when a unit takes damage.
/// </summary>
public class UnitDamagedEvent : IGameEvent
{
    /// <summary>
    /// Gets the unit that was damaged.
    /// </summary>
    public CardInstance? Unit { get; }

    /// <summary>
    /// Gets the unit that caused the damage.
    /// </summary>
    public CardInstance? Source { get; }

    /// <summary>
    /// Gets the amount of damage dealt.
    /// </summary>
    public int Amount { get; }

    /// <summary>
    /// Gets the health value after damage was applied.
    /// </summary>
    public int? HealthAfter { get; }

    /// <summary>
    /// Gets the ID of the damaged unit.
    /// </summary>
    public int TargetId => Unit?.InstanceId ?? 0;

    /// <summary>
    /// Gets the optional ID of the source unit that caused the damage.
    /// </summary>
    public int? SourceId => Source?.InstanceId;

    /// <summary>
    /// Gets the ID of the player who caused the damage.
    /// </summary>
    public int SourcePlayerId => Source?.OwnerPlayerId ?? (Unit?.OwnerPlayerId ?? 0);

    /// <summary>
    /// Initializes a new instance of the UnitDamagedEvent class.
    /// </summary>
    /// <param name="unit">The unit instance that was damaged.</param>
    /// <param name="amount">The amount of damage dealt.</param>
    /// <param name="source">The unit that caused the damage.</param>
    /// <param name="healthAfter">The resulting health value after damage.</param>
    public UnitDamagedEvent(CardInstance? unit, int amount, CardInstance? source, int? healthAfter = null)
    {
        Unit = unit;
        Amount = amount;
        Source = source;
        HealthAfter = healthAfter;
    }
}