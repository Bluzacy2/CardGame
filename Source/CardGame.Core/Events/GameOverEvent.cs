using CardGame.Core.Events.Interfaces;

namespace CardGame.Core.Events
{
    public class GameOverEvent : IGameEvent
    {
        public int? WinnerId { get; }
        public GameOverEvent(int? winnerId) => WinnerId = winnerId;
    }
}