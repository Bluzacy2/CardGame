using CardGame.Core.Cards.Data;

namespace CardGame.Core.State.Models
{
    public class PendingInteraction
    {
        public int SourceCardInstanceId { get; } // Karta wykonująca efekt
        public int EffectIndex { get; }          // Który to efekt z listy Effects (0, 1, 2...)
        public int ActionIndex { get; }          // Która akcja wewnątrz efektu (0, 1, 2...)
        public TargetType RequiredTargetType { get; } // Czego UI ma szukać

        public PendingInteraction(int sourceId, int effectIndex, int actionIndex, TargetType targetType)
        {
            SourceCardInstanceId = sourceId;
            EffectIndex = effectIndex;
            ActionIndex = actionIndex;
            RequiredTargetType = targetType;
        }
    }
}