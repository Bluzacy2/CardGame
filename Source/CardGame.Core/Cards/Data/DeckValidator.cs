using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CardGame.Core.Cards.Data
{
    public static class DeckValidator
    {
        public const int DeckSize = 30;
        public const int MaxCopies = 3;

        public static bool Validate(List<int> cardIds, out string error)
        {
            error = string.Empty;
            if (cardIds.Count != DeckSize)
            {
                error = $"Talia musi mieć {DeckSize} kart. Obecnie ich jest {cardIds.Count}";
                return false;
            }
            var counts = cardIds.GroupBy(id => id).ToDictionary(g => g.Key, g => g.Count());

            foreach (var kvp in counts)
            {
                if (kvp.Value > MaxCopies)
                {
                    
                    string name = $"ID {kvp.Key}";
                    try { name = CardLibrary.Instance.GetCard(kvp.Key).Name; } catch { }

                    error = $"Karta '{name}' występuje zbyt często ({kvp.Value} razy). Max: {MaxCopies}.";
                    return false;
                }
            }

            return true;
        }
    }
}
