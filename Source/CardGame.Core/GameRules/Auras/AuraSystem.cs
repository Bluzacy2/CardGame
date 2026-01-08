using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Models;
using CardGame.Core.State.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CardGame.Core.GameRules.Auras
{
    public class AuraSystem
    {
        public GameState RecalculateAuras(GameState currentState)
        {
            var workingState = currentState;

            // 1. Calculate cost modifiers for both players' hands
            // Check how much total "reduction" (positive discount, negative tax) each player has
            int p1Reduction = CalculateHandModifier(workingState, 1);
            int p2Reduction = CalculateHandModifier(workingState, 2);

            // 2. Update cards in hands (only spells)
            workingState = UpdateHandDiscounts(workingState, 1, p1Reduction);
            workingState = UpdateHandDiscounts(workingState, 2, p2Reduction);

            // 3. Aura Keywords logic (e.g., SoulGuard) - Your original logic
            // Only alive units can emit auras
            var allUnits = workingState.Board.GetAllUnits();
            var aliveUnits = allUnits.Where(u => !u.IsSilenced && u.CurrentStats.Health > 0).ToList();

            var map = new Dictionary<int, List<Keyword>>();
            foreach (var src in aliveUnits) // Only alive units emit auras
            {
                var passives = src.Definition.Effects.Where(e => e.Trigger == TriggerType.Passive && e.Zone == EffectZone.Board);
                foreach (var eff in passives)
                {
                    foreach (var act in eff.Actions.Where(a => a.Type == ActionType.ApplyStatus && a.StatusKeyword.HasValue))
                    {
                        var targets = FindTargets(workingState, src, act.Target);
                        foreach (var t in targets)
                        {
                            if (!map.ContainsKey(t.InstanceId)) map[t.InstanceId] = new List<Keyword>();
                            map[t.InstanceId].Add(act.StatusKeyword!.Value);
                        }
                    }
                }
            }

            foreach (var unit in allUnits)
            {
                var latestUnit = workingState.Board.GetAllUnits().FirstOrDefault(u => u.InstanceId == unit.InstanceId);
                if (latestUnit == null) continue;

                var targetKw = map.ContainsKey(latestUnit.InstanceId) ? map[latestUnit.InstanceId].Distinct().ToList() : new List<Keyword>();
                if (latestUnit.IsSilenced) targetKw.Clear();

                if (targetKw.Contains(Keyword.SoulGuard) && latestUnit.CurrentStats.Keywords.Contains(Keyword.SoulGuardDepleted))
                    targetKw.Remove(Keyword.SoulGuard);

                if (!latestUnit.AuraKeywords.SequenceEqual(targetKw))
                {
                    var updated = latestUnit.WithAuras(targetKw);
                    workingState = workingState.UpdateBoard(workingState.Board.UpdateUnit(updated));
                }
            }
            return workingState;
        }

        private int CalculateHandModifier(GameState state, int handOwnerId)
        {
            int totalReduction = 0;
            // Get all units on board, filter out silenced and dead units
            var allUnits = state.Board.GetAllUnits().Where(u => !u.IsSilenced && u.CurrentStats.Health > 0);

            foreach (var unit in allUnits)
            {
                // Look for Passive effects (or WhileOnBoard if you use both interchangeably)
                var passiveEffects = unit.Definition.Effects
                    .Where(e => e.Trigger == TriggerType.Passive && e.Zone == EffectZone.Board);

                foreach (var eff in passiveEffects)
                {
                    foreach (var act in eff.Actions.Where(a => a.Type == ActionType.BuffStats))
                    {
                        bool applies = false;

                        // My unit affects MY hand
                        if (act.Target == TargetType.FriendlySpellsInHand && unit.OwnerPlayerId == handOwnerId)
                            applies = true;

                        // Enemy unit affects MY hand
                        if (act.Target == TargetType.EnemySpellsInHand && unit.OwnerPlayerId != handOwnerId)
                            applies = true;

                        if (applies)
                        {
                            // Tea Maid (Amount: -1) -> totalReduction -= (-1) => +1 (Discount of 1)
                            // Monster (Amount: 6)   -> totalReduction -= (6)  => -6 (Tax of 6)
                            totalReduction -= act.Amount;
                        }
                    }
                }
            }
            return totalReduction;
        }

        private GameState UpdateHandDiscounts(GameState s, int pid, int reductionValue)
        {
            var player = s.GetPlayer(pid);
            bool changed = false;
            var newHand = new List<CardInstance>();

            foreach (var card in player.Hand)
            {
                // Cost auras in this system only affect spells
                int targetModifier = (card.Definition.Type == CardType.Spell) ? reductionValue : 0;

                if (card.CostReduction != targetModifier)
                {
                    // Use WithCostReduction to create a new immutable instance
                    var updatedCard = card.WithCostReduction(targetModifier);
                    newHand.Add(updatedCard);
                    changed = true;
                }
                else
                {
                    newHand.Add(card);
                }
            }

            return changed ? s.UpdatePlayer(player.With(hand: newHand)) : s;
        }

        private List<CardInstance> FindTargets(GameState s, CardInstance src, TargetType t)
        {
            var u = s.Board.GetAllUnits();
            return t switch
            {
                TargetType.AllFriendlyUnits => u.Where(x => x.OwnerPlayerId == src.OwnerPlayerId).ToList(),
                TargetType.OtherFriendlyUnits => u.Where(x => x.OwnerPlayerId == src.OwnerPlayerId && x.InstanceId != src.InstanceId).ToList(),
                TargetType.AllEnemyUnits => u.Where(x => x.OwnerPlayerId != src.OwnerPlayerId).ToList(),
                TargetType.AllUnitsOnBoard => u.ToList(),
                _ => new List<CardInstance>()
            };
        }
    }
}