using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Models;
using CardGame.Core.Events;
using CardGame.Core.State.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CardGame.Core.GameRules.Auras
{
    /// <summary>
    /// Manages aura calculations and updates, including hand cost modifiers and status keyword applications.
    /// </summary>
    public class AuraSystem
    {
        #region Public Methods
        /// <summary>
        /// Recalculates all aura effects on the board and updates hand cost modifiers.
        /// </summary>
        /// <param name="currentState">The current game state to recalculate auras for.</param>
        /// <param name="events">Optional event bus for publishing stat change events.</param>
        /// <returns>The updated game state with recalculated auras applied.</returns>
        public GameState RecalculateAuras(GameState currentState, EventBus? events = null)
        {
            var workingState = currentState;

            // 1. Calculate cost modifiers for both players' hands
            int player1Reduction = CalculateHandModifier(workingState, 1);
            int player2Reduction = CalculateHandModifier(workingState, 2);

            // 2. Update cards in hands (only spells)
            workingState = UpdateHandDiscounts(workingState, 1, player1Reduction);
            workingState = UpdateHandDiscounts(workingState, 2, player2Reduction);

            // 3. Aura Keywords logic (e.g., SoulGuard) - Only alive units can emit auras
            var allUnits = workingState.Board.GetAllUnits();
            var aliveUnits = allUnits.Where(unit => !unit.IsSilenced && unit.CurrentStats.Health > 0).ToList();

            var auraMap = new Dictionary<int, List<Keyword>>();
            foreach (var source in aliveUnits)
            {
                var passiveEffects = source.Definition.Effects.Where(effect => effect.Trigger == TriggerType.Passive && effect.Zone == EffectZone.Board);
                foreach (var effect in passiveEffects)
                {
                    foreach (var action in effect.Actions.Where(act => act.Type == ActionType.ApplyStatus && act.StatusKeyword.HasValue))
                    {
                        var targets = FindTargets(workingState, source, action.Target);
                        foreach (var target in targets)
                        {
                            if (!auraMap.ContainsKey(target.InstanceId)) 
                                auraMap[target.InstanceId] = new List<Keyword>();
                            
                            auraMap[target.InstanceId].Add(action.StatusKeyword!.Value);
                        }
                    }
                }
            }

            foreach (var unit in allUnits)
            {
                var latestUnit = workingState.Board.GetAllUnits().FirstOrDefault(u => u.InstanceId == unit.InstanceId);
                if (latestUnit == null) continue;

                var oldStats = latestUnit.CurrentStats;
                var targetKeywords = auraMap.ContainsKey(latestUnit.InstanceId) 
                    ? auraMap[latestUnit.InstanceId].Distinct().ToList() 
                    : new List<Keyword>();
                
                if (latestUnit.IsSilenced) 
                    targetKeywords.Clear();

                if (targetKeywords.Contains(Keyword.SoulGuard) && latestUnit.CurrentStats.Keywords.Contains(Keyword.SoulGuardDepleted))
                    targetKeywords.Remove(Keyword.SoulGuard);

                if (!latestUnit.AuraKeywords.SequenceEqual(targetKeywords))
                {
                    var updatedUnit = latestUnit.WithAuras(targetKeywords);
                    workingState = workingState.UpdateBoard(workingState.Board.UpdateUnit(updatedUnit));

                    var newStats = updatedUnit.CurrentStats;
                    if (events != null && (oldStats.Attack != newStats.Attack || oldStats.Health != newStats.Health))
                    {
                        events.Publish(new UnitStatsChangedEvent(
                            updatedUnit.InstanceId,
                            newStats.Attack - oldStats.Attack,
                            newStats.Health - oldStats.Health,
                            newStats.Attack,
                            newStats.Health,
                            null,
                            updatedUnit.OwnerPlayerId,
                            true
                        ));
                    }
                }
            }
            
            return workingState;
        }
        #endregion

        #region Hand Modifier Calculation
        private int CalculateHandModifier(GameState state, int handOwnerId)
        {
            int totalReduction = 0;
            var allUnits = state.Board.GetAllUnits().Where(unit => !unit.IsSilenced && unit.CurrentStats.Health > 0);

            foreach (var unit in allUnits)
            {
                var passiveEffects = unit.Definition.Effects
                    .Where(effect => effect.Trigger == TriggerType.Passive && effect.Zone == EffectZone.Board);

                foreach (var effect in passiveEffects)
                {
                    foreach (var action in effect.Actions.Where(act => act.Type == ActionType.BuffStats))
                    {
                        bool applies = false;

                        if (action.Target == TargetType.FriendlySpellsInHand && unit.OwnerPlayerId == handOwnerId)
                            applies = true;

                        if (action.Target == TargetType.EnemySpellsInHand && unit.OwnerPlayerId != handOwnerId)
                            applies = true;

                        if (applies)
                        {
                            // Tea Maid (Amount: -1) -> totalReduction -= (-1) => +1 (Discount of 1)
                            // Monster (Amount: 6)   -> totalReduction -= (6)  => -6 (Tax of 6)
                            totalReduction -= action.Amount;
                        }
                    }
                }
            }
            return totalReduction;
        }

        private GameState UpdateHandDiscounts(GameState state, int playerId, int reductionValue)
        {
            var player = state.GetPlayer(playerId);
            bool changed = false;
            var newHand = new List<CardInstance>();

            foreach (var card in player.Hand)
            {
                int targetModifier = (card.Definition.Type == CardType.Spell) ? reductionValue : 0;

                if (card.CostReduction != targetModifier)
                {
                    var updatedCard = card.WithCostReduction(targetModifier);
                    newHand.Add(updatedCard);
                    changed = true;
                }
                else
                {
                    newHand.Add(card);
                }
            }

            return changed ? state.UpdatePlayer(player.With(hand: newHand)) : state;
        }
        #endregion

        #region Target Finding
        private List<CardInstance> FindTargets(GameState state, CardInstance source, TargetType targetType)
        {
            var units = state.Board.GetAllUnits();
            
            return targetType switch
            {
                TargetType.AllFriendlyUnits => units.Where(unit => unit.OwnerPlayerId == source.OwnerPlayerId).ToList(),
                TargetType.OtherFriendlyUnits => units.Where(unit => unit.OwnerPlayerId == source.OwnerPlayerId && unit.InstanceId != source.InstanceId).ToList(),
                TargetType.AllEnemyUnits => units.Where(unit => unit.OwnerPlayerId != source.OwnerPlayerId).ToList(),
                TargetType.AllUnitsOnBoard => units.ToList(),
                _ => new List<CardInstance>()
            };
        }
        #endregion
    }
}