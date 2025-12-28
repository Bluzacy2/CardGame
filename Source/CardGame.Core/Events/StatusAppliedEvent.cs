using CardGame.Core.Cards.Data;
using CardGame.Core.Events.Interfaces;

namespace CardGame.Core.Events
{
    public class StatusAppliedEvent : IGameEvent
    {
        public int TargetUnitId { get; }
        public Keyword Status { get; }
        public int SourcePlayerId { get; }
        public StatusAppliedEvent(int sourcePlayerId, int targetUnitId, Keyword status)
        {
            SourcePlayerId = sourcePlayerId;
            TargetUnitId = targetUnitId;
            Status = status;
        }
    }
}