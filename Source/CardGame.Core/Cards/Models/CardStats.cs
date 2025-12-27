using CardGame.Core.Cards.Data;
using System.Collections.Generic;

namespace CardGame.Core.Cards.Models
{
    public struct CardStats
    {
        public int Attack { get; }
        public int Health { get; }
        public int BloodCost { get; } // Zmieniono z Cost na BloodCost
        public ResourceType CostType { get; }

        public IReadOnlyList<Keyword> Keywords { get; }
        public IReadOnlyDictionary<Keyword, int> KeywordParams { get; }

        public CardStats(int attack, int health, int bloodCost, ResourceType costType = ResourceType.Blood)
        {
            Attack = attack;
            Health = health;
            BloodCost = bloodCost;
            CostType = costType;
            Keywords = new List<Keyword>();
            KeywordParams = new Dictionary<Keyword, int>();
        }

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

        public CardStats WithKeyword(Keyword keyword)
        {
            var newKeywords = new List<Keyword>(Keywords);
            if (!newKeywords.Contains(keyword)) newKeywords.Add(keyword);

            var newParams = new Dictionary<Keyword, int>();
            foreach (var kvp in KeywordParams) newParams[kvp.Key] = kvp.Value;

            return new CardStats(Attack, Health, BloodCost, newKeywords, newParams, CostType);
        }

        public static CardStats operator +(CardStats a, CardStats b)
        {
            var combinedKeywords = new List<Keyword>(a.Keywords);
            foreach (var keyword in b.Keywords)
            {
                if (!combinedKeywords.Contains(keyword))
                {
                    combinedKeywords.Add(keyword);
                }
            }
            var combinedParams = new Dictionary<Keyword, int>(a.KeywordParams);
            foreach (var kvp in b.KeywordParams)
            {
                if (!combinedParams.ContainsKey(kvp.Key) || kvp.Value > combinedParams[kvp.Key])
                    combinedParams[kvp.Key] = kvp.Value;
            }

            return new CardStats(
                a.Attack + b.Attack,
                a.Health + b.Health,
                a.BloodCost + b.BloodCost,
                combinedKeywords,
                combinedParams,
                a.CostType
            );
        }
    }
}