using System;
using System.Collections.Generic;
using System.Linq;
using CardGame.Core.Application;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Models;
using CardGame.Core.Events;
using CardGame.Core.GameRules.Damage;
using CardGame.Core.State.Models;

namespace CardGame.Core.Cards.Logic.Keywords.Handlers
{
    public class ArmoredHandler : IKeywordHandler
    {
        public Keyword Type => Keyword.Armored;
        public int OnModifyDamageTaken(int amount, DamageContext context)
        {
            if (context.Type != DamageType.Combat) return amount;
            int armorValue = 1;
            if (context.Target.Definition.BaseStats.KeywordParams.TryGetValue(Keyword.Armored, out int val))
                armorValue = val;
            return Math.Max(0, amount - armorValue);
        }
        public GameState OnAfterAttack(GameState s, CardInstance a, CardInstance? v, int l, GameContext c) => s;
        public GameState OnRoundEnd(GameState s, CardInstance u, GameContext c) => s;
        public bool OnPreventDeath(ref GameState state, CardInstance unit, GameContext context, bool isSacrifice) => false;
    }

    public class MarkedHandler : IKeywordHandler
    {
        public Keyword Type => Keyword.Marked;
        public int OnModifyDamageTaken(int amount, DamageContext context) => amount * 2;
        public GameState OnAfterAttack(GameState s, CardInstance a, CardInstance? v, int l, GameContext c) => s;
        public GameState OnRoundEnd(GameState s, CardInstance u, GameContext c) => s;
        public bool OnPreventDeath(ref GameState s, CardInstance u, GameContext c, bool isSacrifice) => false;
    }

    public class SplashDamageHandler : IKeywordHandler
    {
        public Keyword Type => Keyword.SplashDamage;
        public int OnModifyDamageTaken(int a, DamageContext c) => a;
        public GameState OnAfterAttack(GameState state, CardInstance attacker, CardInstance? victim, int lineIdx, GameContext context)
        {
            if (!attacker.Definition.BaseStats.KeywordParams.TryGetValue(Keyword.SplashDamage, out int dmg)) return state;
            var workingState = state;
            int opponentId = attacker.OwnerPlayerId == 1 ? 2 : 1;
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
                        context.Events.Publish(new UnitDamagedEvent(nextUnit, dmg, attacker));
                    }
                }
            }
            return workingState;
        }
        public GameState OnRoundEnd(GameState s, CardInstance u, GameContext c) => s;
        public bool OnPreventDeath(ref GameState s, CardInstance u, GameContext c, bool isSacrifice) => false;
    }

    public class BurnSourceHandler : IKeywordHandler
    {
        public Keyword Type => Keyword.BurnSource;
        public int OnModifyDamageTaken(int a, DamageContext c) => a;
        public GameState OnAfterAttack(GameState state, CardInstance attacker, CardInstance? victim, int lineIdx, GameContext context)
        {
            if (victim == null) return state;
            var burnedVictim = victim.AddPermanentBuff(new CardStats(0, 0, 0, new[] { Keyword.Burning }));
            return state.UpdateBoard(state.Board.UpdateUnit(burnedVictim));
        }
        public GameState OnRoundEnd(GameState s, CardInstance u, GameContext c) => s;
        public bool OnPreventDeath(ref GameState s, CardInstance u, GameContext c, bool isSacrifice) => false;
    }

    public class BurningHandler : IKeywordHandler
    {
        public Keyword Type => Keyword.Burning;
        public int OnModifyDamageTaken(int a, DamageContext c) => a;
        public GameState OnAfterAttack(GameState s, CardInstance a, CardInstance? v, int l, GameContext c) => s;
        public GameState OnRoundEnd(GameState state, CardInstance unit, GameContext context)
        {
            var damagedUnit = unit.TakeDamage(1);
            context.Events.Publish(new UnitDamagedEvent(unit, 1, null));
            return state.UpdateBoard(state.Board.UpdateUnit(damagedUnit));
        }
        public bool OnPreventDeath(ref GameState s, CardInstance u, GameContext c, bool isSacrifice) => false;
    }

    public class SoulGuardHandler : IKeywordHandler
    {
        public Keyword Type => Keyword.SoulGuard;
        public int OnModifyDamageTaken(int a, DamageContext c) => a;
        public GameState OnAfterAttack(GameState s, CardInstance a, CardInstance? v, int l, GameContext c) => s;
        public GameState OnRoundEnd(GameState s, CardInstance u, GameContext c) => s;

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

    public class UnkillableHandler : IKeywordHandler
    {
        public Keyword Type => Keyword.Unkillable;
        public int OnModifyDamageTaken(int a, DamageContext c) => a;
        public GameState OnAfterAttack(GameState s, CardInstance a, CardInstance? v, int l, GameContext c) => s;
        public GameState OnRoundEnd(GameState s, CardInstance u, GameContext c) => s;

        public bool OnPreventDeath(ref GameState state, CardInstance unit, GameContext context, bool isSacrifice)
        {
            // 1. Logic Check: If the card is silenced, Unkillable shouldn't work 
            // (KeywordProcessor usually handles this, but we'll be safe)
            if (unit.IsSilenced) return false;

            context.Events.Publish(new TriggerActivatedEvent(unit.InstanceId, unit.OwnerPlayerId));

            var board = state.Board;
            int lineIndex = -1;

            // 2. Remove from Board
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

            // 3. Prepare the card for hand
            // MoveAndReset wipes PermanentBuffs, so we must detect if Unkillable was one of them
            bool wasGraftedUnkillable = unit.CurrentStats.Keywords.Contains(Keyword.Unkillable) &&
                                        !unit.Definition.Keywords.Contains(Keyword.Unkillable);

            var returnedCard = unit.MoveAndReset();

            if (wasGraftedUnkillable)
            {
                // Re-apply the Unkillable status so it stays for the NEXT time it's played
                var statsWithUnkillable = new CardStats(0, 0, 0, new[] { Keyword.Unkillable });
                returnedCard = returnedCard.AddPermanentBuff(statsWithUnkillable);
            }

            // 4. Update State
            int pid = unit.OwnerPlayerId;
            var owner = state.GetPlayer(pid);

            // UI Event
            context.Events.Publish(new CardMovedEvent(unit.InstanceId, pid, CardZone.Board, CardZone.Hand, lineIndex));

            // Use the updated player state (which now handles full hands by discarding)
            state = state.UpdateBoard(board).UpdatePlayer(owner.WithCardAddedToHand(returnedCard));

            return true;
        }
    }

    public class StunnedHandler : IKeywordHandler
    {
        public Keyword Type => Keyword.Stunned;
        public int OnModifyDamageTaken(int a, DamageContext c) => a;
        public GameState OnAfterAttack(GameState s, CardInstance a, CardInstance? v, int l, GameContext c) => s;
        public GameState OnRoundEnd(GameState s, CardInstance u, GameContext c) => s;
        public bool OnPreventDeath(ref GameState s, CardInstance u, GameContext c, bool isSacrifice) => false;
    }
}