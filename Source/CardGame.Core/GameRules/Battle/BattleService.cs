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

            bool u1Stunned = u1.CurrentStats.Keywords.Contains(Keyword.Stunned);
            bool u2Stunned = u2.CurrentStats.Keywords.Contains(Keyword.Stunned);

            // Jeśli jednostka jest oszołomiona, usuwamy Stun (odpoczęła turę), ale nie pozwalamy jej zadać obrażeń w tej rundzie.
            var activeU1 = u1Stunned ? u1.SuppressKeyword(Keyword.Stunned) : u1;
            var activeU2 = u2Stunned ? u2.SuppressKeyword(Keyword.Stunned) : u2;

            if (u1Stunned)
            {
                workingState = workingState.UpdateBoard(workingState.Board.UpdateUnit(activeU1));
            }
            if (u2Stunned)
            {
                workingState = workingState.UpdateBoard(workingState.Board.UpdateUnit(activeU2));      
            }

            // Obliczamy obrażenia (0 jeśli Stunned)
            int dmgToU2 = u1Stunned ? 0 : context.DamageCalculator.CalculateFinalDamage(new DamageContext(activeU1, activeU2, activeU1.CurrentStats.Attack, DamageType.Combat));
            int dmgToU1 = u2Stunned ? 0 : context.DamageCalculator.CalculateFinalDamage(new DamageContext(activeU2, activeU1, activeU2.CurrentStats.Attack, DamageType.Combat));

            var nextU1 = activeU1.TakeDamage(dmgToU1);
            var nextU2 = activeU2.TakeDamage(dmgToU2);

            workingState = workingState.UpdateBoard(workingState.Board.UpdateUnit(nextU1));
            workingState = workingState.UpdateBoard(workingState.Board.UpdateUnit(nextU2));

            // Pobieramy aktualny stan z planszy po zmianach (HP/Statusy)
            var finalU1 = workingState.Board.Lines[lineIdx].Player1Unit;
            var finalU2 = workingState.Board.Lines[lineIdx].Player2Unit;

            if (finalU1 != null && finalU2 != null)
            {
                if (!u1Stunned) workingState = context.Keywords.ProcessPostAttack(workingState, finalU1, finalU2, lineIdx, context);
                if (!u2Stunned) workingState = context.Keywords.ProcessPostAttack(workingState, finalU2, finalU1, lineIdx, context);
            }

            if (dmgToU1 > 0) events.Publish(new UnitDamagedEvent(nextU1, dmgToU1, activeU2));
            if (dmgToU2 > 0) events.Publish(new UnitDamagedEvent(nextU2, dmgToU2, activeU1));

            return workingState;
        }

        public GameState ResolveBonusStrike(GameState state, CardInstance attacker, CardInstance? defender, int lineIdx, EventBus events, GameContext context)
        {
            if (attacker.CurrentStats.Keywords.Contains(Keyword.Stunned))
            {
                var cleanAttacker = attacker.SuppressKeyword(Keyword.Stunned);
                return state.UpdateBoard(state.Board.UpdateUnit(cleanAttacker));
            }

            var workingState = state;
            if (defender != null)
            {
                int dmg = context.DamageCalculator.CalculateFinalDamage(new DamageContext(attacker, defender, attacker.CurrentStats.Attack, DamageType.Combat));
                var nextDefender = defender.TakeDamage(dmg);
                workingState = workingState.UpdateBoard(workingState.Board.UpdateUnit(nextDefender));
                workingState = context.Keywords.ProcessPostAttack(workingState, attacker, nextDefender, lineIdx, context);
                events.Publish(new UnitDamagedEvent(nextDefender, dmg, attacker));
            }
            else
            {
                int dmgValue = attacker.CurrentStats.Attack;
                var opponent = workingState.GetOpponent(attacker.OwnerPlayerId);
                workingState = workingState.UpdatePlayer(opponent.WithDamageTaken(dmgValue));
                workingState = context.Keywords.ProcessPostAttack(workingState, attacker, null, lineIdx, context);

                // Atak na bohatera to specyficzny rodzaj obrażeń - nie celujemy w jednostkę.
                events.Publish(new UnitDamagedEvent(null!, dmgValue, attacker));
            }
            return _deathResolver.ResolveDeaths(workingState, events, context);
        }
    }
}