using CardGame.Core.Events.Interfaces;

namespace CardGame.Core.Events
{
    public class TargetSelectedEvent : IGameEvent
    {
        public int PlayerId { get; }
        public int SourcePlayerId => PlayerId;
        public int SourceCardId { get; }
        public int SelectedTargetId { get; }

        public TargetSelectedEvent(int playerId, int sourceCardId, int selectedTargetId)
        {
            PlayerId = playerId;
            SourceCardId = sourceCardId;
            SelectedTargetId = selectedTargetId;
        }
    }
}