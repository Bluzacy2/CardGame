using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CardGame.Core.Cards.Models
{
    public class CardDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public CardStats BaseStats { get; }

        public CardDefinition(string id, string name, CardStats baseStats)
        {
            Id = id;
            Name = name;
            BaseStats = baseStats;
        }
    }
}
