using CardGame.Core.Events.Interfaces;

namespace CardGame.Core.Events
{
    public class TargetSelectedEvent : IGameEvent
    {
        public int PlayerId { get; }
        public int SourceCardId { get; }     // ID karty, której efekt wznawiamy
        public int SelectedTargetId { get; } // ID celu wybranego przez gracza

        public TargetSelectedEvent(int playerId, int sourceCardId, int selectedTargetId)
        {
            PlayerId = playerId;
            SourceCardId = sourceCardId;
            SelectedTargetId = selectedTargetId;
        }
    }
}