using System;
using System.Collections.Generic;
using System.Linq;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Models;
using CardGame.Core.Events;
using CardGame.Core.Events.Interfaces;
using CardGame.Core.State.Models;

namespace CardGame.Core.Cards.Logic
{
    #region Supporting Types
    /// <summary>
    /// Contains the resolved targets for an effect, including player and unit targets.
    /// </summary>
    public class EffectTargets
    {
        /// <summary>
        /// Gets or sets the target player, if any.
        /// </summary>
        public PlayerState? TargetPlayer { get; set; }

        /// <summary>
        /// Gets or sets the list of target units.
        /// </summary>
        public List<CardInstance> UnitTargets { get; set; } = new();

        /// <summary>
        /// Gets the first target unit, if any.
        /// </summary>
        public CardInstance? TargetUnit => UnitTargets.FirstOrDefault();
    }
    #endregion

    /// <summary>
    /// Resolves effect targets based on targeting types, game state, and context events.
    /// </summary>
    public static class EffectTargetResolver
    {
        #region Public Methods
        /// <summary>
        /// Resolves the targets for an effect based on the specified targeting type and context.
        /// </summary>
        /// <param name="type">The targeting type to resolve.</param>
        /// <param name="state">The current game state.</param>
        /// <param name="contextEvent">The context event that triggered the effect.</param>
        /// <param name="sourceCardId">The instance ID of the card that owns the effect.</param>
        /// <returns>An EffectTargets object containing all resolved targets.</returns>
        public static EffectTargets Resolve(TargetType type, GameState state, IGameEvent contextEvent, int sourceCardId)
        {
            var result = new EffectTargets();
            int ownerId = GetOwner(state, sourceCardId, contextEvent);
            int opponentId = ownerId == 1 ? 2 : 1;

            switch (type)
            {
                case TargetType.SelectedTarget:
                case TargetType.TargetEnemyUnit:
                case TargetType.TargetFriendlyUnit:
                case TargetType.OtherFriendlyUnits:
                    int? targetId = null;

                    if (contextEvent is TargetSelectedEvent targetSelectedEvent)
                        targetId = targetSelectedEvent.SelectedTargetId;
                    else if (contextEvent is CardPlayedEvent cardPlayedEvent)
                        targetId = cardPlayedEvent.SelectedTargetId;
                    else if (contextEvent is StatusAppliedEvent statusAppliedEvent)
                        targetId = statusAppliedEvent.TargetUnitId;
                    else if (contextEvent is UnitDamagedEvent unitDamagedEvent)
                        targetId = unitDamagedEvent.Unit?.InstanceId ?? unitDamagedEvent.Source?.InstanceId;
                    else if (contextEvent is UnitDiedEvent unitDiedEvent)
                        targetId = unitDiedEvent.Unit.InstanceId;

                    if (targetId.HasValue)
                    {
                        var targetUnit = state.Board.GetAllUnits().FirstOrDefault(unit => unit.InstanceId == targetId.Value);
                        if (targetUnit == null && (type == TargetType.Self || type == TargetType.SelectedTarget))
                        {
                            targetUnit = state.PlayerA.DiscardPile.FirstOrDefault(unit => unit.InstanceId == targetId.Value)
                                      ?? state.PlayerB.DiscardPile.FirstOrDefault(unit => unit.InstanceId == targetId.Value);
                        }
                        if (type == TargetType.OtherFriendlyUnits && targetId.Value == sourceCardId)
                            return result;

                        if (targetUnit != null)
                        {
                            bool isValid = true;
                            if (type == TargetType.TargetFriendlyUnit && targetUnit.OwnerPlayerId != ownerId) isValid = false;
                            if (type == TargetType.OtherFriendlyUnits && targetUnit.OwnerPlayerId != ownerId) isValid = false;
                            if (type == TargetType.TargetEnemyUnit && targetUnit.OwnerPlayerId != opponentId) isValid = false;

                            if (isValid) result.UnitTargets.Add(targetUnit);
                        }
                    }
                    break;

                case TargetType.FriendlyHero:
                    result.TargetPlayer = state.GetPlayer(ownerId);
                    break;
                case TargetType.EnemyHero:
                    result.TargetPlayer = state.GetPlayer(opponentId);
                    break;
                case TargetType.Choice:
                    result.TargetPlayer = state.GetPlayer(ownerId);
                    break;
                case TargetType.AllUnitsOnBoard:
                    result.UnitTargets.AddRange(state.Board.GetAllUnits());
                    break;
                case TargetType.AllEnemyUnits:
                    result.UnitTargets.AddRange(state.Board.GetAllUnits().Where(unit => unit.OwnerPlayerId == opponentId));
                    break;
                case TargetType.AllFriendlyUnits:
                    result.UnitTargets.AddRange(state.Board.GetAllUnits().Where(unit => unit.OwnerPlayerId == ownerId));
                    break;

                case TargetType.Self:
                    var self = state.Board.GetAllUnits().FirstOrDefault(unit => unit.InstanceId == sourceCardId)
                            ?? state.PlayerA.Hand.Concat(state.PlayerB.Hand).FirstOrDefault(card => card.InstanceId == sourceCardId)
                            ?? state.SpellStack.FirstOrDefault(spell => spell.InstanceId == sourceCardId)
                            ?? state.PlayerA.DiscardPile.Concat(state.PlayerB.DiscardPile).FirstOrDefault(card => card.InstanceId == sourceCardId);

                    if (self != null) result.UnitTargets.Add(self);
                    result.TargetPlayer = state.GetPlayer(ownerId);
                    break;
                case TargetType.OppositeEnemyUnit:
                    int lineIdx = -1;

                    // 1. PRIORITY: Check if the event tells us where the unit died
                    if (contextEvent is UnitDiedEvent ude)
                        lineIdx = ude.LineIndex;
                    else if (contextEvent is UnitSacrificedEvent use)
                        lineIdx = use.LineIndex;

                    // 2. FALLBACK: If it's not a death event (e.g., a "Before Combat" trigger),
                    // search the board
                    if (lineIdx == -1)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            if (state.Board.Lines[i].Player1Unit?.InstanceId == sourceCardId ||
                                state.Board.Lines[i].Player2Unit?.InstanceId == sourceCardId)
                            {
                                lineIdx = i;
                                break;
                            }
                        }
                    }

