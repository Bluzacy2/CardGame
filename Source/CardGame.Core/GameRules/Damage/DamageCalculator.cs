using CardGame.Core.Cards.Data;
using System;
using System.Linq;

namespace CardGame.Core.GameRules.Damage
{
    public class DamageCalculator
    {
        public int CalculateFinalDamage(DamageContext context)
        {
            int finalDamage = context.RawAmount;

            // 1. MARKED
            if (context.Target.CurrentStats.Keywords.Contains(Keyword.Marked))
            {
                finalDamage *= 2;
                Console.WriteLine($"[CALC] Cel MARKED! Podwajam obrażenia: {context.RawAmount} -> {finalDamage}");
            }

            // 2. ARMORED (Poprawiona nazwa z 'Armor' na 'Armored')
            if (context.Target.CurrentStats.Keywords.Contains(Keyword.Armor))
            {
                if (context.Type == DamageType.Combat)
                {
                    finalDamage = Math.Max(0, finalDamage - 1);
                    Console.WriteLine($"[CALC] Cel ARMORED! Redukcja: {finalDamage + 1} -> {finalDamage}");
                }
            }

            return Math.Max(0, finalDamage);
        }
    }
}