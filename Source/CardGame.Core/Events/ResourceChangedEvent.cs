using CardGame.Core.Events.Interfaces;

namespace CardGame.Core.Events
{
    public class ResourceChangedEvent : IGameEvent
    {
        public int PlayerId { get; }
        public int OldValue { get; }
        public int NewValue { get; }
        public int SourcePlayerId => PlayerId;

        public ResourceChangedEvent(int playerId, int oldValue, int newValue)
        {
            PlayerId = playerId;
            OldValue = oldValue;
            NewValue = newValue;
        }
    }
}
