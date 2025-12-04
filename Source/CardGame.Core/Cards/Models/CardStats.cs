using CardGame.Core.Cards.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CardGame.Core.Cards.Models
{
    public struct CardStats
    {
        public int Attack { get; }
        public int Health { get; }
        public int BloodCost { get; }

        public IReadOnlyList<Keyword> Keywords { get; }

        public CardStats(int attack, int health, int bloodCost, IEnumerable<Keyword> keywords)
        {
            Attack = attack;
            Health = health;
            BloodCost = bloodCost;
            Keywords = keywords != null ? new List<Keyword>(keywords) : new List<Keyword>();
        }
        public CardStats WithKeyword(Keyword keyword)
        {
            var newKeywords = new List<Keyword>(Keywords);
            if (!newKeywords.Contains(keyword))
            {
                newKeywords.Add(keyword);
            }
            return new CardStats(Attack, Health, BloodCost, newKeywords);
        }
    }
}
