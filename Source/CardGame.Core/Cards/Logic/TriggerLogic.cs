using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Models;
using CardGame.Core.Events;
using CardGame.Core.Events.Interfaces;
using CardGame.Core.State.Models;
using System;
using System.Linq;

namespace CardGame.Core.Cards.Logic
{
    /// <summary>
    /// Contains logic for evaluating whether card effects should trigger based on game events and conditions.
    /// </summary>
    public static class TriggerLogic
    {
        #region Public Methods
        /// <summary>
        /// Checks whether a specific effect should trigger based on the current game event and state.
        /// </summary>
        /// <param name="effect">The effect data to evaluate.</param>
        /// <param name="gameEvent">The current game event that might trigger the effect.</param>
        /// <param name="state">The current game state.</param>
        /// <param name="sourceCardId">The instance ID of the card that owns the effect.</param>
        /// <returns>True if the effect should trigger, otherwise false.</returns>
        public static bool Check(EffectData effect, IGameEvent gameEvent, GameState state, int sourceCardId)
        {
            CardInstance? source = null;
            if (gameEvent is UnitDiedEvent unitDiedEvent && unitDiedEvent.Unit.InstanceId == sourceCardId)
                source = unitDiedEvent.Unit;
            else if (gameEvent is UnitSacrificedEvent unitSacrificedEvent && unitSacrificedEvent.Unit.InstanceId == sourceCardId)
                source = unitSacrificedEvent.Unit;
            else
                source = state.Board.GetAllUnits().FirstOrDefault(unit => unit.InstanceId == sourceCardId)
                      ?? state.PlayerA.Hand.Concat(state.PlayerB.Hand).FirstOrDefault(card => card.InstanceId == sourceCardId)
                      ?? state.SpellStack.FirstOrDefault(spell => spell.InstanceId == sourceCardId)
                      ?? state.PlayerA.DiscardPile.Concat(state.PlayerB.DiscardPile).FirstOrDefault(card => card.InstanceId == sourceCardId);

            if (source == null) return false;

            if (source.IsSilenced && effect.Trigger != TriggerType.OnPlayed) return false;

            if (effect.Condition != null && !EvaluateCondition(effect.Condition, gameEvent, state, source))
                return false;

            int myOwnerId = source.OwnerPlayerId;

            switch (effect.Trigger)
            {
                case TriggerType.OnPlayed:
                    if (gameEvent is CardPlayedEvent cardPlayedEvent)
                    {
                     
                        if (cardPlayedEvent.Card.InstanceId == sourceCardId) return true;
                        if (effect.Zone == EffectZone.Board && effect.Condition != null)
                        {
                           
                            return IsListeningToOthers(effect.Condition) &&
                                   EvaluateCondition(effect.Condition, gameEvent, state, source);
                        }
                    }
                    return false;

                case TriggerType.OnDeath:
                    return gameEvent is UnitDiedEvent deathEvent && deathEvent.Unit.InstanceId == sourceCardId;

                case TriggerType.OnKill:
                    return gameEvent is UnitDiedEvent killEvent && killEvent.KillerInstanceId == sourceCardId;

                case TriggerType.OnSacrificed:
                    return gameEvent is UnitSacrificedEvent sacrificedEvent && sacrificedEvent.Unit.InstanceId == sourceCardId;

                case TriggerType.OnFriendlyUnitDied:
                    if (gameEvent is UnitDiedEvent friendlyDeathEvent) 
                        return friendlyDeathEvent.Unit.InstanceId != sourceCardId && friendlyDeathEvent.OwnerId == myOwnerId;
                    if (gameEvent is UnitSacrificedEvent friendlySacrificeEvent) 
                        return friendlySacrificeEvent.Unit.InstanceId != sourceCardId && friendlySacrificeEvent.OwnerId == myOwnerId;
                    return false;

                case TriggerType.OnOtherUnitSacrificed:
                    return gameEvent is UnitSacrificedEvent otherSacrificeEvent && 
                           otherSacrificeEvent.Unit.InstanceId != sourceCardId && 
                           otherSacrificeEvent.OwnerId == myOwnerId;

                case TriggerType.OnDamagedEnemyHero:
                    return gameEvent is UnitDamagedEvent heroDamageEvent && 
                           heroDamageEvent.Source != null && 
                           heroDamageEvent.Source.OwnerPlayerId == myOwnerId && 
                           heroDamageEvent.Unit == null;

                case TriggerType.OnDamagedEnemyUnit:
                    return gameEvent is UnitDamagedEvent unitDamageEvent && 
                           unitDamageEvent.Source?.InstanceId == sourceCardId && 
                           unitDamageEvent.Unit != null && 
                           unitDamageEvent.Unit.OwnerPlayerId != myOwnerId;

                case TriggerType.OnOpponentCardDrawn:
                    return gameEvent is CardDrawnEvent opponentDrawEvent && opponentDrawEvent.PlayerId != myOwnerId;

                case TriggerType.OnFriendlyCardDrawn:
                    return gameEvent is CardDrawnEvent friendlyDrawEvent && friendlyDrawEvent.PlayerId == myOwnerId;

                case TriggerType.OnPreCombatLine:
                    return gameEvent is PreLineCombatEvent lineCombatEvent && 
                           state.Board.Lines[lineCombatEvent.LineIndex].GetAllUnits().Any(unit => unit.InstanceId == sourceCardId);

                case TriggerType.OnStatusApplied:
                    return gameEvent is StatusAppliedEvent;

                case TriggerType.OnDamagTaken:
                    return gameEvent is UnitDamagedEvent damageTakenEvent && damageTakenEvent.Unit?.InstanceId == sourceCardId;

                case TriggerType.OnFriendlyActionPlayed:
                    return gameEvent is CardPlayedEvent friendlyActionEvent &&
                           friendlyActionEvent.PlayerId == myOwnerId &&
                           friendlyActionEvent.Card.Definition.Type == CardType.Spell;

                default:
                    return false;
            }
        }
        #endregion