                    // 3. TARGETING: Now that we have the line, find the enemy
                    if (lineIdx >= 0 && lineIdx < 4)
                    {
                        var targetLine = state.Board.Lines[lineIdx];
                        var enemyUnit = (ownerId == 1) ? targetLine.Player2Unit : targetLine.Player1Unit;

                        if (enemyUnit != null)
                        {
                            result.UnitTargets.Add(enemyUnit);
                        }
                    }
                    break;

            }
            return result;
        }

        /// <summary>
        /// Gets all potential targets for a given targeting type without requiring a specific context event.
        /// </summary>
        /// <param name="type">The targeting type to get potential targets for.</param>
        /// <param name="state">The current game state.</param>
        /// <param name="sourceCardId">The instance ID of the card that owns the effect.</param>
        /// <returns>A list of potential target units.</returns>
        public static List<CardInstance> GetPotentialTargets(TargetType type, GameState state, int sourceCardId)
        {
            int ownerId = GetOwner(state, sourceCardId);
            int opponentId = ownerId == 1 ? 2 : 1;
            var allUnits = state.Board.GetAllUnits();

            return type switch
            {
                TargetType.TargetEnemyUnit => allUnits.Where(unit => unit.OwnerPlayerId == opponentId).ToList(),
                TargetType.TargetFriendlyUnit => allUnits.Where(unit => unit.OwnerPlayerId == ownerId && unit.InstanceId != sourceCardId).ToList(),
                TargetType.OtherFriendlyUnits => allUnits.Where(unit => unit.OwnerPlayerId == ownerId && unit.InstanceId != sourceCardId).ToList(),
                TargetType.SelectedTarget => allUnits.Where(unit => unit.InstanceId != sourceCardId).ToList(),
                _ => new List<CardInstance>()
            };
        }
        #endregion

        #region Private Methods
        private static int GetOwner(GameState state, int instanceId, IGameEvent? contextEvent = null)
        {
            // 1. Priority: Check the context event (Deaths/Sacrifices)
            if (contextEvent is UnitDiedEvent unitDiedEvent && unitDiedEvent.Unit.InstanceId == instanceId)
                return unitDiedEvent.Unit.OwnerPlayerId;
            if (contextEvent is UnitSacrificedEvent unitSacrificedEvent && unitSacrificedEvent.Unit.InstanceId == instanceId)
                return unitSacrificedEvent.Unit.OwnerPlayerId;
            if (contextEvent is TargetSelectedEvent tse && tse.SourceCardId == instanceId)
                return tse.SourcePlayerId;

            // 2. Check the Board
            var unit = state.Board.GetAllUnits().FirstOrDefault(x => x.InstanceId == instanceId);
            if (unit != null) return unit.OwnerPlayerId;

            // 3. Check Hands/Discard
            if (state.PlayerA.Hand.Any(x => x.InstanceId == instanceId) ||
                state.PlayerA.DiscardPile.Any(x => x.InstanceId == instanceId)) return 1;
            if (state.PlayerB.Hand.Any(x => x.InstanceId == instanceId) ||
                state.PlayerB.DiscardPile.Any(x => x.InstanceId == instanceId)) return 2;

            // 4. Fallback: The player whose turn it is
            return state.ActivePlayerId;
        }
        #endregion
    }
}