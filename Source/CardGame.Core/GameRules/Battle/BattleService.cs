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

            // 1. Flying Logic: If one unit is flying and the other is not, they ignore each other and strike heroes.
            bool unit1Flying = unit1.CurrentStats.Keywords.Contains(Keyword.Flying);
            bool unit2Flying = unit2.CurrentStats.Keywords.Contains(Keyword.Flying);

            if (unit1Flying != unit2Flying)
            {
                workingState = ResolveBonusStrike(workingState, unit1, null, lineIndex, events, context);
                workingState = ResolveBonusStrike(workingState, unit2, null, lineIndex, events, context);
                return workingState;
            }

            // 2. Capability Check: A unit can only perform a "Strike" if it has > 0 Attack and is NOT Stunned.
            bool canUnit1Strike = !unit1.CurrentStats.Keywords.Contains(Keyword.Stunned) && unit1.CurrentStats.Attack > 0;
            bool canUnit2Strike = !unit2.CurrentStats.Keywords.Contains(Keyword.Stunned) && unit2.CurrentStats.Attack > 0;

            // 3. Stun Management: Handle Stun removal. 
            // Note: Stun is consumed (removed) even if the unit has 0 Attack, as they "spent" their turn waiting.
            var activeUnit1 = unit1.CurrentStats.Keywords.Contains(Keyword.Stunned) ? unit1.SuppressKeyword(Keyword.Stunned) : unit1;
            var activeUnit2 = unit2.CurrentStats.Keywords.Contains(Keyword.Stunned) ? unit2.SuppressKeyword(Keyword.Stunned) : unit2;

            // Update board state if keywords changed due to stun suppression
            if (unit1.CurrentStats.Keywords.Contains(Keyword.Stunned))
                workingState = workingState.UpdateBoard(workingState.Board.UpdateUnit(activeUnit1));
            if (unit2.CurrentStats.Keywords.Contains(Keyword.Stunned))
                workingState = workingState.UpdateBoard(workingState.Board.UpdateUnit(activeUnit2));

            // 4. Calculate Damage: Damage is 0 if the unit cannot strike.
            int damageToUnit2 = canUnit1Strike
                ? context.DamageCalculator.CalculateFinalDamage(new DamageContext(activeUnit1, activeUnit2, activeUnit1.CurrentStats.Attack, DamageType.Combat))
                : 0;

            int damageToUnit1 = canUnit2Strike
                ? context.DamageCalculator.CalculateFinalDamage(new DamageContext(activeUnit2, activeUnit1, activeUnit2.CurrentStats.Attack, DamageType.Combat))
                : 0;

            // 5. Publish Clash Events: Only published if the unit actually performs a strike.
            if (canUnit1Strike) events.Publish(new BattleClashEvent(unit1.InstanceId, unit2.InstanceId));
            if (canUnit2Strike) events.Publish(new BattleClashEvent(unit2.InstanceId, unit1.InstanceId));

            // 6. Apply Damage to both units.
            var nextUnit1 = activeUnit1.TakeDamage(damageToUnit1);
            var nextUnit2 = activeUnit2.TakeDamage(damageToUnit2);

            workingState = workingState.UpdateBoard(workingState.Board.UpdateUnit(nextUnit1));
            workingState = workingState.UpdateBoard(workingState.Board.UpdateUnit(nextUnit2));

            // 7. Post-Attack Keywords (SplashDamage, BurnSource, etc.):
            // These only trigger if the unit performed a valid strike (Atk > 0).
            var finalUnit1 = workingState.Board.Lines[lineIndex].Player1Unit;
            var finalUnit2 = workingState.Board.Lines[lineIndex].Player2Unit;

            if (finalUnit1 != null && finalUnit2 != null)
            {
                if (canUnit1Strike)
                {
                    workingState = context.Keywords.ProcessPostAttack(workingState, finalUnit1, finalUnit2, lineIndex, context);
                    // Refresh references in case keywords modified the board
                    finalUnit1 = workingState.Board.Lines[lineIndex].Player1Unit;
                    finalUnit2 = workingState.Board.Lines[lineIndex].Player2Unit;
                }

                if (canUnit2Strike && finalUnit1 != null && finalUnit2 != null)
                {
                    workingState = context.Keywords.ProcessPostAttack(workingState, finalUnit2, finalUnit1, lineIndex, context);
                }
            }

            // 8. Publish Damage Events for UI/Logs.
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
            if (attacker.CurrentStats.Attack <= 0) return state;

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