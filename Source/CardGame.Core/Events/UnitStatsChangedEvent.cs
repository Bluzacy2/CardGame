using CardGame.Core.Events.Interfaces;

namespace CardGame.Core.Events
{
    public class UnitStatsChangedEvent : IGameEvent
    {
        public int TargetUnitId { get; }
        public int? SourceId { get; }
        public int AttackDelta { get; }
        public int HealthDelta { get; }
        public int CurrentAttack { get; }
        public int CurrentHealth { get; }
        public bool IsAura { get; }

        public int SourcePlayerId { get; }

        public UnitStatsChangedEvent(int targetId, int atkDelta, int hpDelta, int curAtk, int curHp, int? sourceId, int sourcePlayerId, bool isAura = false)
        {
            TargetUnitId = targetId;
            SourceId = sourceId;
            AttackDelta = atkDelta;
            HealthDelta = hpDelta;
            CurrentAttack = curAtk;
            CurrentHealth = curHp;
            IsAura = isAura;
            SourcePlayerId = sourcePlayerId;
        }
    }
}