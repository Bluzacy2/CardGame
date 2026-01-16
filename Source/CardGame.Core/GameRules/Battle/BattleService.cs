using CardGame.Core.Application;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Models;
using CardGame.Core.Events;
using CardGame.Core.GameRules.Damage;
using CardGame.Core.GameRules.Death;
using CardGame.Core.State.Models;
using System;
using System.Linq;

namespace CardGame.Core.GameRules.Battle
{
    /// <summary>
    /// Handles combat resolution between units, including damage calculation, flying mechanics, and stun effects.
    /// </summary>
    public class BattleService
    {
        #region Private Fields
        private readonly DeathResolver _deathResolver = new DeathResolver();
        #endregion

        #region Public Methods
        /// <summary>
        /// Resolves combat between two units on the same line, applying damage and status effects.
        /// </summary>
        /// <param name="state">The current game state.</param>
        /// <param name="unit1">The first unit in the combat.</param>
        /// <param name="unit2">The second unit in the combat.</param>
        /// <param name="lineIndex">The index of the battle line.</param>
        /// <param name="events">Event bus for publishing combat events.</param>
        /// <param name="context">Game context for damage calculation and keyword processing.</param>
        /// <returns>The updated game state after resolving the combat duel.</returns>
        public GameState ResolveCombatDuel(GameState state, CardInstance unit1, CardInstance unit2, int lineIndex, EventBus events, GameContext context)
        {
            var workingState = state;
            bool unit1Flying = unit1.CurrentStats.Keywords.Contains(Keyword.Flying);
            bool unit2Flying = unit2.CurrentStats.Keywords.Contains(Keyword.Flying);

            // Flying Logic: If one unit is flying and the other is not, they ignore each other and strike heroes.
            if (unit1Flying != unit2Flying)
            {
                workingState = ResolveBonusStrike(workingState, unit1, null, lineIndex, events, context);
                workingState = ResolveBonusStrike(workingState, unit2, null, lineIndex, events, context);
                return workingState;
            }

            bool unit1Stunned = unit1.CurrentStats.Keywords.Contains(Keyword.Stunned);
            bool unit2Stunned = unit2.CurrentStats.Keywords.Contains(Keyword.Stunned);

            // Handle Stun removal
            var activeUnit1 = unit1Stunned ? unit1.SuppressKeyword(Keyword.Stunned) : unit1;
            var activeUnit2 = unit2Stunned ? unit2.SuppressKeyword(Keyword.Stunned) : unit2;

            if (unit1Stunned) workingState = workingState.UpdateBoard(workingState.Board.UpdateUnit(activeUnit1));
            if (unit2Stunned) workingState = workingState.UpdateBoard(workingState.Board.UpdateUnit(activeUnit2));

            // Calculate Damage (0 if Stunned)
            int damageToUnit2 = unit1Stunned ? 0 : context.DamageCalculator.CalculateFinalDamage(new DamageContext(activeUnit1, activeUnit2, activeUnit1.CurrentStats.Attack, DamageType.Combat));
            int damageToUnit1 = unit2Stunned ? 0 : context.DamageCalculator.CalculateFinalDamage(new DamageContext(activeUnit2, activeUnit1, activeUnit2.CurrentStats.Attack, DamageType.Combat));

            events.Publish(new BattleClashEvent(unit1.InstanceId, unit2.InstanceId));
            events.Publish(new BattleClashEvent(unit2.InstanceId, unit1.InstanceId));

            var nextUnit1 = activeUnit1.TakeDamage(damageToUnit1);
            var nextUnit2 = activeUnit2.TakeDamage(damageToUnit2);

            workingState = workingState.UpdateBoard(workingState.Board.UpdateUnit(nextUnit1));
            workingState = workingState.UpdateBoard(workingState.Board.UpdateUnit(nextUnit2));

            // Resolve Post-Attack keywords (like Splash or BurnSource)
            var finalUnit1 = workingState.Board.Lines[lineIndex].Player1Unit;
            var finalUnit2 = workingState.Board.Lines[lineIndex].Player2Unit;

            if (finalUnit1 != null && finalUnit2 != null)
            {
                if (!unit1Stunned) workingState = context.Keywords.ProcessPostAttack(workingState, finalUnit1, finalUnit2, lineIndex, context);
                if (!unit2Stunned) workingState = context.Keywords.ProcessPostAttack(workingState, finalUnit2, finalUnit1, lineIndex, context);
            }

            // Publish Events using the "Hybrid" constructor for UI support
            if (damageToUnit1 > 0)
                events.Publish(new UnitDamagedEvent(nextUnit1, damageToUnit1, activeUnit2, nextUnit1.CurrentStats.Health));

            if (damageToUnit2 > 0)
                events.Publish(new UnitDamagedEvent(nextUnit2, damageToUnit2, activeUnit1, nextUnit2.CurrentStats.Health));

            return workingState;
        }

        /// <summary>
        /// Resolves a bonus strike from a unit to either another unit or the opponent's hero.
        /// </summary>
        /// <param name="state">The current game state.</param>
        /// <param name="attacker">The unit making the bonus attack.</param>
        /// <param name="defender">The target unit, or null to attack the hero.</param>
        /// <param name="lineIndex">The index of the battle line.</param>
        /// <param name="events">Event bus for publishing combat events.</param>
        /// <param name="context">Game context for damage calculation and keyword processing.</param>
        /// <returns>The updated game state after resolving the bonus strike.</returns>
        public GameState ResolveBonusStrike(GameState state, CardInstance attacker, CardInstance? defender, int lineIndex, EventBus events, GameContext context)
        {
            // Handle Stunned check
            if (attacker.CurrentStats.Keywords.Contains(Keyword.Stunned))
            {
                var cleanAttacker = attacker.SuppressKeyword(Keyword.Stunned);
                return state.UpdateBoard(state.Board.UpdateUnit(cleanAttacker));
            }

            var workingState = state;

            // Flying Logic: Bonus attacks are also blocked/ignored by flying mismatch
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
                int damage = context.DamageCalculator.CalculateFinalDamage(new DamageContext(attacker, defender, attacker.CurrentStats.Attack, DamageType.Combat));
                var nextDefender = defender.TakeDamage(damage);
                workingState = workingState.UpdateBoard(workingState.Board.UpdateUnit(nextDefender));
                workingState = context.Keywords.ProcessPostAttack(workingState, attacker, nextDefender, lineIndex, context);

                // Publish Unit Damage
                events.Publish(new UnitDamagedEvent(nextDefender, damage, attacker, nextDefender.CurrentStats.Health));
            }
            else
            {
                // Damage to Hero
                int damageValue = attacker.CurrentStats.Attack;
                var opponent = workingState.GetOpponent(attacker.OwnerPlayerId);
                workingState = workingState.UpdatePlayer(opponent.WithDamageTaken(damageValue));
                workingState = context.Keywords.ProcessPostAttack(workingState, attacker, null, lineIndex, context);

                // Publish Hero Damage
                events.Publish(new UnitDamagedEvent(null, damageValue, attacker, opponent.Health - damageValue));
            }

            // Check for deaths after the strike
            return _deathResolver.ResolveDeaths(workingState, events, context);
        }
        #endregion
    }
}