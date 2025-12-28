using CardGame.Core.Events.Interfaces;

namespace CardGame.Core.Events
{
    public class PreLineCombatEvent : IGameEvent
    {
        public int LineIndex { get; }
        public int SourcePlayerId => 0;

        public PreLineCombatEvent(int lineIndex)
        {
            LineIndex = lineIndex;
        }
    }
}