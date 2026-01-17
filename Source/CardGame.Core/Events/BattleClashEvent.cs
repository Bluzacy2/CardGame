using CardGame.Core.Events.Interfaces;

namespace CardGame.Core.Events
{
    public class BattleClashEvent : IGameEvent
    {
        public int AttackerId { get; }
        public int? DefenderId { get; }
        public int SourcePlayerId => 0;

        public BattleClashEvent(int attackerId, int? defenderId)
        {
            AttackerId = attackerId;
            DefenderId = defenderId;
        }
    }
}
