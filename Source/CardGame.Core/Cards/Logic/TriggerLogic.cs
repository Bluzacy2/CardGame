using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Models;
using CardGame.Core.Events;
using CardGame.Core.Events.Interfaces;
using CardGame.Core.State.Models;
using System.Linq;

namespace CardGame.Core.Cards.Logic
{
    public static class TriggerLogic
    {
        public static bool Check(EffectData effect, IGameEvent gameEvent, GameState state, int sourceCardId)
        {
            var source = state.Board.GetAllUnits().FirstOrDefault(u => u.InstanceId == sourceCardId)
                      ?? state.PlayerA.Hand.Concat(state.PlayerB.Hand).FirstOrDefault(c => c.InstanceId == sourceCardId)
                      ?? state.SpellStack.FirstOrDefault(s => s.InstanceId == sourceCardId);

            if (source == null) return false;
            if (source.IsSilenced && effect.Trigger != TriggerType.OnPlayed) return false;

            if (effect.Condition != null && !EvaluateCondition(effect.Condition, gameEvent, state, source))
                return false;

            int myOwnerId = source.OwnerPlayerId;

            switch (effect.Trigger)
            {
                case TriggerType.OnPlayed:
                    return gameEvent is CardPlayedEvent cpe && cpe.Card.InstanceId == sourceCardId;

                case TriggerType.OnDeath:
                    return gameEvent is UnitDiedEvent ude && ude.Unit.InstanceId == sourceCardId;

                case TriggerType.OnKill:
                    return gameEvent is UnitDiedEvent uk && uk.KillerInstanceId == sourceCardId;

                case TriggerType.OnSacrificed:
                    return gameEvent is UnitSacrificedEvent use && use.Unit.InstanceId == sourceCardId;

                case TriggerType.OnFriendlyUnitDied:
                    if (gameEvent is UnitDiedEvent fde) return fde.Unit.InstanceId != sourceCardId && fde.OwnerId == myOwnerId;
                    if (gameEvent is UnitSacrificedEvent use2) return use2.Unit.InstanceId != sourceCardId && use2.OwnerId == myOwnerId;
                    return false;

                case TriggerType.OnOtherUnitSacrificed:
                    return gameEvent is UnitSacrificedEvent ose && ose.Unit.InstanceId != sourceCardId && ose.OwnerId == myOwnerId;

                case TriggerType.OnDamagedEnemyHero:
                    return gameEvent is UnitDamagedEvent heroDmg && heroDmg.Source != null && heroDmg.Source.OwnerPlayerId == myOwnerId && heroDmg.Unit == null;

                case TriggerType.OnDamagedEnemyUnit:
                    return gameEvent is UnitDamagedEvent unitDmg && unitDmg.Source?.InstanceId == sourceCardId && unitDmg.Unit != null && unitDmg.Unit.OwnerPlayerId != myOwnerId;

                case TriggerType.OnOpponentCardDrawn:
                    return gameEvent is CardDrawnEvent od && od.PlayerId != myOwnerId;

                case TriggerType.OnFriendlyCardDrawn:
                    return gameEvent is CardDrawnEvent fd && fd.PlayerId == myOwnerId;

                case TriggerType.OnPreCombatLine:
                    return gameEvent is PreLineCombatEvent ple && state.Board.Lines[ple.LineIndex].GetAllUnits().Any(u => u.InstanceId == sourceCardId);

                case TriggerType.OnStatusApplied:
                    return gameEvent is StatusAppliedEvent;

                case TriggerType.OnDamagTaken:
                    return gameEvent is UnitDamagedEvent udt && udt.Unit?.InstanceId == sourceCardId;

                default:
                    return false;
            }
        }

        private static bool EvaluateCondition(ConditionData cond, IGameEvent gameEvent, GameState state, CardInstance source)
        {
            int myOwnerId = source.OwnerPlayerId;

            switch (cond.Condition)
            {
                case ConditionType.And:
                    return cond.SubConditions.All(c => EvaluateCondition(c, gameEvent, state, source));

                case ConditionType.Or:
                    return cond.SubConditions.Any(c => EvaluateCondition(c, gameEvent, state, source));

                case ConditionType.IsStatus:
                    return gameEvent is StatusAppliedEvent sae &&
                           string.Equals(sae.Status.ToString(), cond.TargetParam, System.StringComparison.OrdinalIgnoreCase);

                case ConditionType.IsEnemy:
                    if (gameEvent is StatusAppliedEvent sae2)
                    {
                        var targetUnit = state.Board.GetAllUnits().FirstOrDefault(u => u.InstanceId == sae2.TargetUnitId);
                        return targetUnit != null && targetUnit.OwnerPlayerId != myOwnerId;
                    }
                    return gameEvent.SourcePlayerId != myOwnerId;

                case ConditionType.IsSelf:
                    int actorId = -1;
                    if (gameEvent is UnitDamagedEvent ude) actorId = ude.Source?.InstanceId ?? -1;
                    else if (gameEvent is UnitDiedEvent udied) actorId = (cond.ValueParam == 1) ? udied.KillerInstanceId ?? -1 : udied.Unit.InstanceId;
                    else if (gameEvent is CardPlayedEvent cpe) actorId = cpe.Card.InstanceId;
                    else if (gameEvent is UnitSacrificedEvent use) actorId = use.Unit.InstanceId;
                    return actorId == source.InstanceId;

                case ConditionType.IsSubtype:
                    CardInstance? subject = null;
                    if (gameEvent is UnitDamagedEvent ude2) subject = ude2.Source;
                    else if (gameEvent is UnitDiedEvent ud) subject = ud.Unit;
                    else if (gameEvent is CardPlayedEvent cp) subject = cp.Card;
                    return subject != null && subject.Definition.Subtypes.Contains(cond.TargetParam);

                default:
                    return true;
            }
        }
    }
}