using CardGame.Core.Events.Interfaces;

namespace CardGame.Core.Events
{
    public class GameOverEvent : IGameEvent
    {
        public int? WinnerId { get; }
        public int SourcePlayerId => WinnerId ?? 0;

        public GameOverEvent(int? winnerId) => WinnerId = winnerId;
    }
}