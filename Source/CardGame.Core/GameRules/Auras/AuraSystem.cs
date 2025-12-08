using System;
using System.Collections.Generic;
using System.Linq;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Models;
using CardGame.Core.State.Models;

namespace CardGame.Core.GameRules.Auras
{
    public class AuraSystem
    {
        public GameState RecalculateAuras(GameState currentState)
        {
            // 1. WYCZYŚĆ AURY
            var state = ClearAllAuras(currentState);

            // 2. ZNAJDŹ ŹRÓDŁA I CELE
            var units = state.Board.GetAllUnits();
            var pendingBuffs = new List<(int TargetId, Keyword KeywordToAdd)>();

            foreach (var sourceUnit in units)
            {
                foreach (var effect in sourceUnit.Definition.Effects)
                {
                    if (effect.Trigger == TriggerType.Passive && effect.Zone == EffectZone.Board)
                    {
                        foreach (var action in effect.Actions)
                        {
                            if (action.Type == ActionType.ApplyStatus && Enum.TryParse<Keyword>(action.StringParam, out var keyword))
                            {
                                var targets = FindTargets(state, sourceUnit, action.Target);
                                foreach (var t in targets)
                                {
                                    pendingBuffs.Add((t.InstanceId, keyword));
                                }
                            }
                        }
                    }
                }
            }

            // 3. ZAAPLIKUJ (Z WALIDACJĄ)
            foreach (var buff in pendingBuffs)
            {
                var targetUnit = state.Board.GetAllUnits().FirstOrDefault(u => u.InstanceId == buff.TargetId);

                if (targetUnit != null)
                {
                    // --- DIAGNOSTYKA ---
                    if (buff.KeywordToAdd == Keyword.SoulGuard)
                    {
                        bool hasDepleted = targetUnit.CurrentStats.Keywords.Contains(Keyword.SoulGuardDepleted);
                        Console.WriteLine($"[AURA CHECK] ID {targetUnit.InstanceId} ({targetUnit.Definition.Name}): Chcę nadać SoulGuard. Ma Depleted? {hasDepleted}");

                        // Debug keywordów
                        // Console.WriteLine($"   Keywordy: {string.Join(", ", targetUnit.CurrentStats.Keywords)}");
                    }
                    // -------------------

                    // BLOKADA: Jeśli ma Depleted, nie dawaj SoulGuarda
                    if (buff.KeywordToAdd == Keyword.SoulGuard &&
                        targetUnit.CurrentStats.Keywords.Contains(Keyword.SoulGuardDepleted))
                    {
                        Console.WriteLine($"[AURA BLOCKED] Zablokowano odnowienie SoulGuard dla {targetUnit.Definition.Name}!");
                        continue;
                    }

                    var newAuras = new List<Keyword>(targetUnit.AuraKeywords);
                    if (!newAuras.Contains(buff.KeywordToAdd))
                    {
                        newAuras.Add(buff.KeywordToAdd);
                    }

                    var newUnit = targetUnit.WithAuras(newAuras);
                    state = state.UpdateBoard(state.Board.UpdateUnit(newUnit));
                }
            }

            return state;
        }

        private GameState ClearAllAuras(GameState state)
        {
            var units = state.Board.GetAllUnits();
            foreach (var unit in units)
            {
                if (unit.AuraKeywords.Count > 0)
                {
                    var cleanUnit = unit.WithAuras(new List<Keyword>());
                    state = state.UpdateBoard(state.Board.UpdateUnit(cleanUnit));
                }
            }
            return state;
        }

        private List<CardInstance> FindTargets(GameState state, CardInstance source, TargetType targetType)
        {
            var all = state.Board.GetAllUnits();
            switch (targetType)
            {
                case TargetType.AllFriendlyUnits:
                    return all.Where(u => u.OwnerPlayerId == source.OwnerPlayerId).ToList();

                case TargetType.OtherFriendlyUnits:
                    return all.Where(u => u.OwnerPlayerId == source.OwnerPlayerId && u.InstanceId != source.InstanceId).ToList();

                case TargetType.AllEnemyUnits:
                    int enemyId = source.OwnerPlayerId == 1 ? 2 : 1;
                    return all.Where(u => u.OwnerPlayerId == enemyId).ToList();

                default:
                    return new List<CardInstance>();
            }
        }
    }
}