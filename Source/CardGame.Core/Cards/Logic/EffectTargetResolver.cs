using System;
using System.Linq;
using CardGame.Core.Cards.Data;
using CardGame.Core.Events;
using CardGame.Core.Cards.Models;
using System.Collections.Generic;
using CardGame.Core.Events.Interfaces;
using CardGame.Core.State.Models;

namespace CardGame.Core.Cards.Logic
{
    public class EffectTargets
    {
        public PlayerState? TargetPlayer;
        public List<CardInstance> UnitTargets { get; set; } = new();
        public CardInstance? TargetUnit => UnitTargets.FirstOrDefault();
    }

    public static class EffectTargetResolver
    {
        public static EffectTargets Resolve(TargetType type, GameState state, IGameEvent contextEvent, int sourceCardId)
        {
            var result = new EffectTargets();
            int ownerId = GetOwner(state, sourceCardId);
            int opponentId = (ownerId == 1) ? 2 : 1;

            switch (type)
            {
                case TargetType.SelectedTarget:
                case TargetType.TargetEnemyUnit:
                case TargetType.TargetFriendlyUnit:
                    int? targetId = null;

                    if (contextEvent is CardPlayedEvent cpe) targetId = cpe.SelectedTargetId;
                    else if (contextEvent is TargetSelectedEvent tse) targetId = tse.SelectedTargetId;
                    else if (contextEvent is StatusAppliedEvent sae) targetId = sae.TargetUnitId;
                    else if (contextEvent is UnitDamagedEvent ude)
                    {
                    
                        targetId = ude.Unit?.InstanceId ?? ude.Source?.InstanceId;
                    }
                    else if (contextEvent is UnitDiedEvent udied) targetId = udied.Unit.InstanceId;

                    if (targetId.HasValue)
                    {
                        var tUnit = state.Board.GetAllUnits().FirstOrDefault(u => u.InstanceId == targetId.Value);
                        if (tUnit != null)
                        {
                            bool isValid = true;
                            if (type == TargetType.TargetEnemyUnit && tUnit.OwnerPlayerId != opponentId) isValid = false;
                            if (type == TargetType.TargetFriendlyUnit && tUnit.OwnerPlayerId != ownerId) isValid = false;
                            if (isValid) result.UnitTargets.Add(tUnit);
                        }
                    }
                    break;

                case TargetType.FriendlyHero: result.TargetPlayer = state.GetPlayer(ownerId); break;
                case TargetType.EnemyHero: result.TargetPlayer = state.GetPlayer(opponentId); break;
                case TargetType.Choice: result.TargetPlayer = state.GetPlayer(ownerId); break;
                case TargetType.AllUnitsOnBoard: result.UnitTargets.AddRange(state.Board.GetAllUnits()); break;
                case TargetType.AllEnemyUnits: result.UnitTargets.AddRange(state.Board.GetAllUnits().Where(u => u.OwnerPlayerId == opponentId)); break;
                case TargetType.AllFriendlyUnits: result.UnitTargets.AddRange(state.Board.GetAllUnits().Where(u => u.OwnerPlayerId == ownerId)); break;
                case TargetType.OtherFriendlyUnits: result.UnitTargets.AddRange(state.Board.GetAllUnits().Where(u => u.OwnerPlayerId == ownerId && u.InstanceId != sourceCardId)); break;
                case TargetType.Self:
                    var self = state.Board.GetAllUnits().FirstOrDefault(u => u.InstanceId == sourceCardId) ??
                               state.PlayerA.Hand.Concat(state.PlayerB.Hand).FirstOrDefault(c => c.InstanceId == sourceCardId) ??
                               state.SpellStack.FirstOrDefault(s => s.InstanceId == sourceCardId) ??
                               state.PlayerA.DiscardPile.Concat(state.PlayerB.DiscardPile).FirstOrDefault(c => c.InstanceId == sourceCardId);
                    if (self != null) result.UnitTargets.Add(self);
                    result.TargetPlayer = state.GetPlayer(ownerId);
                    break;
            }
            return result;
        }

        public static List<CardInstance> GetPotentialTargets(TargetType type, GameState state, int sourceCardId)
        {
            int ownerId = GetOwner(state, sourceCardId);
            int opponentId = (ownerId == 1) ? 2 : 1;
            var allUnits = state.Board.GetAllUnits();

            return type switch
            {
                TargetType.TargetEnemyUnit => allUnits.Where(u => u.OwnerPlayerId == opponentId).ToList(),
                TargetType.TargetFriendlyUnit => allUnits.Where(u => u.OwnerPlayerId == ownerId).ToList(),
                TargetType.SelectedTarget => allUnits.ToList(),
                TargetType.OtherFriendlyUnits => allUnits.Where(u => u.OwnerPlayerId == ownerId && u.InstanceId != sourceCardId).ToList(),
                _ => new List<CardInstance>()
            };
        }

        private static int GetOwner(GameState state, int instanceId)
        {
          
            var unit = state.Board.GetAllUnits().FirstOrDefault(x => x.InstanceId == instanceId);
            if (unit != null) return unit.OwnerPlayerId;

          
            var spell = state.SpellStack.FirstOrDefault(x => x.InstanceId == instanceId);
            if (spell != null) return spell.OwnerPlayerId;

          
            if (state.PlayerA.Hand.Any(x => x.InstanceId == instanceId)) return 1;
            if (state.PlayerB.Hand.Any(x => x.InstanceId == instanceId)) return 2;

           
            if (state.PlayerA.DiscardPile.Any(x => x.InstanceId == instanceId)) return 1;
            if (state.PlayerB.DiscardPile.Any(x => x.InstanceId == instanceId)) return 2;

            return state.ActivePlayerId;
        }
    }
}