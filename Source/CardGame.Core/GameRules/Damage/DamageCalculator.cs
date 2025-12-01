using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CardGame.Core.GameRules.Damage
{
    public class DamageCalculator
    {
        public int CalculateFinalDamage(DamageContext context)
        {
            int finalDamage = context.RawAmount;
            /* W przyszłości tutaj będzie się rozwiązywało lokigę związaną z Armorami, Markami itd. */

            return Math.Max(0, finalDamage);
        }
    }
}
