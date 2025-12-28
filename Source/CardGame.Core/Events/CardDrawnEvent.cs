using CardGame.Core.Events.Interfaces;

namespace CardGame.Core.Events
{
    public class CardDrawnEvent : IGameEvent
    {
        public int PlayerId { get; }
        public int SourcePlayerId => PlayerId;
        public CardDrawnEvent(int playerId) => PlayerId = playerId;
    }
}