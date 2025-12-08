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
            return unit.CurrentStats.Keywords.Contains(Keyword.SoulGuard);
        }

        public GameState PreventDeath(CardInstance unit, GameState currentState)
        {
           
            var keywordsList = new List<Keyword>(unit.PermanentStats.Keywords); 
            if (keywordsList.Contains(Keyword.SoulGuard))
            {
                keywordsList.Remove(Keyword.SoulGuard);
            }

            if (!keywordsList.Contains(Keyword.SoulGuardDepleted))
            {
                keywordsList.Add(Keyword.SoulGuardDepleted);
            }

            
            int maxHp = 1;

            var newStats = new CardStats(
                unit.PermanentStats.Attack,
                maxHp,
                unit.PermanentStats.BloodCost,
                keywordsList
            );

           
            var survivedUnit = unit.WithStats(newStats);

            System.Console.WriteLine($"[SOUL GUARD] {unit.Definition.Name} uniknął śmierci! (Tarcza zużyta, nałożono Depleted)");

            return currentState.UpdateBoard(currentState.Board.UpdateUnit(survivedUnit));
        }
    }
}