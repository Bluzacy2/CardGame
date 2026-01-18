using CardGame.Core.Events.Interfaces;

/// <summary>
/// Represents an event that occurs when a player's deck is shuffled.
/// </summary>
public class DeckShuffledEvent : IGameEvent
{
    /// <summary>
    /// Gets the ID of the player whose deck was shuffled.
    /// </summary>
    public int PlayerId { get; }

    /// <summary>
    /// Gets the ID of the player who caused the shuffle.
    /// </summary>
    public int SourcePlayerId => PlayerId;

    /// <summary>
    /// Initializes a new instance of the DeckShuffledEvent class.
    /// </summary>
    /// <param name="playerId">The ID of the player whose deck was shuffled.</param>
    public DeckShuffledEvent(int playerId) => PlayerId = playerId;
}