using System;
using System.Collections.Generic;
using CardGame.Core.Application;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Models;
using CardGame.Core.Events;
using CardGame.Core.GameRules.Damage;
using CardGame.Core.State.Models;

namespace CardGame.Core.Cards.Logic.Keywords.Handlers
{
    /// <summary>
    /// Handles the Armored keyword which reduces incoming combat damage by a fixed amount.
    /// </summary>
    public class ArmoredHandler : IKeywordHandler
    {
        /// <summary>
        /// Gets the keyword type this handler processes (Armored).
        /// </summary>
        public Keyword Type => Keyword.Armored;

        /// <summary>
        /// Reduces combat damage by the armor value (default 1 or from keyword parameters).
        /// Non-combat damage is unaffected.
        /// </summary>
        public int OnModifyDamageTaken(int amount, DamageContext context)
        {
            if (context.Type != DamageType.Combat) return amount;

            int armorValue = 1;
            if (context.Target.Definition.BaseStats.KeywordParams.TryGetValue(Keyword.Armored, out int value))
                armorValue = value;

            return Math.Max(0, amount - armorValue);
        }

        /// <summary>
        /// Armored has no special effects after attacking.
        /// </summary>
        public GameState OnAfterAttack(GameState state, CardInstance attacker, CardInstance? victim, int lineIndex, GameContext context) => state;

        /// <summary>
        /// Armored has no special effects at round end.
        /// </summary>
        public GameState OnRoundEnd(GameState state, CardInstance unit, GameContext context) => state;

        /// <summary>
        /// Armored does not prevent death.
        /// </summary>
        public bool OnPreventDeath(ref GameState state, CardInstance unit, GameContext context, bool isSacrifice) => false;
    }

    /// <summary>
    /// Handles the Marked keyword which doubles incoming damage to the unit.
    /// </summary>
    public class MarkedHandler : IKeywordHandler
    {
        /// <summary>
        /// Gets the keyword type this handler processes (Marked).
        /// </summary>
        public Keyword Type => Keyword.Marked;

        /// <summary>
        /// Doubles all incoming damage to the marked unit.
        /// </summary>
        public int OnModifyDamageTaken(int amount, DamageContext context) => amount * 2;

        /// <summary>
        /// Marked has no special effects after attacking.
        /// </summary>
        public GameState OnAfterAttack(GameState state, CardInstance attacker, CardInstance? victim, int lineIndex, GameContext context) => state;

        /// <summary>
        /// Marked has no special effects at round end.
        /// </summary>
        public GameState OnRoundEnd(GameState state, CardInstance unit, GameContext context) => state;

        /// <summary>
        /// Marked does not prevent death.
        /// </summary>
        public bool OnPreventDeath(ref GameState state, CardInstance unit, GameContext context, bool isSacrifice) => false;
    }

    /// <summary>
    /// Handles the SplashDamage keyword which deals damage to adjacent enemy units when attacking.
    /// </summary>
    public class SplashDamageHandler : IKeywordHandler
    {
        /// <summary>
        /// Gets the keyword type this handler processes (SplashDamage).
        /// </summary>
        public Keyword Type => Keyword.SplashDamage;

        /// <summary>
        /// Splash damage does not modify incoming damage.
        /// </summary>
        public int OnModifyDamageTaken(int amount, DamageContext context) => amount;

        /// <summary>
        /// Deals splash damage to enemy units in adjacent lines after attacking.
        /// </summary>
        public GameState OnAfterAttack(GameState state, CardInstance attacker, CardInstance? victim, int lineIndex, GameContext context)
        {
            if (!attacker.Definition.BaseStats.KeywordParams.TryGetValue(Keyword.SplashDamage, out int splashDamage))
                return state;

            var workingState = state;
            int opponentId = attacker.OwnerPlayerId == 1 ? 2 : 1;
            bool actsAsBurnSource = attacker.CurrentStats.Keywords.Contains(Keyword.BurnSource);

            foreach (int neighborLineIndex in new[] { lineIndex - 1, lineIndex + 1 })
            {
                if (neighborLineIndex >= 0 && neighborLineIndex < 4)
                {
                    var unit = workingState.Board.Lines[neighborLineIndex].IsSlotEmpty(opponentId)
                        ? null
                        : (opponentId == 1
                            ? workingState.Board.Lines[neighborLineIndex].Player1Unit
                            : workingState.Board.Lines[neighborLineIndex].Player2Unit);

                    if (unit != null)
                    {
                        var nextUnit = unit.TakeDamage(splashDamage);
                        if (actsAsBurnSource)
                        {
                            nextUnit = nextUnit.AddPermanentBuff(new CardStats(0, 0, 0, new[] { Keyword.Burning }));

                            context.Events.Publish(new StatusAppliedEvent(
                                attacker.OwnerPlayerId,
                                nextUnit.InstanceId,
                                Keyword.Burning,
                                attacker.InstanceId
                            ));
                        }
                        workingState = workingState.UpdateBoard(workingState.Board.UpdateUnit(nextUnit));
                        context.Events.Publish(new UnitDamagedEvent(nextUnit, splashDamage, attacker));
                    }
                }
            }
            return workingState;
        }

