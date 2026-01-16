using CardGame.Core.Events.Interfaces;
using CardGame.Core.State.Enums;

namespace CardGame.Core.Events
{
    public class PhaseChangedEvent : IGameEvent
    {
        public GamePhase NewPhase { get; }
        public int ActivePlayerId { get; }
        public int SourcePlayerId => ActivePlayerId;

        public PhaseChangedEvent(GamePhase newPhase, int activePlayerId)
        {
            NewPhase = newPhase;
            ActivePlayerId = activePlayerId;
        }
    }
}