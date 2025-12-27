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
            int ownerId = contextEvent.SourcePlayerId; // Pobieramy bezpośrednio z eventu!
            int opponentId = (ownerId == 1) ? 2 : 1;

            switch (type)
            {
                case TargetType.SelectedTarget:
                case TargetType.TargetEnemyUnit:
                case TargetType.TargetFriendlyUnit:
                    int? targetId = (contextEvent is CardPlayedEvent cpe) ? cpe.SelectedTargetId : (contextEvent is TargetSelectedEvent tse) ? tse.SelectedTargetId : null;
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

                case TargetType.FriendlyHero:
                case TargetType.Choice:
                case TargetType.Self when type == TargetType.Choice:
                    result.TargetPlayer = state.GetPlayer(ownerId);
                    break;

                case TargetType.EnemyHero:
                    result.TargetPlayer = state.GetPlayer(opponentId);
                    break;

                case TargetType.AllUnitsOnBoard:
                    result.UnitTargets.AddRange(state.Board.GetAllUnits());
                    break;

                case TargetType.AllEnemyUnits:
                    result.UnitTargets.AddRange(state.Board.GetAllUnits().Where(u => u.OwnerPlayerId == opponentId));
                    break;

                case TargetType.AllFriendlyUnits:
                    result.UnitTargets.AddRange(state.Board.GetAllUnits().Where(u => u.OwnerPlayerId == ownerId));
                    break;

                case TargetType.Self:
                    var self = state.Board.GetAllUnits().FirstOrDefault(u => u.InstanceId == sourceCardId) ??
                               state.GetPlayer(ownerId).Hand.FirstOrDefault(c => c.InstanceId == sourceCardId) ??
                               state.SpellStack.FirstOrDefault(s => s.InstanceId == sourceCardId);

                    if (self != null) result.UnitTargets.Add(self);
                    result.TargetPlayer = state.GetPlayer(ownerId);
                    break;
            }
            return result;
        }
    }
}