        /// <summary>
        /// Splash damage has no special effects at round end.
        /// </summary>
        public GameState OnRoundEnd(GameState state, CardInstance unit, GameContext context) => state;

        /// <summary>
        /// Splash damage does not prevent death.
        /// </summary>
        public bool OnPreventDeath(ref GameState state, CardInstance unit, GameContext context, bool isSacrifice) => false;
    }

    /// <summary>
    /// Handles the BurnSource keyword which applies the Burning status to attacked units.
    /// </summary>
    public class BurnSourceHandler : IKeywordHandler
    {
        /// <summary>
        /// Gets the keyword type this handler processes (BurnSource).
        /// </summary>
        public Keyword Type => Keyword.BurnSource;

        /// <summary>
        /// BurnSource does not modify incoming damage.
        /// </summary>
        public int OnModifyDamageTaken(int amount, DamageContext context) => amount;

        /// <summary>
        /// Applies the Burning keyword to the attacked unit.
        /// </summary>
        public GameState OnAfterAttack(GameState state, CardInstance attacker, CardInstance? victim, int lineIndex, GameContext context)
        {
            if (victim == null) return state;
            var burnedVictim = victim.AddPermanentBuff(new CardStats(0, 0, 0, new[] { Keyword.Burning }));
            return state.UpdateBoard(state.Board.UpdateUnit(burnedVictim));
        }

        /// <summary>
        /// BurnSource has no special effects at round end.
        /// </summary>
        public GameState OnRoundEnd(GameState state, CardInstance unit, GameContext context) => state;

        /// <summary>
        /// BurnSource does not prevent death.
        /// </summary>
        public bool OnPreventDeath(ref GameState state, CardInstance unit, GameContext context, bool isSacrifice) => false;
    }

    /// <summary>
    /// Handles the Burning keyword which deals 1 damage to the unit at the end of each round.
    /// </summary>
    public class BurningHandler : IKeywordHandler
    {
        /// <summary>
        /// Gets the keyword type this handler processes (Burning).
        /// </summary>
        public Keyword Type => Keyword.Burning;

        /// <summary>
        /// Burning does not modify incoming damage.
        /// </summary>
        public int OnModifyDamageTaken(int amount, DamageContext context) => amount;

        /// <summary>
        /// Burning has no special effects after attacking.
        /// </summary>
        public GameState OnAfterAttack(GameState state, CardInstance attacker, CardInstance? victim, int lineIndex, GameContext context) => state;

        /// <summary>
        /// Deals 1 damage to the burning unit at the end of the round.
        /// </summary>
        public GameState OnRoundEnd(GameState state, CardInstance unit, GameContext context)
        {
            var damagedUnit = unit.TakeDamage(1);
            context.Events.Publish(new UnitDamagedEvent(unit, 1, null));
            return state.UpdateBoard(state.Board.UpdateUnit(damagedUnit));
        }

        /// <summary>
        /// Burning does not prevent death.
        /// </summary>
        public bool OnPreventDeath(ref GameState state, CardInstance unit, GameContext context, bool isSacrifice) => false;
    }

    /// <summary>
    /// Handles the SoulGuard keyword which prevents death once per game (unless depleted or sacrificed).
    /// </summary>
    public class SoulGuardHandler : IKeywordHandler
    {
        /// <summary>
        /// Gets the keyword type this handler processes (SoulGuard).
        /// </summary>
        public Keyword Type => Keyword.SoulGuard;

        /// <summary>
        /// SoulGuard does not modify incoming damage.
        /// </summary>
        public int OnModifyDamageTaken(int amount, DamageContext context) => amount;

        /// <summary>
        /// SoulGuard has no special effects after attacking.
        /// </summary>
        public GameState OnAfterAttack(GameState state, CardInstance attacker, CardInstance? victim, int lineIndex, GameContext context) => state;

        /// <summary>
        /// SoulGuard has no special effects at round end.
        /// </summary>
        public GameState OnRoundEnd(GameState state, CardInstance unit, GameContext context) => state;