        #region Condition Evaluation
        private static bool EvaluateCondition(ConditionData condition, IGameEvent gameEvent, GameState state, CardInstance source)
        {
            int myOwnerId = source.OwnerPlayerId;

            switch (condition.Condition)
            {
                case ConditionType.And:
                    return condition.SubConditions.All(subCondition => EvaluateCondition(subCondition, gameEvent, state, source));

                case ConditionType.Or:
                    return condition.SubConditions.Any(subCondition => EvaluateCondition(subCondition, gameEvent, state, source));

                case ConditionType.IsStatus:
                    return gameEvent is StatusAppliedEvent statusAppliedEvent &&
                           string.Equals(statusAppliedEvent.Status.ToString(), condition.TargetParam, StringComparison.OrdinalIgnoreCase);

                case ConditionType.IsFriendly:
                    return gameEvent.SourcePlayerId == myOwnerId;

                case ConditionType.IsEnemy:
                    if (gameEvent is StatusAppliedEvent statusAppliedEvent2)
                    {
                        var targetUnit = state.Board.GetAllUnits().FirstOrDefault(unit => unit.InstanceId == statusAppliedEvent2.TargetUnitId);
                        return targetUnit != null && targetUnit.OwnerPlayerId != myOwnerId;
                    }
                    return gameEvent.SourcePlayerId != myOwnerId;

                case ConditionType.IsSelf:
                    int actorId = -1;
                    if (gameEvent is UnitDamagedEvent unitDamagedEvent) actorId = unitDamagedEvent.Source?.InstanceId ?? -1;
                    else if (gameEvent is UnitDiedEvent unitDiedEvent) actorId = (condition.ValueParam == 1) ? unitDiedEvent.KillerInstanceId ?? -1 : unitDiedEvent.Unit.InstanceId;
                    else if (gameEvent is CardPlayedEvent cardPlayedEvent) actorId = cardPlayedEvent.Card.InstanceId;
                    else if (gameEvent is UnitSacrificedEvent unitSacrificedEvent) actorId = unitSacrificedEvent.Unit.InstanceId;
                    return actorId == source.InstanceId;

                case ConditionType.IsSubtype:
                    CardInstance? subject = null;
                    if (gameEvent is UnitDamagedEvent unitDamagedEvent2) subject = unitDamagedEvent2.Source;
                    else if (gameEvent is UnitDiedEvent unitDiedEvent2) subject = unitDiedEvent2.Unit;
                    else if (gameEvent is CardPlayedEvent cardPlayedEvent2) subject = cardPlayedEvent2.Card;
                    else if (gameEvent is UnitSacrificedEvent unitSacrificedEvent2) subject = unitSacrificedEvent2.Unit;
                    else if (gameEvent is CardMovedEvent cardMovedEvent) actorId = cardMovedEvent.CardId;
                    return subject != null && subject.Definition.Subtypes.Contains(condition.TargetParam);

                case ConditionType.Not:
                    return !condition.SubConditions.Any(subCondition => EvaluateCondition(subCondition, gameEvent, state, source));

                case ConditionType.HasSubtypeOnBoard:
                    return state.Board.GetAllUnits()
                        .Any(unit => unit.OwnerPlayerId == myOwnerId &&
                                  unit.Definition.Subtypes.Contains(condition.TargetParam));

                default:
                    return true;
            }
        }

        private static bool IsListeningToOthers(ConditionData condition)
        {
            // Check if the condition is "Not IsSelf"
            if (condition.Condition == ConditionType.Not && condition.SubConditions.Any(subCondition => subCondition.Condition == ConditionType.IsSelf))
                return true;

            // Search complex conditions (And/Or)
            if (condition.Condition == ConditionType.And || condition.Condition == ConditionType.Or)
                return condition.SubConditions.Any(IsListeningToOthers);

            return false;
        }
        #endregion
    }
}