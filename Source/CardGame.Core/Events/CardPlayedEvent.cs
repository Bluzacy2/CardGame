using CardGame.Core.Cards.Models;
using CardGame.Core.Events.Interfaces;

namespace CardGame.Core.Events
{
    public class CardPlayedEvent : IGameEvent
    {
        public int PlayerId { get; }
        public int SourcePlayerId => PlayerId;
        public CardInstance Card { get; }
        public int? LineIndex { get; }
        public int? SelectedTargetId { get; }

        public CardPlayedEvent(int playerId, CardInstance card, int? lineIndex, int? selectedTargetId)
        {
            PlayerId = playerId;
            Card = card;
            LineIndex = lineIndex;
            SelectedTargetId = selectedTargetId;
        }
    }
}