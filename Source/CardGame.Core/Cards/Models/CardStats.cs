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

        public CardStats(int attack, int health, int bloodCost)
        {
            Attack = attack;
            Health = health;
            BloodCost = bloodCost;
        }
    }
}
