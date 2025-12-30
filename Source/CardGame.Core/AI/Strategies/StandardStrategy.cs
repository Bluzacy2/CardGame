using CardGame.Core.AI.Interfaces;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Models;
using CardGame.Core.State.Models;
using System.Linq;

namespace CardGame.Core.AI.Strategies
{
    public class StandardStrategy : IAIStrategy
    {
        private const float HealthWeight = 1.5f;
        private const float BoardPresenceWeight = 2.5f;
        private const float HandPotentialWeight = 0.7f;
        private const float TempoWeight = -0.2f;

        public float Evaluate(GameState state, int botPlayerId)
        {
            var bot = state.GetPlayer(botPlayerId);
            var enemy = state.GetOpponent(botPlayerId);

            float score = 0;

            float healthScore = (bot.Health - enemy.Health) * HealthWeight;
            if (bot.Health < 10)
            {
                healthScore *= (20 - bot.Health) / 5.0f;
            }
            score += healthScore;

            float boardScore = 0;
            for (int lane = 0; lane < state.Board.Lines.Count; lane++)
            {
                var botUnit = GetUnitAt(state, bot.PlayerId, lane);
                var enemyUnit = GetUnitAt(state, enemy.PlayerId, lane);

                float botUnitPower = EvaluateUnitPower(botUnit, enemyUnit, state, lane);
                float enemyUnitPower = EvaluateUnitPower(enemyUnit, botUnit, state, lane);

                boardScore += (botUnitPower - enemyUnitPower);
            }
            score += boardScore * BoardPresenceWeight;

            score += EvaluateHandPotential(bot, state) * HandPotentialWeight;

            score += (bot.MaxBlood - bot.CurrentBlood) * TempoWeight;

            return score;
        }

        private float EvaluateUnitPower(CardInstance? unit, CardInstance? opponent, GameState state, int lane)
        {
            if (unit == null) return 0;
            if (unit.CurrentStats.Keywords.Contains(Keyword.Stunned)) return 0;

            float power = 0;
            int effectiveHealth = unit.CurrentStats.Health;
            power += unit.CurrentStats.Attack;

            if (unit.CurrentStats.Keywords.Contains(Keyword.Armored) && opponent != null) effectiveHealth += 1;
            if (unit.CurrentStats.Keywords.Contains(Keyword.Armor2) && opponent != null) effectiveHealth += 2;

            if (unit.CurrentStats.Keywords.Contains(Keyword.SplashDamage))
            {
                int splashValue = unit.Definition.BaseStats.KeywordParams.GetValueOrDefault(Keyword.SplashDamage, 1);
                int neighborCount = CountAdjacentEnemies(lane, unit.OwnerPlayerId, state);
                power += splashValue * neighborCount * 1.5f;
            }

            if (unit.CurrentStats.Keywords.Contains(Keyword.DoubleStrike)) power += unit.CurrentStats.Attack;
            if (unit.CurrentStats.Keywords.Contains(Keyword.SoulGuard) && !unit.CurrentStats.Keywords.Contains(Keyword.SoulGuardDepleted)) power += (unit.CurrentStats.Attack + effectiveHealth) * 0.8f;
            if (unit.CurrentStats.Keywords.Contains(Keyword.Unkillable)) power += (unit.Definition.BaseStats.Attack + unit.Definition.BaseStats.Health) * 0.5f - unit.Definition.BaseStats.BloodCost;

            power += effectiveHealth;
            return power;
        }

        private float EvaluateHandPotential(PlayerState botPlayer, GameState state)
        {
            float handScore = 0;
            var enemyUnits = state.Board.GetAllUnits().Where(u => u.OwnerPlayerId != botPlayer.PlayerId).ToList();

            foreach (var card in botPlayer.Hand)
            {
                if (card.CurrentStats.CostType == ResourceType.Blood && card.CurrentStats.BloodCost > botPlayer.CurrentBlood)
                {
                    handScore -= 2.0f;
                }

                if (card.Definition.Type == CardType.Spell)
                {
                    var spellAction = card.Definition.Effects.FirstOrDefault()?.Actions.FirstOrDefault();
                    if (spellAction == null) continue;

                    switch (spellAction.Type)
                    {
                        case ActionType.DealDamage:
                            if (spellAction.Target == TargetType.TargetEnemyUnit && enemyUnits.Any())
                            {
                                float bestOutcome = 0;
                                foreach (var enemy in enemyUnits)
                                {
                                    if (spellAction.Amount >= enemy.CurrentStats.Health)
                                    {
                                        float outcome = EvaluateUnitPower(enemy, null, state, -1) * 1.2f;
                                        if (outcome > bestOutcome) bestOutcome = outcome;
                                    }
                                }
                                handScore += bestOutcome;
                            }
                            break;

                        case ActionType.DrawCard:
                            handScore += spellAction.Amount * 1.5f;
                            break;
                    }
                }
            }
            return handScore;
        }

        private CardInstance? GetUnitAt(GameState state, int playerId, int lane)
        { 
            if (lane < 0 || lane >= state.Board.Lines.Count) return null;
            var line = state.Board.Lines[lane];
            return playerId == 1 ? line.Player1Unit : line.Player2Unit;
        }

        private int CountAdjacentEnemies(int lane, int ownerId, GameState state)
        {
            int count = 0;
            int opponentId = 3 - ownerId;
            if (lane > 0 && GetUnitAt(state, opponentId, lane - 1) != null) count++;
            if (lane < state.Board.Lines.Count - 1 && GetUnitAt(state, opponentId, lane + 1) != null) count++;
            return count;
        }
    }
}
