using System;
using System.Collections.Generic;
using System.Linq;

namespace CardGame.Core.Cards.Data
{
    /// <summary>
    /// Provides validation logic for deck construction rules.
    /// </summary>
    public static class DeckValidator
    {
        /// <summary>
        /// The required number of cards in a valid deck.
        /// </summary>
        public const int DeckSize = 30;

        /// <summary>
        /// The maximum number of copies allowed for a single card in a deck.
        /// </summary>
        public const int MaxCopies = 3;

        /// <summary>
        /// Validates a deck against game rules.
        /// </summary>
        /// <param name="cardIds">The list of card IDs in the deck.</param>
        /// <param name="error">Output parameter containing validation error message if any.</param>
        /// <returns>True if the deck is valid, otherwise false.</returns>
        public static bool Validate(List<int> cardIds, out string error)
        {
            error = string.Empty;

            if (cardIds.Count != DeckSize)
            {
                error = $"Deck must have {DeckSize} cards. Currently has {cardIds.Count}.";
                return false;
            }

            var counts = cardIds.GroupBy(id => id)
                .ToDictionary(g => g.Key, g => g.Count());

            foreach (var kvp in counts)
            {
                if (kvp.Value > MaxCopies)
                {
                    string name = $"ID {kvp.Key}";

                    try
                    {
                        name = CardLibrary.Instance.GetCard(kvp.Key).Name;
                    }
                    catch
                    {
                        // Fallback to ID if card name cannot be retrieved
                    }

                    error = $"Card '{name}' appears too many times ({kvp.Value} copies). Maximum allowed: {MaxCopies}.";
                    return false;
                }
            }

            return true;
        }
    }
}