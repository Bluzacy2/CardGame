using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using CardGame.Core.Cards.Models;

namespace CardGame.Core.Cards.Data
{
    public  class CardLibrary
    {
        private Dictionary<int, CardData> _cards = new();

        public static CardLibrary Instance { get; } = new CardLibrary();

        public void LoadFromJson(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new Exception($"Nie znaleziono pliku kart {filePath}");
            }
            string jsonString = File.ReadAllText(filePath);
            var options = new JsonSerializerOptions
            {
                Converters = { new JsonStringEnumConverter()},
                PropertyNameCaseInsensitive = true,
            };

            List<CardData>? loadedCards = JsonSerializer.Deserialize<List<CardData>>(jsonString, options);

            _cards = (loadedCards ?? new List<CardData>())
                        .ToDictionary(k => k.Id, v => v);

            Console.WriteLine($"[LIBRARY] Załadowano {_cards.Count} kart z JSON.");
        }
        public CardData GetCard(int id)
        { 
            if (_cards.TryGetValue(id, out var card))
            {  return card; }
            throw new Exception($"Karta o ID {id} nie istnieje.");
        }
        public CardDefinition CreateDefinition(int id)
        {
            var data = GetCard(id);
            return new CardDefinition(data.Id.ToString(),
                data.Name, new CardStats(data.Attack, data.Health, data.Cost, data.Keywords), data.Keywords,
                data.Effects);
        }
    }
}
