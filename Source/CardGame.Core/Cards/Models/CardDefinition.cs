using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using CardGame.Core.Cards.Data;

namespace CardGame.Core.Cards.Models
{
    public class CardDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public CardStats BaseStats { get; }

        public IReadOnlyList<Keyword> Keywords { get; }
        
        public IReadOnlyList<EffectData> Effects { get; }


        public CardDefinition(string id, string name, CardStats baseStats,
            IEnumerable<Keyword> kewords,
            IEnumerable<EffectData> effects)
        {
            Id = id;
            Name = name;
            BaseStats = baseStats;
            Keywords = new List<Keyword>(kewords);
            Effects = new List<EffectData>(effects);
        }
    }
}
