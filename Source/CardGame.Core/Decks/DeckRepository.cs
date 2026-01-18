using CardGame.Core.Decks.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace CardGame.Core.Decks
{
    /// <summary>
    /// Repository for loading and accessing deck data definitions from JSON files.
    /// </summary>
    public class DeckRepository
    {
        #region Properties

        /// <summary>
        /// Gets the singleton instance of the DeckRepository.
        /// </summary>
        public static DeckRepository Instance { get; } = new DeckRepository();

        #endregion

        #region Fields

        private Dictionary<string, DeckData> _decks = new Dictionary<string, DeckData>();

        #endregion

        #region Public Methods

        /// <summary>
        /// Loads all deck definitions from JSON files in the specified directory.
        /// </summary>
        /// <param name="directoryPath">The path to the directory containing deck JSON files.</param>
        public void LoadDecksFromDirectory(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                Console.WriteLine($"[DECK REPO] Directory does not exist: {directoryPath}");
                return;
            }

            var files = Directory.GetFiles(directoryPath, "*.json");
            foreach (var file in files)
            {
                try
                {
                    string json = File.ReadAllText(file);
                    // CaseInsensitive option allows "cardIds" or "CardIds"
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var deck = JsonSerializer.Deserialize<DeckData>(json, options);

                    if (deck != null && !string.IsNullOrEmpty(deck.Id))
                    {
                        _decks[deck.Id] = deck;
                        Console.WriteLine($"[DECK REPO] Loaded deck: {deck.Name} ({deck.CardIds.Count} cards)");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Failed to load deck from {file}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Retrieves deck data by its unique identifier.
        /// </summary>
        /// <param name="deckId">The unique identifier of the deck.</param>
        /// <returns>The deck data object.</returns>
        /// <exception cref="Exception">Thrown when the deck ID is not found.</exception>
        public DeckData GetDeck(string deckId)
        {
            if (_decks.TryGetValue(deckId, out var deck))
            {
                return deck;
            }

            throw new Exception($"Deck with ID not found: {deckId}");
        }

        #endregion
    }
}