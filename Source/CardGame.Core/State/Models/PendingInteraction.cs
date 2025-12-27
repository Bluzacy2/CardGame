using CardGame.Core.Cards.Data;

namespace CardGame.Core.State.Models
{
    public class PendingInteraction
    {
        public int SourceCardInstanceId { get; } // Karta wykonująca efekt
        public int EffectIndex { get; }          // Który to efekt z listy Effects (0, 1, 2...)
        public int ActionIndex { get; }          // Która akcja wewnątrz efektu (0, 1, 2...)
        public TargetType RequiredTargetType { get; } // Czego UI ma szukać

        public IReadOnlyList<string> Options { get; }

        public PendingInteraction(int sourceId, int effectIndex, int actionIndex, TargetType targetType, IEnumerable<string>? options = null)
        {
            SourceCardInstanceId = sourceId;
            EffectIndex = effectIndex;
            ActionIndex = actionIndex;
            RequiredTargetType = targetType;
            Options = options != null ? new List<string>(options) : new List<string>();
        }
    }
}