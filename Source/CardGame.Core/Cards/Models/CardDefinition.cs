using System.Collections.Generic;
using CardGame.Core.Cards.Data;

namespace CardGame.Core.Cards.Models
{
    public class CardDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public CardType Type { get; }
        public IReadOnlyList<string> Subtypes { get; } 
        public CardStats BaseStats { get; }
        public IReadOnlyList<Keyword> Keywords { get; }
        public IReadOnlyList<EffectData> Effects { get; }

        public CardDefinition(
            string id,
            string name,
            string description,
            CardType type,
            IEnumerable<string> subtypes, 
            CardStats baseStats,
            IEnumerable<Keyword> keywords,
            IEnumerable<EffectData> effects)
        {
            Id = id;
            Name = name;
            Description = description;
            Type = type;
            Subtypes = new List<string>(subtypes); 
            BaseStats = baseStats;
            Keywords = new List<Keyword>(keywords);
            Effects = new List<EffectData>(effects);
        }
    }
}