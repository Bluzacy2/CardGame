using CardGame.Core.Application;
using System;

namespace CardGame.Core.GameRules.Damage
{
    public class DamageCalculator
    {
        private readonly GameContext _context;

        public DamageCalculator(GameContext context)
        {
            _context = context;
        }

        public int CalculateFinalDamage(DamageContext context)
        {
            int finalDamage = context.RawAmount;

            finalDamage = _context.Keywords.ProcessDamageTaken(finalDamage, context);

            return Math.Max(0, finalDamage);
        }
    }
}