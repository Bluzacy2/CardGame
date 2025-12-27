using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using CardGame.Core.Cards.Models;

namespace CardGame.Core.Cards.Data
{
    public class CardLibrary
    {
        private Dictionary<int, CardData> _cards = new();

        public static CardLibrary Instance { get; } = new CardLibrary();

        public void Clear()
        {
            _cards.Clear();
        }

        public void LoadFromJson(string filePath)
        {
            if (!File.Exists(filePath)) throw new Exception($"Nie znaleziono pliku kart {filePath}");

            string jsonString = File.ReadAllText(filePath);
            var options = new JsonSerializerOptions
            {
                Converters = { new JsonStringEnumConverter() },
                PropertyNameCaseInsensitive = true,
            };

            List<CardData>? loadedCards = JsonSerializer.Deserialize<List<CardData>>(jsonString, options);
            _cards = (loadedCards ?? new List<CardData>()).ToDictionary(k => k.Id, v => v);

            ValidateLibrary();

            Console.WriteLine($"[LIBRARY] Załadowano i zweryfikowano {_cards.Count} kart.");
        }

        private void ValidateLibrary()
        {
            foreach (var card in _cards.Values)
            {
                foreach (var effect in card.Effects)
                {
                    foreach (var action in effect.Actions)
                    {
                        if (action.Type == ActionType.ApplyStatus && action.StatusKeyword == null)
                        {
                            if (!string.IsNullOrEmpty(action.StringParam) && Enum.TryParse<Keyword>(action.StringParam, out var k))
                            {
                                action.StatusKeyword = k;
                            }
                            else
                            {
                                throw new Exception($"BŁĄD KARTY ID {card.Id} ({card.Name}): Akcja ApplyStatus nie posiada zdefiniowanego statusu!");
                            }
                        }
                    }
                }
            }
        }

        public CardData GetCard(int id)
        {
            if (_cards.TryGetValue(id, out var card)) return card;
            throw new Exception($"Karta o ID {id} nie istnieje.");
        }

        public CardDefinition CreateDefinition(int id)
        {
            var data = GetCard(id);
            // data.Cost mapujemy na bloodCost w strukturze CardStats
            return new CardDefinition(
                data.Id.ToString(),
                data.Name,
                data.Type,
                new CardStats(data.Attack, data.Health, data.Cost, data.Keywords, data.KeywordParams, data.CostType),
                data.Keywords,
                data.Effects);
        }
    }
}