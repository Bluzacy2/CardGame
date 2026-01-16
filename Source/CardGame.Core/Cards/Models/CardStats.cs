using CardGame.Core.Cards.Data;
using System.Collections.Generic;

namespace CardGame.Core.Cards.Models
{
    /// <summary>
    /// Represents the statistics and attributes of a card, including attack, health, cost, and keywords.
    /// This struct is immutable; all modification operations return new instances.
    /// </summary>
    public struct CardStats
    {
        #region Constructors
        /// <summary>
        /// Initializes a new instance of the CardStats struct with basic stats and no keywords.
        /// </summary>
        /// <param name="attack">The attack value of the card.</param>
        /// <param name="health">The health value of the card.</param>
        /// <param name="bloodCost">The blood cost to play the card. Changed from Cost to BloodCost.</param>
        /// <param name="costType">The type of resource required to play the card (defaults to Blood).</param>
        public CardStats(int attack, int health, int bloodCost, ResourceType costType = ResourceType.Blood)
        {
            Attack = attack;
            Health = health;
            BloodCost = bloodCost;
            CostType = costType;
            Keywords = new List<Keyword>();
            KeywordParams = new Dictionary<Keyword, int>();
        }

        /// <summary>
        /// Initializes a new instance of the CardStats struct with keywords and parameters.
        /// </summary>
        /// <param name="attack">The attack value of the card.</param>
        /// <param name="health">The health value of the card.</param>
        /// <param name="bloodCost">The blood cost to play the card.</param>
        /// <param name="keywords">Optional collection of keywords associated with the card.</param>
        /// <param name="keywordParams">Optional dictionary of keyword parameters.</param>
        /// <param name="costType">The type of resource required to play the card (defaults to Blood).</param>
        public CardStats(
            int attack,
            int health,
            int bloodCost,
            IEnumerable<Keyword>? keywords,
            Dictionary<Keyword, int>? keywordParams = null,
            ResourceType costType = ResourceType.Blood)
        {
            Attack = attack;
            Health = health;
            BloodCost = bloodCost;
            CostType = costType;
            Keywords = keywords != null ? new List<Keyword>(keywords) : new List<Keyword>();
            KeywordParams = new Dictionary<Keyword, int>(keywordParams ?? new Dictionary<Keyword, int>());
        }
        #endregion

        #region Properties
        /// <summary>
        /// Gets the attack value of the card.
        /// </summary>
        public int Attack { get; }

        /// <summary>
        /// Gets the health value of the card.
        /// </summary>
        public int Health { get; }

        /// <summary>
        /// Gets the blood cost to play the card.
        /// </summary>
        public int BloodCost { get; }

        /// <summary>
        /// Gets the type of resource required to play the card.
        /// </summary>
        public ResourceType CostType { get; }

        /// <summary>
        /// Gets the list of keywords associated with the card.
        /// </summary>
        public IReadOnlyList<Keyword> Keywords { get; }

        /// <summary>
        /// Gets the dictionary of keyword parameters.
        /// </summary>
        public IReadOnlyDictionary<Keyword, int> KeywordParams { get; }
        #endregion

        #region Public Methods
        /// <summary>
        /// Creates a new CardStats instance with an additional keyword added to the existing keywords.
        /// </summary>
        /// <param name="keyword">The keyword to add.</param>
        /// <returns>A new CardStats instance with the keyword added.</returns>
        public CardStats WithKeyword(Keyword keyword)
        {
            var newKeywords = new List<Keyword>(Keywords);
            if (!newKeywords.Contains(keyword)) newKeywords.Add(keyword);

            var newParams = new Dictionary<Keyword, int>();
            foreach (var kvp in KeywordParams) newParams[kvp.Key] = kvp.Value;

            return new CardStats(Attack, Health, BloodCost, newKeywords, newParams, CostType);
        }
        #endregion

        #region Operator Overloads
        /// <summary>
        /// Combines two CardStats instances by adding their numeric values and merging their keywords and parameters.
        /// </summary>
        /// <param name="first">The first CardStats instance.</param>
        /// <param name="second">The second CardStats instance.</param>
        /// <returns>A new CardStats instance representing the combination of both inputs.</returns>
        public static CardStats operator +(CardStats first, CardStats second)
        {
            var combinedKeywords = new List<Keyword>(first.Keywords);
            foreach (var keyword in second.Keywords)
            {
                if (!combinedKeywords.Contains(keyword))
                {
                    combinedKeywords.Add(keyword);
                }
            }
            
            var combinedParams = new Dictionary<Keyword, int>(first.KeywordParams);
            foreach (var kvp in second.KeywordParams)
            {
                if (!combinedParams.ContainsKey(kvp.Key) || kvp.Value > combinedParams[kvp.Key])
                    combinedParams[kvp.Key] = kvp.Value;
            }

            return new CardStats(
                first.Attack + second.Attack,
                first.Health + second.Health,
                first.BloodCost + second.BloodCost,
                combinedKeywords,
                combinedParams,
                first.CostType
            );
        }
        #endregion
    }
}