        /// <summary>
        /// Prevents death by setting unit to 1 HP and applying the SoulGuardDepleted keyword.
        /// Does not work against sacrifice effects.
        /// </summary>
        public bool OnPreventDeath(ref GameState state, CardInstance unit, GameContext context, bool isSacrifice)
        {
            if (isSacrifice) return false;
            if (unit.CurrentStats.Keywords.Contains(Keyword.SoulGuardDepleted)) return false;

            int damageToSet = Math.Max(0, unit.MaxHealth - 1);
            var survivedUnit = unit.WithDamage(damageToSet).AddPermanentBuff(new CardStats(0, 0, 0, new[] { Keyword.SoulGuardDepleted }));
            state = state.UpdateBoard(state.Board.UpdateUnit(survivedUnit));
            return true;
        }
    }

    /// <summary>
    /// Handles the Unkillable keyword which returns the unit to hand instead of dying.
    /// </summary>
    public class UnkillableHandler : IKeywordHandler
    {
        /// <summary>
        /// Gets the keyword type this handler processes (Unkillable).
        /// </summary>
        public Keyword Type => Keyword.Unkillable;

        /// <summary>
        /// Unkillable does not modify incoming damage.
        /// </summary>
        public int OnModifyDamageTaken(int amount, DamageContext context) => amount;

        /// <summary>
        /// Unkillable has no special effects after attacking.
        /// </summary>
        public GameState OnAfterAttack(GameState state, CardInstance attacker, CardInstance? victim, int lineIndex, GameContext context) => state;

        /// <summary>
        /// Unkillable has no special effects at round end.
        /// </summary>
        public GameState OnRoundEnd(GameState state, CardInstance unit, GameContext context) => state;

        /// <summary>
        /// Prevents death by returning the unit to the owner's hand.
        /// Silenced units cannot use this effect.
        /// </summary>
        public bool OnPreventDeath(ref GameState state, CardInstance unit, GameContext context, bool isSacrifice)
        {
            if (unit.IsSilenced) return false;

            context.Events.Publish(new TriggerActivatedEvent(unit.InstanceId, unit.OwnerPlayerId));

            var board = state.Board;
            int lineIndex = -1;

            for (int i = 0; i < 4; i++)
            {
                if (board.Lines[i].Player1Unit?.InstanceId == unit.InstanceId)
                {
                    lineIndex = i;
                    board = board.WithUnitPlacedAt(i, 1, null);
                    break;
                }
                if (board.Lines[i].Player2Unit?.InstanceId == unit.InstanceId)
                {
                    lineIndex = i;
                    board = board.WithUnitPlacedAt(i, 2, null);
                    break;
                }
            }

            bool wasGraftedUnkillable = unit.CurrentStats.Keywords.Contains(Keyword.Unkillable) &&
                                        !unit.Definition.Keywords.Contains(Keyword.Unkillable);

            var returnedCard = unit.MoveAndReset();

            if (wasGraftedUnkillable)
            {
                var statsWithUnkillable = new CardStats(0, 0, 0, new[] { Keyword.Unkillable });
                returnedCard = returnedCard.AddPermanentBuff(statsWithUnkillable);
            }

            int playerId = unit.OwnerPlayerId;
            var owner = state.GetPlayer(playerId);

            context.Events.Publish(new CardMovedEvent(unit.InstanceId, playerId, CardZone.Board, CardZone.Hand, lineIndex));

            state = state.UpdateBoard(board).UpdatePlayer(owner.WithCardAddedToHand(returnedCard));

            return true;
        }
    }

    /// <summary>
    /// Handles the Stunned keyword which prevents a unit from attacking on its next turn.
    /// </summary>
    public class StunnedHandler : IKeywordHandler
    {
        /// <summary>
        /// Gets the keyword type this handler processes (Stunned).
        /// </summary>
        public Keyword Type => Keyword.Stunned;

        /// <summary>
        /// Stunned does not modify incoming damage.
        /// </summary>
        public int OnModifyDamageTaken(int amount, DamageContext context) => amount;

        /// <summary>
        /// Stunned has no special effects after attacking.
        /// </summary>
        public GameState OnAfterAttack(GameState state, CardInstance attacker, CardInstance? victim, int lineIndex, GameContext context) => state;

        /// <summary>
        /// Stunned has no special effects at round end.
        /// </summary>
        public GameState OnRoundEnd(GameState state, CardInstance unit, GameContext context) => state;

        /// <summary>
        /// Stunned does not prevent death.
        /// </summary>
        public bool OnPreventDeath(ref GameState state, CardInstance unit, GameContext context, bool isSacrifice) => false;
    }
}