using CardGame.Core.State.Models;
using CardGame.Core.Events;
using CardGame.Core.Application;
using CardGame.Core.Cards.Models;
using CardGame.Core.Cards.Data;
using CardGame.Core.GameRules.Damage;
using CardGame.Core.GameRules.Death;
using System.Linq;

namespace CardGame.Core.GameRules.Battle
{
    public class BattleService
    {
        private readonly DeathResolver _deathResolver = new DeathResolver();

        public GameState ResolveCombatDuel(GameState state, CardInstance u1, CardInstance u2, int lineIdx, EventBus events, GameContext context)
        {
            int dmgToU2 = context.DamageCalculator.CalculateFinalDamage(new DamageContext(u1, u2, u1.CurrentStats.Attack, DamageType.Combat));
            int dmgToU1 = context.DamageCalculator.CalculateFinalDamage(new DamageContext(u2, u1, u2.CurrentStats.Attack, DamageType.Combat));

            var workingState = state;
            var nextU1 = u1.TakeDamage(dmgToU1);
            var nextU2 = u2.TakeDamage(dmgToU2);

            workingState = ApplyPostAttackEffects(workingState, u1, nextU2, lineIdx, events, out nextU2);
            workingState = ApplyPostAttackEffects(workingState, u2, nextU1, lineIdx, events, out nextU1);

            workingState = workingState.UpdateBoard(workingState.Board.UpdateUnit(nextU1));
            workingState = workingState.UpdateBoard(workingState.Board.UpdateUnit(nextU2));

            events.Publish(new UnitDamagedEvent(nextU1, dmgToU1, u2));
            events.Publish(new UnitDamagedEvent(nextU2, dmgToU2, u1));

            return workingState;
        }

        public GameState ResolveBonusStrike(GameState state, CardInstance attacker, CardInstance? defender, int lineIdx, EventBus events, GameContext context)
        {
            var workingState = state;

            if (defender != null)
            {
                int dmg = context.DamageCalculator.CalculateFinalDamage(new DamageContext(attacker, defender, attacker.CurrentStats.Attack, DamageType.Combat));
                var nextDefender = defender.TakeDamage(dmg);
                workingState = ApplyPostAttackEffects(workingState, attacker, nextDefender, lineIdx, events, out nextDefender);
                workingState = workingState.UpdateBoard(workingState.Board.UpdateUnit(nextDefender));
                events.Publish(new UnitDamagedEvent(nextDefender, dmg, attacker));
            }
            else
            {
                // ATAK W BOHATERA
                int dmgValue = attacker.CurrentStats.Attack;
                var opponent = workingState.GetOpponent(attacker.OwnerPlayerId);
                workingState = workingState.UpdatePlayer(opponent.WithDamageTaken(dmgValue));
                Console.WriteLine($"[WALKA] {attacker.Definition.Name} uderza bohatera za {dmgValue}!");

                // SPLASH PRZY ATAKU W BOHATERA
                if (attacker.CurrentStats.Keywords.Contains(Keyword.SplashDamage))
                {
                    if (attacker.Definition.BaseStats.KeywordParams.TryGetValue(Keyword.SplashDamage, out int val))
                    {
                        workingState = ApplySplash(workingState, lineIdx, attacker.OwnerPlayerId, val, attacker, events);
                    }
                }
            }
            return _deathResolver.ResolveDeaths(workingState, events);
        }

        private GameState ApplyPostAttackEffects(GameState state, CardInstance attacker, CardInstance victim, int lineIdx, EventBus events, out CardInstance updatedVictim)
        {
            var workingState = state;
            updatedVictim = victim;

            if (attacker.CurrentStats.Keywords.Contains(Keyword.SplashDamage))
            {
                if (attacker.Definition.BaseStats.KeywordParams.TryGetValue(Keyword.SplashDamage, out int val))
                    workingState = ApplySplash(workingState, lineIdx, attacker.OwnerPlayerId, val, attacker, events);
            }

            if (attacker.CurrentStats.Keywords.Contains(Keyword.BurnSource))
                updatedVictim = updatedVictim.AddPermanentBuff(new CardStats(0, 0, 0, new[] { Keyword.Burning }));

            return workingState;
        }

        private GameState ApplySplash(GameState state, int lineIdx, int attackerId, int dmg, CardInstance source, EventBus events)
        {
            var workingState = state;
            int opponentId = attackerId == 1 ? 2 : 1;
            foreach (int neighbor in new[] { lineIdx - 1, lineIdx + 1 })
            {
                if (neighbor >= 0 && neighbor < 4)
                {
                    var unit = workingState.Board.Lines[neighbor].IsSlotEmpty(opponentId) ? null :
                               (opponentId == 1 ? workingState.Board.Lines[neighbor].Player1Unit : workingState.Board.Lines[neighbor].Player2Unit);
                    if (unit != null)
                    {
                        var nextUnit = unit.TakeDamage(dmg);
                        workingState = workingState.UpdateBoard(workingState.Board.UpdateUnit(nextUnit));
                        events.Publish(new UnitDamagedEvent(nextUnit, dmg, source));
                    }
                }
            }
            return workingState;
        }
    }
}