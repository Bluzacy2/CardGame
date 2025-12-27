using CardGame.Core.Events.Interfaces;

namespace CardGame.Core.Events
{
    public class TurnStartedEvent : IGameEvent
    {
        public int TurnNumer { get; }
        public int ActivePlayerId { get; }

        // Implementacja interfejsu: Gracz, którego tura się zaczyna
        public int SourcePlayerId => ActivePlayerId;

        public TurnStartedEvent(int turnNumer, int activePlayerId)
        {
            TurnNumer = turnNumer;
            ActivePlayerId = activePlayerId;
        }
    }
}