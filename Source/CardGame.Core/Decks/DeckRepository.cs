using System;

using System.IO;
using System.Linq;
using System.Text.Json;
using CardGame.Core.Decks.Data;
using System.Collections.Generic;

namespace CardGame.Core.Decks
{
    public class DeckRepository
    {
        public static DeckRepository Instance { get; } = new DeckRepository();
        private Dictionary<string, DeckData> _decks = new Dictionary<string, DeckData>();

        public void LoadDecksFromDirectory(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                Console.WriteLine($"[DECK REPO] Folder nie istnieje: {directoryPath}");
                return;
            }

            var files = Directory.GetFiles(directoryPath, "*.json");
            foreach (var file in files)
            {
                try
                {
                    string json = File.ReadAllText(file);
                    // Opcja CaseInsensitive pozwala pisać "cardIds" lub "CardIds"
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var deck = JsonSerializer.Deserialize<DeckData>(json, options);

                    if (deck != null && !string.IsNullOrEmpty(deck.Id))
                    {
                        _decks[deck.Id] = deck;
                        Console.WriteLine($"[DECK REPO] Załadowano talię: {deck.Name} ({deck.CardIds.Count} kart)");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[BŁĄD] Nie udało się wczytać talii z {file}: {ex.Message}");
                }
            }
        }

        public DeckData GetDeck(string deckId)
        {
            if (_decks.TryGetValue(deckId, out var deck))
            {
                return deck;
            }
            throw new Exception($"Nie znaleziono talii o ID: {deckId}");
        }
    }
}
    

