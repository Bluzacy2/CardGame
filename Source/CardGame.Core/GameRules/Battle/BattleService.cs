using CardGame.Core.State.Models;
using CardGame.Core.Events;
using CardGame.Core.Application;
using CardGame.Core.Cards.Models;
using CardGame.Core.Cards.Data;
using CardGame.Core.GameRules.Damage;
using CardGame.Core.GameRules.Death;
using System;
using System.Linq;

namespace CardGame.Core.GameRules.Battle
{
    public class BattleService
    {
        private readonly DeathResolver _deathResolver = new DeathResolver();

        public GameState ResolveCombatDuel(GameState state, CardInstance u1, CardInstance u2, int lineIdx, EventBus events, GameContext context)
        {
            var workingState = state;
            bool u1Flying = u1.CurrentStats.Keywords.Contains(Keyword.Flying);
            bool u2Flying = u2.CurrentStats.Keywords.Contains(Keyword.Flying);

            // FLYING LOGIC: If one unit is flying and the other is not, they ignore each other and strike heroes.
            if (u1Flying != u2Flying)
            {
                workingState = ResolveBonusStrike(workingState, u1, null, lineIdx, events, context);
                workingState = ResolveBonusStrike(workingState, u2, null, lineIdx, events, context);
                return workingState;
            }

            bool u1Stunned = u1.CurrentStats.Keywords.Contains(Keyword.Stunned);
            bool u2Stunned = u2.CurrentStats.Keywords.Contains(Keyword.Stunned);

            // Handle Stun removal
            var activeU1 = u1Stunned ? u1.SuppressKeyword(Keyword.Stunned) : u1;
            var activeU2 = u2Stunned ? u2.SuppressKeyword(Keyword.Stunned) : u2;

            if (u1Stunned) workingState = workingState.UpdateBoard(workingState.Board.UpdateUnit(activeU1));
            if (u2Stunned) workingState = workingState.UpdateBoard(workingState.Board.UpdateUnit(activeU2));

            // Calculate Damage (0 if Stunned)
            int dmgToU2 = u1Stunned ? 0 : context.DamageCalculator.CalculateFinalDamage(new DamageContext(activeU1, activeU2, activeU1.CurrentStats.Attack, DamageType.Combat));
            int dmgToU1 = u2Stunned ? 0 : context.DamageCalculator.CalculateFinalDamage(new DamageContext(activeU2, activeU1, activeU2.CurrentStats.Attack, DamageType.Combat));

            events.Publish(new BattleClashEvent(u1.InstanceId, u2.InstanceId));
            events.Publish(new BattleClashEvent(u2.InstanceId, u1.InstanceId));

            var nextU1 = activeU1.TakeDamage(dmgToU1);
            var nextU2 = activeU2.TakeDamage(dmgToU2);

            workingState = workingState.UpdateBoard(workingState.Board.UpdateUnit(nextU1));
            workingState = workingState.UpdateBoard(workingState.Board.UpdateUnit(nextU2));

            // Resolve Post-Attack keywords (like Splash or BurnSource)
            var finalU1 = workingState.Board.Lines[lineIdx].Player1Unit;
            var finalU2 = workingState.Board.Lines[lineIdx].Player2Unit;

            if (finalU1 != null && finalU2 != null)
            {
                if (!u1Stunned) workingState = context.Keywords.ProcessPostAttack(workingState, finalU1, finalU2, lineIdx, context);
                if (!u2Stunned) workingState = context.Keywords.ProcessPostAttack(workingState, finalU2, finalU1, lineIdx, context);
            }

            // Publish Events using the "Hybrid" constructor for UI support
            if (dmgToU1 > 0)
                events.Publish(new UnitDamagedEvent(nextU1, dmgToU1, activeU2, nextU1.CurrentStats.Health));

            if (dmgToU2 > 0)
                events.Publish(new UnitDamagedEvent(nextU2, dmgToU2, activeU1, nextU2.CurrentStats.Health));

            return workingState;
        }

        public GameState ResolveBonusStrike(GameState state, CardInstance attacker, CardInstance? defender, int lineIdx, EventBus events, GameContext context)
        {
            // Handle Stunned check
            if (attacker.CurrentStats.Keywords.Contains(Keyword.Stunned))
            {
                var cleanAttacker = attacker.SuppressKeyword(Keyword.Stunned);
                return state.UpdateBoard(state.Board.UpdateUnit(cleanAttacker));
            }

            var workingState = state;

            // FLYING LOGIC: Bonus attacks are also blocked/ignored by flying mismatch
            if (defender != null)
            {
                bool attackerFlying = attacker.CurrentStats.Keywords.Contains(Keyword.Flying);
                bool defenderFlying = defender.CurrentStats.Keywords.Contains(Keyword.Flying);
                if (attackerFlying != defenderFlying) defender = null;
            }

            events.Publish(new BattleClashEvent(attacker.InstanceId, defender?.InstanceId));

            if (defender != null)
            {
                // Damage to Unit
                int dmg = context.DamageCalculator.CalculateFinalDamage(new DamageContext(attacker, defender, attacker.CurrentStats.Attack, DamageType.Combat));
                var nextDefender = defender.TakeDamage(dmg);
                workingState = workingState.UpdateBoard(workingState.Board.UpdateUnit(nextDefender));
                workingState = context.Keywords.ProcessPostAttack(workingState, attacker, nextDefender, lineIdx, context);

                // Publish Unit Damage
                events.Publish(new UnitDamagedEvent(nextDefender, dmg, attacker, nextDefender.CurrentStats.Health));
            }
            else
            {
                // Damage to Hero
                int dmgValue = attacker.CurrentStats.Attack;
                var opponent = workingState.GetOpponent(attacker.OwnerPlayerId);
                workingState = workingState.UpdatePlayer(opponent.WithDamageTaken(dmgValue));
                workingState = context.Keywords.ProcessPostAttack(workingState, attacker, null, lineIdx, context);

                // Publish Hero Damage (Passing null as Unit indicates Hero in your hybrid event)
                events.Publish(new UnitDamagedEvent(null, dmgValue, attacker, opponent.Health - dmgValue));
            }

            // Check for deaths after the strike
            return _deathResolver.ResolveDeaths(workingState, events, context);
        }
    }
}