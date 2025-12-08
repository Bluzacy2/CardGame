using System.Collections.Generic;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Models;
using CardGame.Core.State.Models;

namespace CardGame.Core.GameRules.Death.Prevention
{
    public class SoulGuardPrevention : IDeathPrevention
    {
        public bool CanPreventDeath(CardInstance unit, GameState state)
        {
            var keywords = unit.CurrentStats.Keywords;

            
            bool hasShield = keywords.Contains(Keyword.SoulGuard);
            bool isDepleted = keywords.Contains(Keyword.SoulGuardDepleted);

            return hasShield && !isDepleted;
        }

        public GameState PreventDeath(CardInstance unit, GameState currentState)
        {

            int maxHp = unit.MaxHealth;
            int neededDamageTaken = maxHp - 1;

            if (neededDamageTaken < 0) neededDamageTaken = 0;
           
            var depletedMarker = new CardStats(0, 0, 0, new List<Keyword> { Keyword.SoulGuardDepleted });

            var survivedUnit = unit
                .WithDamage(neededDamageTaken)     
                .AddPermanentBuff(depletedMarker);  

            System.Console.WriteLine($"[SOUL GUARD] {unit.Definition.Name} uniknął śmierci! (HP ustawione na 1, Tarcza zużyta)");

            return currentState.UpdateBoard(currentState.Board.UpdateUnit(survivedUnit));
        }
    }
}