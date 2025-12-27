using System.Collections.Generic;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Models;
using CardGame.Core.State.Models;
using System;

namespace CardGame.Core.GameRules.Death.Prevention
{
    public class SoulGuardPrevention : IDeathPrevention
    {
        public bool CanPreventDeath(CardInstance unit, GameState state)
        {
            var keywords = unit.CurrentStats.Keywords;
            return keywords.Contains(Keyword.SoulGuard) && !keywords.Contains(Keyword.SoulGuardDepleted);
        }

        public GameState PreventDeath(CardInstance unit, GameState currentState)
        {
            int maxHp = unit.MaxHealth;
            // Aby HP wynosiło 1, DamageTaken musi wynosić MaxHP - 1
            int damageToSet = Math.Max(0, maxHp - 1);

            var depletedMarker = new CardStats(0, 0, 0, new List<Keyword> { Keyword.SoulGuardDepleted });

            // Używamy WithDamage (nadpisanie), a nie TakeDamage (dodanie), 
            // bo inaczej przy -7 HP mielibyśmy nadal ujemną wartość.
            var survivedUnit = unit
                .WithDamage(damageToSet)
                .AddPermanentBuff(depletedMarker);

            Console.WriteLine($"[SOULGUARD] {unit.Definition.Name} (ID:{unit.InstanceId}) ratuje się przed śmiercią (HP: 1).");
            return currentState.UpdateBoard(currentState.Board.UpdateUnit(survivedUnit));
        }
    }
}