using CardGame.Core.Events.Interfaces;

public class DeckShuffledEvent : IGameEvent
{
    public int PlayerId { get; }
    public int SourcePlayerId => PlayerId;
    public DeckShuffledEvent(int playerId) => PlayerId = playerId;
}
