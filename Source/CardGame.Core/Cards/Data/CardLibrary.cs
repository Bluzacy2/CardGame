using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using CardGame.Core.Cards.Models;

namespace CardGame.Core.Cards.Data
{
    /// <summary>
    /// Manages the collection of all available card definitions and provides methods for card creation.
    /// </summary>
    public class CardLibrary
    {
        private Dictionary<int, CardData> _cards = new Dictionary<int, CardData>();

        /// <summary>
        /// Gets the singleton instance of the CardLibrary.
        /// </summary>
        public static CardLibrary Instance { get; } = new CardLibrary();

        #region Data Management

        /// <summary>
        /// Removes all card definitions from the library.
        /// </summary>
        public void Clear()
        {
            _cards.Clear();
        }

        /// <summary>
        /// Loads card definitions from a JSON file.
        /// </summary>
        /// <param name="filePath">The path to the JSON file containing card data.</param>
        /// <exception cref="Exception">Thrown when the file is not found or card data is invalid.</exception>
        public void LoadFromJson(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new Exception($"Card file not found: {filePath}");
            }

            string jsonString = File.ReadAllText(filePath);
            var options = new JsonSerializerOptions
            {
                Converters = { new JsonStringEnumConverter() },
                PropertyNameCaseInsensitive = true,
            };

            List<CardData>? loadedCards = JsonSerializer.Deserialize<List<CardData>>(jsonString, options);
            _cards = (loadedCards ?? new List<CardData>()).ToDictionary(k => k.Id, v => v);

            ValidateLibrary();
        }

        #endregion

        #region Validation

        /// <summary>
        /// Validates all loaded cards for data integrity.
        /// </summary>
        /// <exception cref="Exception">Thrown when a card has invalid data.</exception>
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
                            if (!string.IsNullOrEmpty(action.StringParam) &&
                                Enum.TryParse<Keyword>(action.StringParam, out var keyword))
                            {
                                action.StatusKeyword = keyword;
                            }
                            else
                            {
                                throw new Exception($"CARD ERROR ID {card.Id} ({card.Name}): ApplyStatus action does not have a defined status!");
                            }
                        }
                    }
                }
            }
        }

        #endregion

        #region Card Access

        /// <summary>
        /// Retrieves card data by ID.
        /// </summary>
        /// <param name="id">The unique identifier of the card.</param>
        /// <returns>The CardData object.</returns>
        /// <exception cref="Exception">Thrown when the card ID does not exist.</exception>
        public CardData GetCard(int id)
        {
            if (_cards.TryGetValue(id, out var card))
            {
                return card;
            }

            throw new Exception($"Card with ID {id} does not exist.");
        }

        /// <summary>
        /// Creates a CardDefinition from a card ID.
        /// </summary>
        /// <param name="id">The unique identifier of the card.</param>
        /// <returns>A new CardDefinition instance.</returns>
        public CardDefinition CreateDefinition(int id)
        {
            var data = GetCard(id);
            return new CardDefinition(
                data.Id.ToString(),
                data.Name,
                data.Description,
                data.Type,
                data.Subtypes,
                new CardStats(data.Attack, data.Health, data.Cost, data.Keywords, data.KeywordParams, data.CostType),
                data.Keywords,
                data.Effects);
        }

        #endregion

        #region Query Methods

        /// <summary>
        /// Gets all card IDs in the library.
        /// </summary>
        /// <returns>An array of all card IDs.</returns>
        public int[] GetAllIds()
        {
            return _cards.Keys.ToArray();
        }

        /// <summary>
        /// Gets card data objects that match a predicate.
        /// </summary>
        /// <param name="predicate">The condition to filter cards.</param>
        /// <returns>A list of matching CardData objects.</returns>
        public List<CardData> GetCardsByTraits(Func<CardData, bool> predicate)
        {
            return _cards.Values.Where(predicate).ToList();
        }

        /// <summary>
        /// Gets card IDs that match a predicate.
        /// </summary>
        /// <param name="predicate">The condition to filter cards.</param>
        /// <returns>A list of matching card IDs.</returns>
        public List<int> GetCardIdsByTraits(Func<CardData, bool> predicate)
        {
            return _cards.Values.Where(predicate).Select(c => c.Id).ToList();
        }

        #endregion
    }
}