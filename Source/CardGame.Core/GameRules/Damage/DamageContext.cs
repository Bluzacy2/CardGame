using CardGame.Core.Cards.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CardGame.Core.GameRules.Damage
{
    public class DamageContext
    {
        public CardInstance Source { get; }
        public CardInstance Target { get; }
        public int RawAmount { get; }
        public DamageType Type { get; }
        public DamageContext(CardInstance source, CardInstance target, int rawAmount, DamageType type)
        {
            Source = source;
            Target = target;
            RawAmount = rawAmount;
            Type = type;
        }
    }
}
