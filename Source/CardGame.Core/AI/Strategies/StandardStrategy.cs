using System.Linq;
using CardGame.Core.AI.Interfaces;
using CardGame.Core.Cards.Data;
using CardGame.Core.State.Models;

namespace CardGame.Core.AI.Strategies
{
    public class StandardStrategy : IAIStrategy
    {
        private const float HealthWeight = 1.0f;
        private const float BoardControlWeight = 2.5f;
        private const float HandAdvantageWeight = 1.5f;
        private const float SynergyBonus = 5.0f;

        public float Evaluate(GameState state, int botPlayerId)
        {
            var bot = state.GetPlayer(botPlayerId);
            var enemy = state.GetOpponent(botPlayerId);

            float score = 0;

            // 1. Różnica życia
            score += (bot.Health - enemy.Health) * HealthWeight;

            // 2. Kontrola stołu
            var myUnits = state.Board.GetAllUnits().Where(u => u.OwnerPlayerId == botPlayerId);
            var enemyUnits = state.Board.GetAllUnits().Where(u => u.OwnerPlayerId != botPlayerId);

            float myPower = myUnits.Sum(u => u.CurrentStats.Attack + u.CurrentStats.Health + (u.CurrentStats.Keywords.Count * 2));
            float enemyPower = enemyUnits.Sum(u => u.CurrentStats.Attack + u.CurrentStats.Health + (u.CurrentStats.Keywords.Count * 2));

            score += (myPower - enemyPower) * BoardControlWeight;

            // 3. Przewaga kart
            score += (bot.Hand.Count - enemy.Hand.Count) * HandAdvantageWeight;

            // 4. Analiza ręki i kosztów (Używamy BloodCost)
            foreach (var card in bot.Hand)
            {
                bool hasExecutionSynergy = card.Definition.Effects.Any(e =>
                    e.Actions.Any(a => a.Type == ActionType.DealDamage));

                bool enemyHasDamagedUnits = enemyUnits.Any(u => u.DamageTaken > 0);

                if (hasExecutionSynergy && enemyHasDamagedUnits)
                {
                    score += SynergyBonus;
                }

                // Sprawdzamy MaxBlood bota, a nie na sztywno PlayerA
                if (card.CurrentStats.BloodCost > bot.MaxBlood + 2)
                {
                    score -= 1.0f; // Kara za zbyt drogą kartę w ręce
                }
            }

            return score;
        }
    }
}