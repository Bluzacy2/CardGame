using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Models;
using CardGame.Core.State.Models;
using System.Collections.Generic;
using System.Linq;

namespace CardGame.Core.GameRules.Auras
{
    public class AuraSystem
    {
        public GameState RecalculateAuras(GameState currentState)
        {
            var workingState = currentState;
            var allUnits = workingState.Board.GetAllUnits();

            int p1Discount = allUnits.Count(u => u.OwnerPlayerId == 1 && u.Definition.Id == "45" && !u.IsSilenced);
            int p2Discount = allUnits.Count(u => u.OwnerPlayerId == 2 && u.Definition.Id == "45" && !u.IsSilenced);

            workingState = UpdateHandDiscounts(workingState, 1, p1Discount);
            workingState = UpdateHandDiscounts(workingState, 2, p2Discount);

            var map = new Dictionary<int, List<Keyword>>();
            foreach (var src in allUnits)
            {
                if (src.IsSilenced) continue;
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
                // Zawsze pobieraj najświeższą wersję z aktualizowanego stanu (naprawia Silence)
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

        private GameState UpdateHandDiscounts(GameState s, int pid, int discount)
        {
            var player = s.GetPlayer(pid);
            bool changed = false;
            var newHand = new List<CardInstance>();

            foreach (var card in player.Hand)
            {
                int targetDiscount = (card.Definition.Type == CardType.Spell) ? discount : 0;
                if (card.CostReduction != targetDiscount)
                {
                    card.CostReduction = targetDiscount;
                    changed = true;
                }
                newHand.Add(card);
            }

            return changed ? s.UpdatePlayer(player.With(hand: newHand)) : s;
        }
    }
}