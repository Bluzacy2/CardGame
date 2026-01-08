using CardGame.ConsoleApp.Evolution.Analytics;
using CardGame.Core.AI.Interfaces;
using CardGame.Core.AI.Logic;
using CardGame.Core.AI.Strategies;
using CardGame.Core.Application;
using CardGame.Core.Cards.Data;
using CardGame.Core.Commands.Implementations;
using CardGame.Core.State.Enums;
using CardGame.Core.State.Models;
using CardGame.ConsoleApp.Evolution.V2_NewGen;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CardGame.ConsoleApp.Evolution.V1_Legacy;

namespace CardGame.ConsoleApp.Evolution.V2_NewGen
{
    public static class CardCache
    {
        private static readonly ConcurrentDictionary<int, CardData> _cache = new();
        private static readonly ConcurrentDictionary<int, string> _cardNames = new();
        private static readonly ConcurrentDictionary<int, int> _cardCosts = new();
        private static readonly ConcurrentDictionary<int, object> _cardData = new();

        public static CardData GetCard(int cardId)
        {
            return _cache.GetOrAdd(cardId, id => CardLibrary.Instance.GetCard(id));
        }

        public static string GetCardName(int cardId)
        {
            return _cardNames.GetOrAdd(cardId, id =>
            {
                try { return GetCard(id).Name; }
                catch { return $"Card_{cardId}"; }
            });
        }

        public static int GetCardCost(int cardId)
        {
            return _cardCosts.GetOrAdd(cardId, id =>
            {
                try { return GetCard(id).Cost; }
                catch { return 0; }
            });
        }

        public static object GetCardData(int cardId)
        {
            return _cardData.GetOrAdd(cardId, id =>
            {
                try
                {
                    var card = GetCard(id);
                    return new
                    {
                        card.Cost,
                        card.Attack,
                        card.Health,
                        card.Type,
                        Keywords = card.Keywords ?? new List<Keyword>(),
                        Subtypes = card.Subtypes ?? new List<string>()
                    };
                }
                catch { return null; }
            });
        }

        public static void Clear()
        {
            _cache.Clear();
            _cardNames.Clear();
            _cardCosts.Clear();
            _cardData.Clear();
        }
    }

    public class CardExplorationSystem
    {
        private ConcurrentDictionary<string, List<ExplorationCandidate>> _archetypeCandidates = new();
        private const int CANDIDATES_PER_ARCHETYPE = 8;
        private const int MIN_GAMES_FOR_ANALYSIS = 30;
        private int[] _collectibleCardIds;
        private ConcurrentDictionary<int, float> _cardSuitabilityCache = new();
        private ConcurrentDictionary<string, List<int>> _archetypeSpecialistsCache = new();

        public CardExplorationSystem(int[] collectibleCardIds)
        {
            _collectibleCardIds = collectibleCardIds ?? throw new ArgumentNullException(nameof(collectibleCardIds));
        }

        public class ExplorationCandidate
        {
            public int CardId { get; set; }
            public string CardName { get; set; }
            public float CurrentUseRate { get; set; }
            public float WinRate { get; set; }
            public float ExplorationScore { get; set; }
            public string Reason { get; set; }
        }

        public void UpdateExplorationCandidates(ConcurrentDictionary<int, CardStatsEnhanced> cardStats,
                                              List<EvolvableIndividual> population)
        {
            _archetypeCandidates.Clear();
            _archetypeSpecialistsCache.Clear();

            var archetypes = new[] { "Aggro", "Control", "Combo", "Midrange", "Sacrifice" };
            var archetypeGroups = population.GroupBy(p => p.Dna?.ClassifyArchetype() ?? "Unknown");

            foreach (var group in archetypeGroups)
            {
                _archetypeSpecialistsCache[group.Key] = group.Select(p => p.Id).ToList();
            }

            Parallel.ForEach(archetypes, archetype =>
            {
                var candidates = AnalyzeCardsForArchetype(archetype, cardStats, population);
                _archetypeCandidates[archetype] = candidates;
            });
        }

        private List<ExplorationCandidate> AnalyzeCardsForArchetype(string archetype,
                                                                   ConcurrentDictionary<int, CardStatsEnhanced> cardStats,
                                                                   List<EvolvableIndividual> population)
        {
            var candidates = new List<ExplorationCandidate>();
            var archetypeBots = _archetypeSpecialistsCache.GetValueOrDefault(archetype, new List<int>())
                .Select(id => population.FirstOrDefault(p => p.Id == id))
                .Where(p => p != null)
                .ToList();

            foreach (var cardStat in cardStats.Values)
            {
                if (cardStat.GamesWithCard < MIN_GAMES_FOR_ANALYSIS)
                    continue;

                var card = CardCache.GetCard(cardStat.CardId);
                if (card == null) continue;

                float suitabilityScore = GetCachedArchetypeSuitability(card, archetype);
                float explorationValue = CalculateExplorationValue(cardStat, archetypeBots);

                if (explorationValue > 0.2f && suitabilityScore > 0.3f)
                {
                    var reason = GenerateExplorationReason(card, cardStat, archetype, suitabilityScore);

                    candidates.Add(new ExplorationCandidate
                    {
                        CardId = cardStat.CardId,
                        CardName = cardStat.CardName,
                        CurrentUseRate = cardStat.UseRate,
                        WinRate = cardStat.WinRate,
                        ExplorationScore = explorationValue * suitabilityScore,
                        Reason = reason
                    });
                }
            }

            return candidates
                .OrderByDescending(c => c.ExplorationScore)
                .ThenBy(c => c.CurrentUseRate)
                .Take(CANDIDATES_PER_ARCHETYPE)
                .ToList();
        }

        private float GetCachedArchetypeSuitability(CardData card, string archetype)
        {
            int cacheKey = card.Id * 1000 + archetype.GetHashCode() % 1000;
            return _cardSuitabilityCache.GetOrAdd(cacheKey, _ => CalculateArchetypeSuitability(card, archetype));
        }

        private float CalculateArchetypeSuitability(CardData card, string archetype)
        {
            switch (archetype)
            {
                case "Aggro":
                    return ScoreForAggro(card);
                case "Control":
                    return ScoreForControl(card);
                case "Combo":
                    return ScoreForCombo(card);
                case "Midrange":
                    return ScoreForMidrange(card);
                case "Sacrifice":
                    return ScoreForSacrifice(card);
                default:
                    return 0.1f;
            }
        }

        private float ScoreForAggro(CardData card)
        {
            float score = 0;

            if (card.Cost <= 2)
            {
                float statEfficiency = (card.Attack + card.Health) / (float)Math.Max(1, card.Cost);
                score += statEfficiency * 0.5f;
            }

            if (card.Effects?.Any(e => e.Trigger == TriggerType.OnPlayed) == true)
                score += 0.4f;

            if (card.Effects?.Any(e => e.Actions?.Any(a => a.Type == ActionType.BonusAttack) == true) == true)
                score += 0.6f;

            if (card.Effects?.Any(e => e.Actions?.Any(a => a.Type == ActionType.DealDamage &&
                                                          a.Target == TargetType.EnemyHero) == true) == true)
                score += 0.8f;

            if (card.Keywords?.Contains(Keyword.Flying) == true)
                score += 0.5f;

            return Math.Min(score, 2.0f);
        }

        private float ScoreForControl(CardData card)
        {
            float score = 0;

            if (card.Effects?.Any(e => e.Actions?.Any(a =>
                a.Type == ActionType.DestroyUnit ||
                a.Type == ActionType.Silence ||
                a.Type == ActionType.ReturnToHand ||
                a.Type == ActionType.ApplyStatus && (a.StatusKeyword == Keyword.Stunned || a.StatusKeyword == Keyword.Marked)
            ) == true) == true)
                score += 0.8f;

            if (card.Effects?.Any(e => e.Actions?.Any(a =>
                a.Type == ActionType.DrawCard ||
                a.Type == ActionType.TutorCard
            ) == true) == true)
                score += 0.6f;

            if (card.Effects?.Any(e => e.Actions?.Any(a =>
                a.Type == ActionType.DealDamage && a.Target == TargetType.AllUnitsOnBoard
            ) == true) == true)
                score += 1.0f;

            if (card.Type == CardType.Unit && card.Health >= 4)
                score += 0.4f;

            if (card.Type == CardType.Spell)
                score += 0.3f;

            return Math.Min(score, 2.0f);
        }

        private float ScoreForCombo(CardData card)
        {
            float score = 0;

            if (card.Effects?.Any(e => e.Actions?.Any(a =>
                a.Type == ActionType.TutorCard ||
                a.Type == ActionType.AddCardToHand
            ) == true) == true)
                score += 0.9f;

            if (card.Effects?.Any(e =>
                e.Trigger == TriggerType.OnStatusApplied ||
                e.Actions?.Any(a => a.Type == ActionType.ApplyStatus) == true
            ) == true)
                score += 0.5f;

            if (card.Effects?.Count(e => e.Actions?.Count > 1) > 0)
                score += 0.4f;

            if (card.Effects?.Any(e => e.Actions?.Any(a =>
                a.Type == ActionType.MakeAUnit ||
                a.Type == ActionType.SummonUnit
            ) == true) == true)
                score += 0.3f;

            return Math.Min(score, 2.0f);
        }

        private float ScoreForMidrange(CardData card)
        {
            float score = 0;

            if (card.Type == CardType.Unit)
            {
                float statEfficiency = (card.Attack + card.Health) / (float)Math.Max(1, card.Cost);
                score += statEfficiency * 0.8f;

                if (card.Cost >= 2 && card.Cost <= 5 && card.Attack >= 2 && card.Health >= 2)
                    score += 0.4f;

                if (card.Keywords?.Contains(Keyword.Armored) == true)
                    score += 0.3f;

                if (card.Keywords?.Contains(Keyword.SplashDamage) == true)
                    score += 0.4f;

                if (card.Effects?.Any(e => e.Actions?.Any(a =>
                    a.Type == ActionType.BuffStats ||
                    a.Type == ActionType.Heal
                ) == true) == true)
                    score += 0.3f;
            }

            return Math.Min(score, 2.0f);
        }

        private float ScoreForSacrifice(CardData card)
        {
            float score = 0;

            if (card.Effects?.Any(e =>
                e.Trigger == TriggerType.OnSacrificed ||
                e.Trigger == TriggerType.OnDeath ||
                e.Trigger == TriggerType.OnOtherUnitSacrificed ||
                e.Trigger == TriggerType.OnFriendlyUnitDied
            ) == true)
                score += 1.0f;

            if (card.Effects?.Any(e => e.Actions?.Any(a =>
                a.Type == ActionType.SacrificeUnit
            ) == true) == true)
                score += 0.8f;

            if (card.Effects?.Any(e => e.Actions?.Any(a =>
                a.Type == ActionType.MakeAUnit ||
                a.Type == ActionType.SummonUnit
            ) == true) == true)
                score += 0.4f;

            if (card.Type == CardType.Unit && (card.Attack <= 1 || card.Health <= 1))
                score += 0.2f;

            return Math.Min(score, 2.0f);
        }

        private float CalculateExplorationValue(CardStatsEnhanced stat, List<EvolvableIndividual> archetypeBots)
        {
            float value = 0;

            if (stat.WinRate > 0.55f && stat.UseRate < 0.2f)
                value += 1.0f;

            if (stat.UseRate < 0.05f)
                value += 0.8f;

            float archetypeUsage = CalculateArchetypeSpecificUsage(stat.CardId, archetypeBots);
            if (archetypeUsage < 0.1f)
                value += 0.5f;

            return value;
        }

        private float CalculateArchetypeSpecificUsage(int cardId, List<EvolvableIndividual> archetypeBots)
        {
            if (archetypeBots.Count == 0) return 0;

            int decksWithCard = 0;
            foreach (var bot in archetypeBots)
            {
                var deck = bot.Dna?.BuildDeck(_collectibleCardIds, new Random());
                if (deck?.Contains(cardId) == true)
                    decksWithCard++;
            }

            return (float)decksWithCard / archetypeBots.Count;
        }

        private string GenerateExplorationReason(CardData card, CardStatsEnhanced stat,
                                                string archetype, float suitabilityScore)
        {
            var reasons = new List<string>();

            if (suitabilityScore > 0.7f)
                reasons.Add($"Excellent {archetype} fit");

            if (stat.WinRate > 0.55f && stat.UseRate < 0.2f)
                reasons.Add($"High win rate ({stat.WinRate:P0}) but underused");

            if (stat.UseRate < 0.05f)
                reasons.Add($"Rarely seen (<5% usage)");

            if (card.Effects?.Count > 0)
                reasons.Add($"Has {card.Effects.Count} effects");

            return string.Join(", ", reasons.Take(3));
        }

        public List<ExplorationCandidate> GetCandidatesForArchetype(string archetype)
        {
            return _archetypeCandidates.GetValueOrDefault(archetype, new List<ExplorationCandidate>());
        }

        public void PrintExplorationStatus()
        {
            Console.WriteLine("\n🔍 CARD EXPLORATION STATUS:");

            foreach (var kvp in _archetypeCandidates.OrderBy(kv => kv.Key))
            {
                Console.WriteLine($"\n{kvp.Key}:");
                foreach (var candidate in kvp.Value.Take(3))
                {
                    Console.WriteLine($"  {candidate.CardName}: {candidate.ExplorationScore:F2} ({candidate.Reason})");
                    Console.WriteLine($"    Use: {candidate.CurrentUseRate:P1}, Win: {candidate.WinRate:P1}");
                }
            }
        }
    }

    public class AdaptiveFitnessOptimizer
    {
        private Dictionary<string, float> _componentWeights = new()
        {
            ["BaseFitness"] = 1.0f,      // 100% weight to base
            ["Novelty"] = 0.05f,         // 5% novelty bonus
            ["ArchetypePurity"] = 0.10f, // 10% for being pure
            ["AntiMeta"] = 0.02f
        };

        private float _convergenceLevel = 0f;
        private float _bestFitnessHistory = 0f;
        private int _generationsWithoutImprovement = 0;
        private ConcurrentDictionary<int, float> _individualFitnessCache = new();

        public void UpdateConvergenceState(float currentBestFitness, float populationDiversity,
                                          int generation, int stagnationCounter)
        {
            if (currentBestFitness > _bestFitnessHistory * 1.01f)
            {
                _bestFitnessHistory = currentBestFitness;
                _generationsWithoutImprovement = 0;
                _individualFitnessCache.Clear();
            }
            else
            {
                _generationsWithoutImprovement++;
            }

            _convergenceLevel = Math.Clamp(
                (1.0f - populationDiversity) * 0.7f +
                (Math.Min(_generationsWithoutImprovement, 20) / 20f) * 0.3f,
                0f, 1f);

            AdjustWeightsForConvergence();
        }

        private void AdjustWeightsForConvergence()
        {
            float explorationMultiplier = 1.0f + _convergenceLevel * 1.5f;
            float baseFitnessMultiplier = 1.0f - _convergenceLevel * 0.3f;

            _componentWeights["Exploration"] = 0.15f * explorationMultiplier;
            _componentWeights["Rarity"] = 0.04f * explorationMultiplier;
            _componentWeights["Novelty"] = 0.25f * explorationMultiplier;
            _componentWeights["BaseFitness"] = 1.0f * baseFitnessMultiplier;

            foreach (var key in _componentWeights.Keys.ToList())
            {
                _componentWeights[key] = Math.Clamp(_componentWeights[key], 0f, 2f);
            }
        }

        public float CalculateOptimizedFitness(EvolvableIndividual individual,
                                      float baseFitness,
                                      float deckNoveltyScore,
                                      float synergyBonus,
                                      float consistencyBonus,
                                      float rareCardBonus,
                                      float explorationBonus,
                                      float hybridBonus)
        {
            // 1. CACHE CHECK
            int cacheKey = individual.Id;
            if (_individualFitnessCache.TryGetValue(cacheKey, out float cached))
                return cached;

            // 2. START WITH BASE FITNESS - don't double it!
            // This was causing massive negative values when baseFitness is negative
            float total = baseFitness * _componentWeights["BaseFitness"];  // Removed * 2.0f

            // 3. Add stylistic bonuses
            float stylisticScore = 0;

            // Scale novelty down significantly - 1200 was way too high
            stylisticScore += deckNoveltyScore * 300f * _componentWeights["Novelty"];  // Reduced from 1200

            // Scale synergy and consistency to reasonable values
            stylisticScore += synergyBonus * 0.5f * _componentWeights["Synergy"];
            stylisticScore += consistencyBonus * 0.5f * _componentWeights["Consistency"];

            // Scale rare card bonus WAY down
            stylisticScore += rareCardBonus * 0.1f * _componentWeights["Rarity"];

            // Scale exploration and hybrid bonuses
            stylisticScore += explorationBonus * 0.2f * _componentWeights["Exploration"];
            stylisticScore += hybridBonus * 0.1f * _componentWeights["Hybrid"];

            // 4. Apply cap - but make it relative to base fitness
            float maxAllowedStylistic = Math.Max(Math.Abs(baseFitness) * 0.5f, 100f);  // Reduced from 1.5f
            total += Math.Min(stylisticScore, maxAllowedStylistic);

            // 5. Apply penalties
            float toolboxPenalty = CalculateToolboxPenalty(individual) * (1.0f - _componentWeights["Consistency"] * 0.5f);
            float trapCardPenalty = CalculateTrapCardPenalty(individual) * (1.0f - _componentWeights["Rarity"] * 0.3f);
            total -= (toolboxPenalty + trapCardPenalty);

            // 6. FLOOR - set reasonable minimum, not 1000!
            float finalFitness = Math.Max(total, 1f);  // Changed from 10f to 1f to preserve differences

            _individualFitnessCache[cacheKey] = finalFitness;
            return finalFitness;
        }

        private float CalculateToolboxPenalty(EvolvableIndividual individual) => 0f;
        private float CalculateTrapCardPenalty(EvolvableIndividual individual) => 0f;

        public void PrintWeights()
        {
            Console.WriteLine("\n⚖️  ADAPTIVE FITNESS WEIGHTS:");
            foreach (var kvp in _componentWeights.OrderByDescending(kv => kv.Value))
            {
                string indicator = kvp.Value > 0.15f ? "📈" : kvp.Value < 0.05f ? "📉" : "➡️";
                Console.WriteLine($"  {kvp.Key}: {kvp.Value:F2} {indicator}");
            }
            Console.WriteLine($"  Convergence: {_convergenceLevel:P0}");
        }
    }

    public class ArchetypeSpecialistOptimizer
    {
        private Dictionary<string, SpecialistTarget> _specialistTargets = new();
        private CardExplorationSystem _explorationSystem;
        private int[] _collectibleCardIds;
        private ConcurrentDictionary<string, List<int>> _archetypeDecksCache = new();

        public class SpecialistTarget
        {
            public string Archetype { get; set; }
            public float TargetPurity { get; set; } = 0.9f;
            public int TargetCardCount { get; set; } = 30;
            public List<int> RequiredCards { get; set; } = new();
            public List<int> ForbiddenCards { get; set; } = new();
            public float ManaCurveTarget { get; set; }
            public Dictionary<string, int> SubtypeRequirements { get; set; } = new();
        }

        public ArchetypeSpecialistOptimizer(CardExplorationSystem explorationSystem, int[] collectibleCardIds)
        {
            _explorationSystem = explorationSystem;
            _collectibleCardIds = collectibleCardIds ?? throw new ArgumentNullException(nameof(collectibleCardIds));
            InitializeSpecialistTargets();
        }

        private void InitializeSpecialistTargets()
        {
            _specialistTargets["Aggro"] = new SpecialistTarget
            {
                Archetype = "Aggro",
                TargetPurity = 0.92f,
                ManaCurveTarget = 1.8f,
                RequiredCards = new List<int>(),
                ForbiddenCards = new List<int>(),
                SubtypeRequirements = new Dictionary<string, int>
                {
                    ["Mercenary"] = 6,
                    ["Human"] = 4,
                    ["Monster"] = 4,
                    ["Machine"] = 2
                }
            };

            _specialistTargets["Control"] = new SpecialistTarget
            {
                Archetype = "Control",
                TargetPurity = 0.88f,
                ManaCurveTarget = 3.2f,
                SubtypeRequirements = new Dictionary<string, int>
                {
                    ["Human"] = 6,
                    ["Mercenary"] = 3,
                    ["Monster"] = 4,
                    ["Mystical"] = 1
                }
            };

            _specialistTargets["Combo"] = new SpecialistTarget
            {
                Archetype = "Combo",
                TargetPurity = 0.85f,
                ManaCurveTarget = 2.5f,
                SubtypeRequirements = new Dictionary<string, int>
                {
                    ["Monster"] = 6,
                    ["Human"] = 6
                }
            };

            _specialistTargets["Midrange"] = new SpecialistTarget
            {
                Archetype = "Midrange",
                TargetPurity = 0.87f,
                ManaCurveTarget = 2.8f,
                SubtypeRequirements = new Dictionary<string, int>
                {
                    ["Animal"] = 3,
                    ["Machine"] = 5,
                    ["Human"] = 6,
                    ["Mercenary"] = 4,
                }
            };

            _specialistTargets["Sacrifice"] = new SpecialistTarget
            {
                Archetype = "Sacrifice",
                TargetPurity = 0.91f,
                ManaCurveTarget = 2.3f,
                SubtypeRequirements = new Dictionary<string, int>
                {
                    ["Monster"] = 12,
                    ["Animal"] = 4,
                    ["Human"] = 3
                }
            };
        }

        public void UpdateSpecialistTargets(ConcurrentDictionary<int, CardStatsEnhanced> cardStats,
                                           List<EvolvableIndividual> population)
        {
            _archetypeDecksCache.Clear();

            foreach (var target in _specialistTargets.Values)
            {
                UpdateTargetCards(target, cardStats, population);
            }
        }

        private void UpdateTargetCards(SpecialistTarget target,
                                      ConcurrentDictionary<int, CardStatsEnhanced> cardStats,
                                      List<EvolvableIndividual> population)
        {
            var candidates = _explorationSystem.GetCandidatesForArchetype(target.Archetype);

            target.RequiredCards = candidates
                .Where(c => c.ExplorationScore > 0.7f && c.CurrentUseRate < 0.2f)
                .Take(3)
                .Select(c => c.CardId)
                .ToList();

            var currentSpecialists = population
                .Where(p => p.Dna?.ClassifyArchetype() == target.Archetype)
                .OrderByDescending(p => p.CompositeFitness)
                .Take(5)
                .ToList();

            if (currentSpecialists.Count > 0)
            {
                var highPerformerCards = GetCommonCards(currentSpecialists.Take(2).ToList());
                var lowPerformerCards = GetCommonCards(currentSpecialists.Skip(2).ToList());

                var badCards = lowPerformerCards.Except(highPerformerCards)
                    .Where(cardId => cardStats.TryGetValue(cardId, out var stat) && stat.WinRate < 0.45f)
                    .Take(5)
                    .ToList();

                target.ForbiddenCards = badCards;
            }
        }

        private List<int> GetCommonCards(List<EvolvableIndividual> individuals)
        {
            if (individuals.Count == 0) return new List<int>();

            var cardFrequency = new Dictionary<int, int>();
            foreach (var ind in individuals)
            {
                var deck = GetCachedDeck(ind);
                if (deck != null)
                {
                    foreach (var cardId in deck.Distinct())
                    {
                        cardFrequency[cardId] = cardFrequency.GetValueOrDefault(cardId) + 1;
                    }
                }
            }

            return cardFrequency
                .Where(kv => kv.Value >= individuals.Count / 2)
                .Select(kv => kv.Key)
                .ToList();
        }

        private int[] GetCachedDeck(EvolvableIndividual individual)
        {
            string cacheKey = $"{individual.Id}_{individual.Generation}";
            if (!_archetypeDecksCache.TryGetValue(cacheKey, out var deckList))
            {
                var deck = individual.Dna?.BuildDeck(_collectibleCardIds, new Random());
                deckList = deck?.Distinct().ToList() ?? new List<int>();
                _archetypeDecksCache[cacheKey] = deckList;
            }
            return deckList.ToArray();
        }

        public float EvaluateSpecialistQuality(EvolvableIndividual individual)
        {
            string archetype = individual.Dna?.ClassifyArchetype();
            if (string.IsNullOrEmpty(archetype) || !_specialistTargets.ContainsKey(archetype))
                return 0f;

            var target = _specialistTargets[archetype];
            var deck = GetCachedDeck(individual);

            if (deck == null || deck.Length == 0) return 0f;

            float qualityScore = 0f;

            float purity = ArchetypePurityCalculator.CalculatePurity(individual.Dna, archetype);
            if (purity >= target.TargetPurity * 0.9f)
                qualityScore += 0.3f;

            int requiredCardsIncluded = deck.Count(cardId => target.RequiredCards.Contains(cardId));
            qualityScore += (requiredCardsIncluded / Math.Max(1f, target.RequiredCards.Count)) * 0.2f;

            int forbiddenCardsIncluded = deck.Count(cardId => target.ForbiddenCards.Contains(cardId));
            qualityScore -= (forbiddenCardsIncluded * 0.1f);

            var subtypeCounts = new Dictionary<string, int>();
            foreach (var cardId in deck.Distinct())
            {
                var card = CardCache.GetCard(cardId);
                foreach (var subtype in card.Subtypes ?? new List<string>())
                {
                    subtypeCounts[subtype] = subtypeCounts.GetValueOrDefault(subtype) + 1;
                }
            }

            foreach (var req in target.SubtypeRequirements)
            {
                int actual = subtypeCounts.GetValueOrDefault(req.Key, 0);
                if (actual >= req.Value)
                    qualityScore += 0.1f;
            }

            return Math.Max(qualityScore, 0f) * 10000f;
        }

        public SpecialistTarget GetSpecialistTarget(string archetype)
        {
            return _specialistTargets.GetValueOrDefault(archetype);
        }

        public void PrintSpecialistTargets()
        {
            Console.WriteLine("\n🎯 SPECIALIST OPTIMIZATION TARGETS:");
            foreach (var kvp in _specialistTargets.OrderBy(kv => kv.Key))
            {
                var target = kvp.Value;
                Console.WriteLine($"\n{target.Archetype}:");
                Console.WriteLine($"  Target Purity: {target.TargetPurity:P0}");
                Console.WriteLine($"  Mana Curve: {target.ManaCurveTarget:F1}");
                Console.WriteLine($"  Required Cards: {target.RequiredCards.Count}");
                Console.WriteLine($"  Forbidden Cards: {target.ForbiddenCards.Count}");
                Console.WriteLine($"  Subtype Reqs: {string.Join(", ", target.SubtypeRequirements.Select(kv => $"{kv.Key}:{kv.Value}"))}");
            }
        }
    }

    public class HybridDiscoveryEngine
    {
        private ConcurrentDictionary<string, HybridPattern> _discoveredHybrids = new();
        private ConcurrentDictionary<string, float> _hybridPerformance = new();
        private const int PATTERN_MEMORY = 50;
        private int[] _collectibleCardIds;
        private ConcurrentDictionary<int, int[]> _deckCache = new();

        public HybridDiscoveryEngine(int[] collectibleCardIds)
        {
            _collectibleCardIds = collectibleCardIds ?? throw new ArgumentNullException(nameof(collectibleCardIds));
        }

        public class HybridPattern
        {
            public string PrimaryArchetype { get; set; }
            public string SecondaryArchetype { get; set; }
            public float Ratio { get; set; }
            public List<int> KeyCards { get; set; } = new();
            public float WinRate { get; set; }
            public int GamesPlayed { get; set; }
            public float DiscoveryScore { get; set; }
        }

        public void AnalyzePopulationForHybrids(List<EvolvableIndividual> population,
                                               ConcurrentDictionary<int, CardStatsEnhanced> cardStats)
        {
            var potentialHybrids = population
                .Where(ind => ind.Dna != null)
                .Select(ind => new
                {
                    Individual = ind,
                    Strengths = ArchetypePurityCalculator.GetAllArchetypeStrengths(ind.Dna)
                })
                .Where(x => x.Strengths.Count >= 2)
                .Select(x => new
                {
                    x.Individual,
                    Top2 = x.Strengths.OrderByDescending(kv => kv.Value).Take(2).ToList(),
                    Purity = ArchetypePurityCalculator.CalculatePurity(x.Individual.Dna, x.Strengths.OrderByDescending(kv => kv.Value).First().Key)
                })
                .Where(x => x.Purity < 0.7f && x.Purity > 0.3f)
                .ToList();

            foreach (var hybrid in potentialHybrids)
            {
                string primary = hybrid.Top2[0].Key;
                string secondary = hybrid.Top2[1].Key;
                float ratio = hybrid.Top2[0].Value / Math.Max(0.1f, hybrid.Top2[1].Value);
                string hybridKey = $"{primary}/{secondary}:{ratio:F1}";

                var pattern = _discoveredHybrids.GetOrAdd(hybridKey, _ => new HybridPattern
                {
                    PrimaryArchetype = primary,
                    SecondaryArchetype = secondary,
                    Ratio = ratio
                });

                pattern.GamesPlayed++;
                pattern.WinRate = (pattern.WinRate * (pattern.GamesPlayed - 1) +
                                  (hybrid.Individual.BaseFitness > 0 ? 0.6f : 0.4f)) / pattern.GamesPlayed;

                var deck = GetCachedDeck(hybrid.Individual);
                pattern.KeyCards = deck?.Distinct().Take(8).ToList() ?? new List<int>();

                pattern.DiscoveryScore = CalculateDiscoveryScore(pattern);
            }

            var toRemove = _discoveredHybrids.Where(kv => kv.Value.GamesPlayed < 3).Select(kv => kv.Key).ToList();
            foreach (var key in toRemove)
                _discoveredHybrids.TryRemove(key, out _);
        }

        private int[] GetCachedDeck(EvolvableIndividual individual)
        {
            int cacheKey = individual.Id;
            if (!_deckCache.TryGetValue(cacheKey, out var deck))
            {
                deck = individual.Dna.BuildDeck(_collectibleCardIds, new Random());
                _deckCache[cacheKey] = deck;
            }
            return deck;
        }

        private float CalculateDiscoveryScore(HybridPattern pattern)
        {
            float score = 0;

            float prevalence = (float)pattern.GamesPlayed / PATTERN_MEMORY;
            score += (1.0f - prevalence) * 0.4f;

            score += pattern.WinRate * 0.4f;

            if (pattern.Ratio >= 1.2f && pattern.Ratio <= 2.5f)
                score += 0.2f;

            return score;
        }

        public List<HybridPattern> GetPromisingHybrids(int count = 5)
        {
            return _discoveredHybrids.Values
                .Where(p => p.GamesPlayed >= 3 && p.DiscoveryScore > 0.5f)
                .OrderByDescending(p => p.DiscoveryScore)
                .ThenByDescending(p => p.WinRate)
                .Take(count)
                .ToList();
        }

        public float CalculateHybridPotentialBonus(EvolvableIndividual individual)
        {
            var strengths = ArchetypePurityCalculator.GetAllArchetypeStrengths(individual.Dna);
            if (strengths.Count < 2) return 0f;

            var top2 = strengths.OrderByDescending(kv => kv.Value).Take(2).ToList();
            string primary = top2[0].Key;
            string secondary = top2[1].Key;
            float ratio = top2[0].Value / Math.Max(0.1f, top2[1].Value);
            string hybridKey = $"{primary}/{secondary}:{ratio:F1}";

            if (_discoveredHybrids.TryGetValue(hybridKey, out var pattern))
            {
                return pattern.DiscoveryScore * 15000f;
            }
            else if (ratio >= 1.2f && ratio <= 2.5f)
            {
                return 8000f;
            }

            return 0f;
        }

        public void PrintDiscoveredHybrids()
        {
            var promising = GetPromisingHybrids(3);
            if (promising.Count == 0) return;

            Console.WriteLine("\n🧬 DISCOVERED HYBRID ARCHETYPES:");
            foreach (var hybrid in promising)
            {
                Console.WriteLine($"  {hybrid.PrimaryArchetype}/{hybrid.SecondaryArchetype} ({hybrid.Ratio:F1}:1)");
                Console.WriteLine($"    Score: {hybrid.DiscoveryScore:F2}, Win Rate: {hybrid.WinRate:P0}, Games: {hybrid.GamesPlayed}");
            }
        }
    }

    public class MetagameTracker
    {
        private ConcurrentDictionary<string, float> _archetypePrevalence = new();
        private ConcurrentDictionary<string, int> _dominanceCounter = new();
        private const int MAX_DOMINANCE_GENERATIONS = 5;

        public void UpdateAfterGeneration(List<EvolvableIndividual> population)
        {
            var archetypeCounts = population
                .GroupBy(ind =>
                {
                    var archetype = ind.Dna?.ClassifyArchetype();
                    return string.IsNullOrEmpty(archetype) ? "Unknown" : archetype;
                })
                .ToDictionary(g => g.Key, g => g.Count());

            int total = population.Count;

            foreach (var archetype in new[] { "Aggro", "Control", "Combo", "Midrange", "Sacrifice", "Unknown" })
            {
                archetypeCounts.TryGetValue(archetype, out int count);
                float prevalence = total > 0 ? (float)count / total : 0;
                _archetypePrevalence[archetype] = prevalence;
            }

            var mostPrevalent = GetMostPrevalentArchetype();
            if (mostPrevalent.archetype != null && mostPrevalent.prevalence > 0.4f)
            {
                _dominanceCounter.AddOrUpdate(mostPrevalent.archetype, 1, (k, v) => v + 1);
            }
            else
            {
                foreach (var key in _dominanceCounter.Keys.ToList())
                {
                    if (key != mostPrevalent.archetype)
                    {
                        _dominanceCounter.AddOrUpdate(key, 0, (k, v) => Math.Max(0, v - 1));
                    }
                }
            }
        }

        public (string archetype, float prevalence) GetMostPrevalentArchetype()
        {
            if (_archetypePrevalence.IsEmpty)
                return (null, 0);

            var mostPrevalent = _archetypePrevalence
                .Where(kv => kv.Key != "Unknown")
                .OrderByDescending(kv => kv.Value)
                .FirstOrDefault();

            return (mostPrevalent.Key, mostPrevalent.Value);
        }

        public bool IsArchetypeDominant(string archetype)
        {
            if (string.IsNullOrEmpty(archetype))
                return false;

            return _dominanceCounter.TryGetValue(archetype, out int count) &&
                   count >= MAX_DOMINANCE_GENERATIONS;
        }

        public float GetAntiMetaBonus(EvolvableIndividual individual)
        {
            string archetype = individual.Dna?.ClassifyArchetype();

            if (string.IsNullOrEmpty(archetype))
                return 0;

            float bonus = 0;

            var mostPrevalent = GetMostPrevalentArchetype();

            if (mostPrevalent.archetype == null || mostPrevalent.prevalence <= 0)
                return 0;

            if (IsCounterTo(archetype, mostPrevalent.archetype))
            {
                bonus += mostPrevalent.prevalence * 10000f;
            }

            if (IsArchetypeDominant(mostPrevalent.archetype) && IsCounterTo(archetype, mostPrevalent.archetype))
            {
                bonus += 15000f;
            }

            if (archetype == mostPrevalent.archetype && mostPrevalent.prevalence > 0.35f)
            {
                bonus -= mostPrevalent.prevalence * 5000f;
            }

            return bonus;
        }

        private bool IsCounterTo(string archetype, string targetArchetype)
        {
            if (string.IsNullOrEmpty(targetArchetype) || string.IsNullOrEmpty(archetype))
                return false;

            var counterMap = new Dictionary<string, List<string>>
            {
                ["Aggro"] = new() { "Control", "Combo" },
                ["Control"] = new() { "Midrange", "Sacrifice" },
                ["Combo"] = new() { "Aggro", "Midrange" },
                ["Midrange"] = new() { "Aggro", "Sacrifice" },
                ["Sacrifice"] = new() { "Control", "Combo" }
            };

            return counterMap.ContainsKey(archetype) &&
                   counterMap[archetype].Contains(targetArchetype);
        }

        public Dictionary<string, float> GetArchetypePrevalence()
        {
            return _archetypePrevalence.ToDictionary(kv => kv.Key, kv => kv.Value);
        }
    }

    public class SynergyLearner
    {
        private ConcurrentDictionary<(int, int), float> _cardPairSynergy = new();
        private ConcurrentDictionary<int, ConcurrentDictionary<string, float>> _cardArchetypeAffinity = new();
        private const float LEARNING_RATE = 0.01f;

        public void LearnFromGame(EvolvableIndividual winner, EvolvableIndividual loser,
                                 int[] winnerDeck, int[] loserDeck)
        {
            LearnSynergiesFromDeck(winnerDeck, 0.005f);
            LearnAntiSynergiesFromDeck(loserDeck, -0.002f);

            string winnerArchetype = winner.Dna.ClassifyArchetype();
            foreach (var cardId in winnerDeck.Distinct())
            {
                var affinities = _cardArchetypeAffinity.GetOrAdd(cardId,
                    _ => new ConcurrentDictionary<string, float>());

                affinities.AddOrUpdate(winnerArchetype,
                    0.5f + LEARNING_RATE,
                    (key, oldValue) => Math.Clamp(oldValue + LEARNING_RATE, 0.1f, 0.9f));
            }
        }

        private void LearnSynergiesFromDeck(int[] deck, float strength)
        {
            var uniqueCards = deck.Distinct().ToList();

            for (int i = 0; i < uniqueCards.Count; i++)
            {
                for (int j = i + 1; j < uniqueCards.Count; j++)
                {
                    var pair = (Math.Min(uniqueCards[i], uniqueCards[j]),
                               Math.Max(uniqueCards[i], uniqueCards[j]));

                    _cardPairSynergy.AddOrUpdate(pair,
                        0.5f + strength,
                        (key, oldValue) => Math.Clamp(oldValue + strength, 0.1f, 0.9f));
                }
            }
        }

        private void LearnAntiSynergiesFromDeck(int[] deck, float strength)
        {
            var uniqueCards = deck.Distinct().ToList();

            for (int i = 0; i < uniqueCards.Count; i++)
            {
                for (int j = i + 1; j < uniqueCards.Count; j++)
                {
                    var pair = (Math.Min(uniqueCards[i], uniqueCards[j]),
                               Math.Max(uniqueCards[i], uniqueCards[j]));

                    _cardPairSynergy.AddOrUpdate(pair,
                        0.5f + strength,
                        (key, oldValue) => Math.Clamp(oldValue + strength, 0.1f, 0.9f));
                }
            }
        }
    }

    public class DiversityEnforcer
    {
        private ConcurrentBag<int[]> _recentDecks = new();
        private readonly object _lock = new object();
        private const int DECK_MEMORY = 100;
        private ConcurrentDictionary<string, float> _similarityCache = new();

        public float CalculateDeckNovelty(int[] deck)
        {
            var recentDecks = _recentDecks.ToArray();
            if (recentDecks.Length == 0) return 1.0f;

            string deckKey = string.Join(",", deck.OrderBy(x => x));
            if (_similarityCache.TryGetValue(deckKey, out float cachedNovelty))
                return cachedNovelty;

            float minSimilarity = float.MaxValue;
            foreach (var existingDeck in recentDecks)
            {
                float similarity = CalculateDeckSimilarity(deck, existingDeck);
                minSimilarity = Math.Min(minSimilarity, similarity);
            }

            float novelty = 1.0f - minSimilarity;
            _similarityCache[deckKey] = novelty;
            return novelty;
        }

        public void AddDeck(int[] deck)
        {
            lock (_lock)
            {
                _recentDecks.Add(deck.ToArray());

                if (_recentDecks.Count > DECK_MEMORY)
                {
                    var newBag = new ConcurrentBag<int[]>();
                    var recent = _recentDecks.Skip(_recentDecks.Count - DECK_MEMORY).ToArray();
                    foreach (var d in recent)
                    {
                        newBag.Add(d);
                    }
                    _recentDecks = newBag;
                }
            }
        }

        private float CalculateDeckSimilarity(int[] deck1, int[] deck2)
        {
            var dist1 = deck1.GroupBy(c => c).ToDictionary(g => g.Key, g => g.Count());
            var dist2 = deck2.GroupBy(c => c).ToDictionary(g => g.Key, g => g.Count());

            float totalDiff = 0;
            var allCards = dist1.Keys.Union(dist2.Keys);

            foreach (var cardId in allCards)
            {
                dist1.TryGetValue(cardId, out int count1);
                dist2.TryGetValue(cardId, out int count2);
                totalDiff += Math.Abs(count1 - count2);
            }

            return 1.0f - (totalDiff / (deck1.Length + deck2.Length));
        }
    }

    public class CardStatsEnhanced
    {
        public int CardId { get; }
        public string CardName { get; }
        public int TotalCopiesInPopulation { get; set; }
        public int DeckCount { get; set; }
        public int TotalDecks { get; set; }
        public float UseRate { get; set; }
        public float WinRate { get; private set; }  // Private setter - can only be set internally
        public int GamesWithCard => _gamesWithCard;  // Read-only property
        public int WinsWithCard => _winsWithCard;    // Read-only property
        public float AverageDensity { get; set; }
        public float PopularityScore { get; set; }

        private int _gamesWithCard = 0;
        private int _winsWithCard = 0;
        private readonly object _lock = new();

        // Constructor for required properties
        public CardStatsEnhanced(int cardId, string cardName)
        {
            CardId = cardId;
            CardName = cardName;
        }

        public void ResetUsageStats()
        {
            lock (_lock)
            {
                TotalCopiesInPopulation = 0;
                DeckCount = 0;
                AverageDensity = 0;
                UseRate = 0;
            }
        }

        public void UpdateUsageStats(int deckCount, int totalCopies, int totalDecks)
        {
            lock (_lock)
            {
                DeckCount = deckCount;
                TotalCopiesInPopulation = totalCopies;
                TotalDecks = totalDecks;
                AverageDensity = deckCount > 0 ? (float)totalCopies / deckCount : 0;
                UseRate = totalDecks > 0 ? (float)deckCount / totalDecks : 0;
            }
        }

        public void IncrementGames(bool won)
        {
            lock (_lock)
            {
                _gamesWithCard++;
                if (won) _winsWithCard++;
                WinRate = _gamesWithCard > 0 ? (float)_winsWithCard / _gamesWithCard : 0f;
            }

            // Update WinRate - needs lock for thread-safe calculation

        }

        // Method to set initial values (for loading from CSV)
        public void SetInitialGamesAndWins(int games, int wins)
        {
            lock (_lock)
            {
                _gamesWithCard = games;
                _winsWithCard = wins;
                UpdateWinRateFromCounters();
            }
        }

        // Helper method to update WinRate from current counters
        private void UpdateWinRateFromCounters()
        {
            lock (_lock)
            {
                WinRate = _gamesWithCard > 0 ? (float)_winsWithCard / _gamesWithCard : 0f;
            }
        }

        // Alternative: Method to set WinRate directly (if you really need it)
        // But this would create inconsistency with games/wins counters
        public void SetWinRateDirectly(float winRate)
        {
            lock (_lock)
            {
                WinRate = winRate;
                // Note: This doesn't update _gamesWithCard or _winsWithCard
                // Only use this if you're not tracking games/wins separately
            }
        }
    }

    public class PopulationData
    {
        public int Generation { get; set; }
        public List<EvolvableIndividual> Population { get; set; }
        public float BestFitness { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class ArchetypeHallOfFame
    {
        private ConcurrentDictionary<string, List<ArchetypeChampion>> _pureChampions = new();
        private ArchetypeChampion _bestHybrid = null;
        private const int MAX_PURE_CHAMPIONS = 2;
        private ConcurrentDictionary<int, bool> _duplicateCheckCache = new();

        public void Initialize()
        {
            string[] archetypes = { "Aggro", "Control", "Combo", "Midrange", "Sacrifice" };
            foreach (var archetype in archetypes)
                _pureChampions[archetype] = new List<ArchetypeChampion>();

            Load();
        }

        public void AddCandidate(EvolvableIndividual candidate, int generation,
                                float avgPopulationFitness, float versatilityScore)
        {
            if (candidate.BaseFitness < avgPopulationFitness * 0.8f)
                return;

            if (IsDuplicate(candidate.Dna))
                return;

            string archetype = candidate.Dna?.ClassifyArchetype();
            float purity = ArchetypePurityCalculator.CalculatePurity(candidate.Dna, archetype);

            if (purity >= 0.85f)
            {
                AddSpecialistChampion(candidate, generation, archetype, purity, versatilityScore);
            }
            else if (purity >= 0.60f && versatilityScore >= 0.6f)
            {
                AddFlexibleChampion(candidate, generation, archetype, purity, versatilityScore);
            }
            else if (purity <= 0.5f && versatilityScore >= 0.8f)
            {
                ConsiderForHybridSlot(candidate, generation, purity, versatilityScore);
            }
        }

        private void AddSpecialistChampion(EvolvableIndividual candidate, int generation,
                                         string archetype, float purity, float versatilityScore)
        {
            if (!_pureChampions.ContainsKey(archetype))
                _pureChampions[archetype] = new List<ArchetypeChampion>();

            var champion = new ArchetypeChampion
            {
                Individual = candidate.Clone(),
                GenerationAdded = generation,
                PurityScore = purity,
                IsHybrid = false,
                Archetype = archetype,
                VersatilityScore = versatilityScore,
                LastUpdated = generation,
                IsSpecialist = true,
                CompositeFitness = candidate.CompositeFitness
            };

            lock (_pureChampions)
            {
                if (_pureChampions.TryGetValue(archetype, out var champions))
                {
                    if (champions.Count >= MAX_PURE_CHAMPIONS)
                    {
                        var weakest = champions.OrderBy(c => c.CompositeFitness).First();
                        if (champion.CompositeFitness > weakest.CompositeFitness)
                        {
                            champions.Remove(weakest);
                            champions.Add(champion);
                        }
                    }
                    else
                    {
                        champions.Add(champion);
                    }

                    champions = champions
                        .OrderByDescending(c => c.CompositeFitness)
                        .ToList();
                    _pureChampions[archetype] = champions;
                }
            }

            Save();
        }

        private void AddFlexibleChampion(EvolvableIndividual candidate, int generation,
                                       string archetype, float purity, float versatilityScore)
        {
            lock (_pureChampions)
            {
                if (_pureChampions.TryGetValue(archetype, out var champions))
                {
                    var flexibleChampion = champions.FirstOrDefault(c => !c.IsSpecialist);

                    if (flexibleChampion == null || candidate.CompositeFitness > flexibleChampion.CompositeFitness)
                    {
                        if (flexibleChampion != null)
                            champions.Remove(flexibleChampion);

                        champions.Add(new ArchetypeChampion
                        {
                            Individual = candidate.Clone(),
                            GenerationAdded = generation,
                            PurityScore = purity,
                            IsHybrid = false,
                            Archetype = archetype,
                            VersatilityScore = versatilityScore,
                            LastUpdated = generation,
                            IsSpecialist = false,
                            CompositeFitness = candidate.CompositeFitness
                        });

                        if (champions.Count > MAX_PURE_CHAMPIONS)
                        {
                            var weakest = champions.OrderBy(c => c.CompositeFitness).First();
                            champions.Remove(weakest);
                        }

                        _pureChampions[archetype] = champions;
                    }
                }
            }

            Save();
        }

        private void ConsiderForHybridSlot(EvolvableIndividual candidate, int generation,
                                         float purity, float versatilityScore)
        {
            bool shouldReplace = false;

            if (_bestHybrid == null || _bestHybrid.Individual == null)
            {
                shouldReplace = true;
            }
            else
            {
                float currentBestFitness = _bestHybrid.CompositeFitness;
                float candidateFitness = candidate.CompositeFitness;

                if (candidateFitness > currentBestFitness * 1.05f ||
                    versatilityScore > _bestHybrid.VersatilityScore * 1.15f)
                {
                    shouldReplace = true;
                }
            }

            if (shouldReplace)
            {
                lock (this)
                {
                    _bestHybrid = new ArchetypeChampion
                    {
                        Individual = candidate.Clone(),
                        GenerationAdded = generation,
                        PurityScore = purity,
                        IsHybrid = true,
                        Archetype = GetHybridSignature(candidate.Dna),
                        VersatilityScore = versatilityScore,
                        LastUpdated = generation,
                        CompositeFitness = candidate.CompositeFitness
                    };
                }

                Save();
            }
        }

        private string GetHybridSignature(NewDna dna)
        {
            var strengths = ArchetypePurityCalculator.GetAllArchetypeStrengths(dna);
            var top2 = strengths.OrderByDescending(kvp => kvp.Value).Take(2).ToList();

            if (top2.Count < 2) return "Unknown";

            float ratio = top2[0].Value / Math.Max(0.1f, top2[1].Value);
            return $"{top2[0].Key}/{top2[1].Key} ({ratio:F1}:1)";
        }

        private bool IsDuplicate(NewDna dna)
        {
            int dnaHash = dna.GetHashCode();
            if (_duplicateCheckCache.TryGetValue(dnaHash, out bool isDuplicate))
                return isDuplicate;

            foreach (var kvp in _pureChampions)
            {
                foreach (var champ in kvp.Value)
                {
                    if (NewDna.CalculateDistance(dna, champ.Dna) < 0.1f)
                    {
                        _duplicateCheckCache[dnaHash] = true;
                        return true;
                    }
                }
            }

            if (_bestHybrid != null && NewDna.CalculateDistance(dna, _bestHybrid.Dna) < 0.15f)
            {
                _duplicateCheckCache[dnaHash] = true;
                return true;
            }

            _duplicateCheckCache[dnaHash] = false;
            return false;
        }

        public EvolvableIndividual GetBest(string archetype)
        {
            if (_pureChampions.TryGetValue(archetype, out var champions) && champions.Count > 0)
                return champions.OrderByDescending(c => c.CompositeFitness).First().Individual.Clone();

            return null;
        }

        public EvolvableIndividual GetBestSpecialist(string archetype)
        {
            if (_pureChampions.TryGetValue(archetype, out var champions))
            {
                var specialist = champions.FirstOrDefault(c => c.IsSpecialist);
                return specialist?.Individual?.Clone();
            }
            return null;
        }

        public EvolvableIndividual GetBestHybrid()
        {
            return _bestHybrid?.Individual?.Clone();
        }

        public List<EvolvableIndividual> GetAllChampions()
        {
            var all = new List<EvolvableIndividual>();

            foreach (var kvp in _pureChampions)
            {
                all.AddRange(kvp.Value.Select(c => c.Individual.Clone()));
            }

            if (_bestHybrid != null)
                all.Add(_bestHybrid.Individual.Clone());

            return all;
        }

        public List<EvolvableIndividual> GetSpecialistsForUnderrepresentedArchetypes(Dictionary<string, float> archetypePrevalence)
        {
            var result = new List<EvolvableIndividual>();

            foreach (var kvp in archetypePrevalence)
            {
                if (kvp.Value < 0.15f)
                {
                    var specialist = GetBestSpecialist(kvp.Key);
                    if (specialist != null)
                        result.Add(specialist);
                }
            }

            return result;
        }

        public void PrintStatus()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n=== ARCHETYPE HALL OF FAME (5+1) ===");
            Console.ResetColor();

            Console.WriteLine("SPECIALISTS (Purity >85%):");
            foreach (var kvp in _pureChampions.OrderBy(kvp => kvp.Key))
            {
                var specialist = kvp.Value.FirstOrDefault(c => c.IsSpecialist);
                if (specialist != null)
                {
                    Console.WriteLine($"  {kvp.Key}: #{specialist.Individual.Id} " +
                                    $"(Fitness: {specialist.CompositeFitness:F0}, " +
                                    $"Purity: {specialist.PurityScore:P0})");
                }
            }

            Console.WriteLine("\nFLEXIBLE (Purity 60-85%):");
            foreach (var kvp in _pureChampions.OrderBy(kvp => kvp.Key))
            {
                var flexible = kvp.Value.FirstOrDefault(c => !c.IsSpecialist);
                if (flexible != null)
                {
                    Console.WriteLine($"  {kvp.Key}: #{flexible.Individual.Id} " +
                                    $"(Fitness: {flexible.CompositeFitness:F0}, " +
                                    $"Versatility: {flexible.VersatilityScore:P0})");
                }
            }

            if (_bestHybrid != null)
            {
                Console.WriteLine("\nBEST HYBRID:");
                Console.WriteLine($"  {_bestHybrid.Archetype}: #{_bestHybrid.Individual.Id} " +
                                $"(Fitness: {_bestHybrid.CompositeFitness:F0})");
                Console.WriteLine($"     Purity: {_bestHybrid.PurityScore:P0} | " +
                                $"Versatility: {_bestHybrid.VersatilityScore:P0}");
                Console.WriteLine($"     Last Updated: Gen {_bestHybrid.LastUpdated}");
            }
        }

        private void Save()
        {
            try
            {
                var data = new HallOfFameData
                {
                    PureChampions = _pureChampions.ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value.Select(c => c.ToData()).ToList()
                    ),
                    BestHybrid = _bestHybrid?.ToData()
                };

                string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                Directory.CreateDirectory("EvolutionData");
                File.WriteAllText("EvolutionData/archetype_hall_of_fame.json", json);
            }
            catch { }
        }

        private void Load()
        {
            string path = "EvolutionData/archetype_hall_of_fame.json";
            if (!File.Exists(path)) return;

            try
            {
                string json = File.ReadAllText(path);
                var data = JsonSerializer.Deserialize<HallOfFameData>(json);

                if (data != null)
                {
                    if (data.PureChampions != null)
                    {
                        foreach (var kvp in data.PureChampions)
                        {
                            _pureChampions[kvp.Key] = kvp.Value
                                .Select(cd => ArchetypeChampion.FromData(cd))
                                .ToList();
                        }
                    }

                    if (data.BestHybrid != null)
                    {
                        _bestHybrid = ArchetypeChampion.FromData(data.BestHybrid);
                    }
                }
            }
            catch { }
        }
    }

    public class ArchetypeChampion
    {
        public EvolvableIndividual Individual { get; set; }
        public int GenerationAdded { get; set; }
        public float PurityScore { get; set; }
        public bool IsHybrid { get; set; }
        public string Archetype { get; set; }
        public float VersatilityScore { get; set; }
        public int LastUpdated { get; set; }
        public bool IsSpecialist { get; set; } = false;
        public float CompositeFitness { get; set; }

        public NewDna Dna => Individual?.Dna;

        public ChampionData ToData()
        {
            return new ChampionData
            {
                Individual = Individual,
                GenerationAdded = GenerationAdded,
                PurityScore = PurityScore,
                IsHybrid = IsHybrid,
                Archetype = Archetype,
                VersatilityScore = VersatilityScore,
                LastUpdated = LastUpdated,
                IsSpecialist = IsSpecialist,
                CompositeFitness = CompositeFitness
            };
        }

        public static ArchetypeChampion FromData(ChampionData data)
        {
            return new ArchetypeChampion
            {
                Individual = data.Individual,
                GenerationAdded = data.GenerationAdded,
                PurityScore = data.PurityScore,
                IsHybrid = data.IsHybrid,
                Archetype = data.Archetype,
                VersatilityScore = data.VersatilityScore,
                LastUpdated = data.LastUpdated,
                IsSpecialist = data.IsSpecialist,
                CompositeFitness = data.CompositeFitness
            };
        }
    }

    public class HallOfFameData
    {
        public Dictionary<string, List<ChampionData>> PureChampions { get; set; }
        public ChampionData BestHybrid { get; set; }
    }

    public class ChampionData
    {
        public EvolvableIndividual Individual { get; set; }
        public int GenerationAdded { get; set; }
        public float PurityScore { get; set; }
        public bool IsHybrid { get; set; }
        public string Archetype { get; set; }
        public float VersatilityScore { get; set; }
        public int LastUpdated { get; set; }
        public bool IsSpecialist { get; set; }
        public float CompositeFitness { get; set; }
    }

    public static class ArchetypePurityCalculator
    {
        private static readonly ConcurrentDictionary<(string, string), float> _purityCache = new();
        private static readonly ConcurrentDictionary<string, Dictionary<string, float>> _strengthCache = new();

        public static float CalculatePurity(NewDna dna, string targetArchetype)
        {
            string cacheKey = $"{dna.GetHashCode()}_{targetArchetype}";
            if (_purityCache.TryGetValue((cacheKey, targetArchetype), out float cached))
                return cached;

            float targetScore = GetArchetypeStrength(dna, targetArchetype);

            string[] allArchetypes = { "Aggro", "Control", "Combo", "Midrange", "Sacrifice" };
            float maxOtherScore = allArchetypes
                .Where(a => a != targetArchetype)
                .Max(a => GetArchetypeStrength(dna, a));

            if (targetScore <= 0)
            {
                _purityCache[(cacheKey, targetArchetype)] = 0;
                return 0;
            }

            float purity = (targetScore - maxOtherScore) / targetScore;
            purity = Math.Clamp(purity, 0, 1);
            _purityCache[(cacheKey, targetArchetype)] = purity;
            return purity;
        }

        public static float GetArchetypeStrength(NewDna dna, string archetype)
        {
            string cacheKey = $"{dna.GetHashCode()}_{archetype}";
            if (_purityCache.TryGetValue((cacheKey, archetype), out float cached))
                return cached;

            float strength = archetype switch
            {
                "Aggro" => dna.Genes[NewDna.STYLE_AGGRO] * 1.5f + dna.Genes[NewDna.TARGETING_FACE_VS_BOARD_BIAS] * 0.5f,
                "Control" => dna.Genes[NewDna.STYLE_CONTROL] * 1.5f + dna.Genes[NewDna.LOGIC_HAND_SIZE_OPTIMIZATION] * 0.5f,
                "Combo" => dna.Genes[NewDna.STYLE_COMBO] * 1.5f + dna.Genes[NewDna.LOGIC_TUTOR_PRECISION] * 0.5f,
                "Midrange" => dna.Genes[NewDna.STYLE_MIDRANGE] * 1.5f + dna.Genes[NewDna.COMBAT_TRADING_EFFICIENCY] * 0.5f,
                "Sacrifice" => (dna.Genes[NewDna.TRIGGER_SACRIFICE_VISION] * 1.0f +
                               dna.Genes[NewDna.TRIGGER_DEATH_STRATEGY] * 0.5f +
                               dna.Genes[NewDna.TARGETING_FRIENDLY_SACRIFICE_VALUE] * 0.3f),
                _ => 0
            };

            _purityCache[(cacheKey, archetype)] = strength;
            return strength;
        }

        public static Dictionary<string, float> GetAllArchetypeStrengths(NewDna dna)
        {
            string cacheKey = dna.GetHashCode().ToString();
            if (_strengthCache.TryGetValue(cacheKey, out var cached))
                return cached;

            var strengths = new Dictionary<string, float>
            {
                ["Aggro"] = GetArchetypeStrength(dna, "Aggro"),
                ["Control"] = GetArchetypeStrength(dna, "Control"),
                ["Combo"] = GetArchetypeStrength(dna, "Combo"),
                ["Midrange"] = GetArchetypeStrength(dna, "Midrange"),
                ["Sacrifice"] = GetArchetypeStrength(dna, "Sacrifice")
            };

            _strengthCache[cacheKey] = strengths;
            return strengths;
        }

        public static void EnforceArchetype(NewDna dna, string archetype, Random rng, float strength = 0.8f)
        {
            var targetGenes = GetArchetypeGenes(archetype);
            var otherArchetypes = new[] { "Aggro", "Control", "Combo", "Midrange", "Sacrifice" }
                .Where(a => a != archetype);

            foreach (var geneIndex in targetGenes)
            {
                dna.Genes[geneIndex] = Math.Min(10, dna.Genes[geneIndex] + (10 - dna.Genes[geneIndex]) * strength);
            }

            foreach (var otherArchetype in otherArchetypes)
            {
                var otherGenes = GetArchetypeGenes(otherArchetype);
                foreach (var geneIndex in otherGenes)
                {
                    if (rng.NextDouble() < 0.5f)
                    {
                        dna.Genes[geneIndex] = dna.Genes[geneIndex] * (1 - strength * 0.5f);
                    }
                }
            }

            _purityCache.Clear();
            _strengthCache.Clear();
        }

        private static int[] GetArchetypeGenes(string archetype)
        {
            return archetype switch
            {
                "Aggro" => new[] {
                    NewDna.STYLE_AGGRO,
                    NewDna.STYLE_TEMPO_PREFERENCE,
                    NewDna.TARGETING_FACE_VS_BOARD_BIAS,
                    NewDna.LOGIC_MANA_CURVE_BIAS
                },
                "Control" => new[] {
                    NewDna.STYLE_CONTROL,
                    NewDna.STYLE_FUTURE_PLANNING_BIAS,
                    NewDna.LOGIC_HAND_SIZE_OPTIMIZATION,
                    NewDna.LOGIC_PATIENCE_FACTOR
                },
                "Combo" => new[] {
                    NewDna.STYLE_COMBO,
                    NewDna.LOGIC_TUTOR_PRECISION,
                    NewDna.TRIGGER_SETUP_RECOGNITION,
                    NewDna.PSYCH_PREDICTIVE_CAUTION
                },
                "Midrange" => new[] {
                    NewDna.STYLE_MIDRANGE,
                    NewDna.UNIT_STAT_VS_EFFECT_WEIGHT,
                    NewDna.COMBAT_TRADING_EFFICIENCY,
                    NewDna.LOGIC_MANA_CURVE_BIAS
                },
                "Sacrifice" => new[] {
                    NewDna.TRIGGER_SACRIFICE_VISION,
                    NewDna.TRIGGER_DEATH_STRATEGY,
                    NewDna.TARGETING_FRIENDLY_SACRIFICE_VALUE,
                    NewDna.KEYWORD_MARKED_FOCUS
                },
                _ => Array.Empty<int>()
            };
        }
    }

    public class CrossArchetypeStats
    {
        public ConcurrentDictionary<string, int> GamesAgainst { get; set; } = new();
        public ConcurrentDictionary<string, int> WinsAgainst { get; set; } = new();
        public ConcurrentDictionary<string, float> WinRates { get; set; } = new();

        public float GetOverallWinRate()
        {
            if (GamesAgainst.IsEmpty) return 0;

            int totalGames = 0;
            int totalWins = 0;

            foreach (var kvp in GamesAgainst)
            {
                totalGames += kvp.Value;
                WinsAgainst.TryGetValue(kvp.Key, out int wins);
                totalWins += wins;
            }

            return totalGames > 0 ? (float)totalWins / totalGames : 0;
        }
    }

    public class IntraArchetypeNoveltyArchive
    {
        private ConcurrentDictionary<string, ConcurrentBag<BehaviorVector>> _archive = new();
        private const int MAX_VECTORS_PER_ARCHETYPE = 50;
        private const float NOVELTY_THRESHOLD = 0.7f;

        public float CalculateNovelty(EvolvableIndividual individual, List<EvolvableIndividual> population)
        {
            string archetype = individual.Dna.ClassifyArchetype();

            var archive = _archive.GetOrAdd(archetype, _ => new ConcurrentBag<BehaviorVector>());
            var behavior = ExtractBehaviorVector(individual);

            var allBehaviors = archive.ToList()
                .Concat(population
                    .Where(p => p.Dna.ClassifyArchetype() == archetype && p != individual)
                    .Select(ExtractBehaviorVector))
                .Where(b => b.Values.Count >= 3)
                .ToList();

            if (allBehaviors.Count < 2) return 0.5f;

            var distances = allBehaviors
                .Where(b => b.IndividualId != behavior.IndividualId)
                .Select(b => CalculateDistance(behavior, b))
                .OrderBy(d => d)
                .ToList();

            int k = Math.Min(10, distances.Count);
            if (k == 0) return 0;

            float novelty = distances.Take(k).Average();

            if (novelty > NOVELTY_THRESHOLD)
            {
                var currentArchive = _archive.GetOrAdd(archetype, _ => new ConcurrentBag<BehaviorVector>());
                if (CountVectors(archetype) < MAX_VECTORS_PER_ARCHETYPE)
                {
                    currentArchive.Add(behavior);
                }
            }

            return novelty;
        }

        private int CountVectors(string archetype)
        {
            if (_archive.TryGetValue(archetype, out var bag))
                return bag.Count;
            return 0;
        }

        private BehaviorVector ExtractBehaviorVector(EvolvableIndividual individual)
        {
            return new BehaviorVector
            {
                IndividualId = individual.Id,
                Values = individual.Behaviors.ToList(),
                Archetype = individual.Dna.ClassifyArchetype(),
                DnaSignature = individual.Dna.GetBehaviorVector()
            };
        }

        private float CalculateDistance(BehaviorVector a, BehaviorVector b)
        {
            if (a.Values.Count == 0 || b.Values.Count == 0) return 1.0f;

            int minCount = Math.Min(a.Values.Count, b.Values.Count);
            float sumSq = 0;

            for (int i = 0; i < minCount; i++)
            {
                float diff = a.Values[i] - b.Values[i];
                sumSq += diff * diff;
            }

            float dnaDistance = CalculateDnaDistance(a.DnaSignature, b.DnaSignature);
            sumSq += dnaDistance * dnaDistance * 0.3f;

            return (float)Math.Sqrt(sumSq) / (minCount + 0.3f);
        }

        private float CalculateDnaDistance(float[] a, float[] b)
        {
            if (a.Length != b.Length) return 1.0f;

            float sum = 0;
            for (int i = 0; i < a.Length; i++)
            {
                float diff = a[i] - b[i];
                sum += diff * diff;
            }

            return (float)Math.Sqrt(sum) / a.Length;
        }

        public void Clear()
        {
            _archive.Clear();
        }

        private class BehaviorVector
        {
            public int IndividualId { get; set; }
            public List<float> Values { get; set; } = new();
            public string Archetype { get; set; }
            public float[] DnaSignature { get; set; }
        }
    }


    public static class GameStateExtensions
    {
        public static bool IsGameOver(this GameState state)
        {
            return state.PlayerA.Health <= 0 || state.PlayerB.Health <= 0;
        }
    }

    public class EvolutionRunnerV2
    {
        private const int OPTIMAL_PARALLELISM = 24;
        private const int OPTIMAL_POPULATION = 100;
        private const float OPTIMAL_MUTATION = 0.14f;
        private const int OPTIMAL_IMMIGRANTS = 22;
        private const int OPTIMAL_MATCHES = 6;
        private const int OPTIMAL_MAX_MOVES = 120;
        private const int OPTIMAL_TOURNAMENT = 3;
        private const int OPTIMAL_ELITES = 3;
        private Dictionary<int, CardStatsEnhanced> _statsSnapshot = new();

        private const int POPULATION_SIZE = OPTIMAL_POPULATION;
        private const float SPECIATION_THRESHOLD = 0.3f;
        private const float MUTATION_RATE = OPTIMAL_MUTATION;
        private const int IMMIGRANTS_PER_GEN = OPTIMAL_IMMIGRANTS;
        private const int TOURNAMENT_SIZE = OPTIMAL_TOURNAMENT;
        private const int ELITES_PER_SPECIES = OPTIMAL_ELITES;

        public static ConcurrentDictionary<int, CardStatsEnhanced> GlobalStats = new();
        private List<EvolvableIndividual> _population = new();
        private List<Species> _species = new();
        private IntraArchetypeNoveltyArchive _intraArchetypeNovelty = new();
        private ArchetypeHallOfFame _hallOfFame = new();
        private BotArchetypePool _archetypePool = new();
        private int _generation = 0;
        private int _stagnationCounter = 0;
        private float _bestFitness = -1000000f;
        private ConcurrentDictionary<int, CardStatsEnhanced> _cardStats = new();
        private ConcurrentDictionary<string, CrossArchetypeStats> _crossArchetypeStats = new();

        private MetagameTracker _metagameTracker = new();
        private DiversityEnforcer _diversityEnforcer = new();
        private SynergyLearner _synergyLearner = new();
        private CardExplorationSystem _cardExploration;
        private AdaptiveFitnessOptimizer _fitnessOptimizer = new();
        private ArchetypeSpecialistOptimizer _specialistOptimizer;
        private HybridDiscoveryEngine _hybridDiscovery;

        private static readonly ThreadLocal<Random> _threadRng = new(() =>
            new Random(Guid.NewGuid().GetHashCode() ^ Environment.TickCount));

        private int[] _collectibleCardIds;
        private Stopwatch _generationTimer = new Stopwatch();
        private int _totalGamesLastGen = 0;
        private Dictionary<string, float> _currentArchetypeDistribution = new();
        private ConcurrentDictionary<int, int[]> _individualDeckCache = new();
        private ConcurrentDictionary<int, float> _individualFitnessCache = new();

        private void CreateStatsSnapshot()
        {

            _statsSnapshot = _cardStats.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value
            );
        }

        public async Task RunEvolutionAsync()
        {
            Console.Clear();

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("🚀 EVOLUTION 5.0 - OPTIMAL SPECIALIST DISCOVERY");
            Console.WriteLine("   Finding best bots for each archetype + optimal hybrids\n");
            Console.ResetColor();

            CheckForAutoResume();
            ProcessDataManagement();

            CardLibrary.Instance.LoadFromJson("Data/Cards/cards.json");
            LoadCollectibleIds();

            _cardExploration = new CardExplorationSystem(_collectibleCardIds);
            _specialistOptimizer = new ArchetypeSpecialistOptimizer(_cardExploration, _collectibleCardIds);
            _hybridDiscovery = new HybridDiscoveryEngine(_collectibleCardIds);

            Console.WriteLine("=== EVOLUTION RUNNER 5.0 OPTIMIZED ===");

            bool loadPopulation = HandleResumeDecision();

            if (loadPopulation)
            {
                if (LoadLatestPopulation())
                {
                    Console.WriteLine($"✓ Continuing from generation {_generation}...");
                }
                else
                {
                    Console.WriteLine("⚠️  Could not load saved population. Initializing optimized population...");
                    InitializeOptimizedPopulation();
                }
            }
            else
            {
                Console.WriteLine("Starting optimized population...");
                InitializeOptimizedPopulation();
            }

            _hallOfFame.Initialize();
            _archetypePool.Initialize();

            PrintHardwareInfo();

            Console.WriteLine($"Population: {POPULATION_SIZE} | Species Threshold: {SPECIATION_THRESHOLD}");
            Console.WriteLine($"Mutation Rate: {MUTATION_RATE:P0} | Immigrants: {IMMIGRANTS_PER_GEN}");
            Console.WriteLine($"Matches per bot: {OPTIMAL_MATCHES} random + 2 archetype");
            Console.WriteLine($"Specialist Optimization: ACTIVE | Hybrid Discovery: ACTIVE");
            Console.ResetColor();

            bool shouldStop = false;

            EnsureDirectories();

            while (!shouldStop)
            {
                _generation++;
                _generationTimer.Restart();

                Console.WriteLine($"\n=== GENERATION {_generation:D3} ===");

                UpdateExplorationSystems();

                if (_generation % 5 == 0)
                {
                    AnalyzeConvergence();
                }

                PerformSpeciation();
                Console.WriteLine($"Species: {_species.Count} | Diversity: {CalculatePopulationDiversity():F3}");
                UpdateCardStatistics();
                CreateStatsSnapshot();
                EvaluatePopulation();
                DebugCardStats();
                UpdateHallOfFame();
                LogGenerationStats();
                CheckStagnation();
                BreedOptimizedPopulation();
                SaveLatestPopulation();

                if (_generation % 5 == 0)
                {
                    PerformAnalytics();
                    SaveBestDecks();
                }

                if (_generation % 10 == 0)
                {
                    SaveCheckpoint();
                    _hallOfFame.PrintStatus();
                    _cardExploration.PrintExplorationStatus();
                    _specialistOptimizer.PrintSpecialistTargets();
                    _hybridDiscovery.PrintDiscoveredHybrids();
                    _fitnessOptimizer.PrintWeights();
                }

                if (Console.KeyAvailable)
                {
                    if (Console.ReadKey(true).Key == ConsoleKey.Escape)
                        shouldStop = true;
                }

                _generationTimer.Stop();
                LogPerformanceMetrics();

                await Task.Delay(100);
            }
        }

        private Dictionary<string, int> CalculateArchetypeQuotas()
        {
            var quotas = new Dictionary<string, int>
            {
                ["Aggro"] = POPULATION_SIZE / 6,      // ~16%
                ["Control"] = POPULATION_SIZE / 6,    // ~16%
                ["Combo"] = POPULATION_SIZE / 6,      // ~16%
                ["Midrange"] = POPULATION_SIZE / 6,   // ~16%
                ["Sacrifice"] = POPULATION_SIZE / 6,  // ~16%
                ["Hybrid"] = POPULATION_SIZE / 6      // ~16%
            };

            // Adjust based on current distribution
            foreach (var kvp in _currentArchetypeDistribution)
            {
                if (quotas.ContainsKey(kvp.Key))
                {
                    if (kvp.Value > 0.25f)  // Reduce quota for over-represented
                        quotas[kvp.Key] = (int)(POPULATION_SIZE * 0.12f);
                    else if (kvp.Value < 0.10f)  // Increase quota for under-represented
                        quotas[kvp.Key] = (int)(POPULATION_SIZE * 0.20f);
                }
            }

            return quotas;
        }

        private void UpdateExplorationSystems()
        {
            _cardExploration.UpdateExplorationCandidates(_cardStats, _population);
            _specialistOptimizer.UpdateSpecialistTargets(_cardStats, _population);
            _hybridDiscovery.AnalyzePopulationForHybrids(_population, _cardStats);

            float currentBestFitness = _population.Max(x => x.CompositeFitness);
            float diversity = CalculatePopulationDiversity();
            _fitnessOptimizer.UpdateConvergenceState(currentBestFitness, diversity,
                                                    _generation, _stagnationCounter);
        }

        private void InitializeOptimizedPopulation()
        {
            _population.Clear();
            _individualDeckCache.Clear();
            _individualFitnessCache.Clear();
            var rng = _threadRng.Value;

            var archetypes = new[] { "Aggro", "Control", "Combo", "Midrange", "Sacrifice" };
            int specialistsPerArchetype = POPULATION_SIZE / (archetypes.Length + 2);

            foreach (var archetype in archetypes)
            {
                for (int i = 0; i < specialistsPerArchetype; i++)
                {
                    var dna = NewDna.CreateArchetypeDNA(new BotArchetype(archetype), rng);
                    dna = OptimizeDNAForSpecialist(dna, archetype, rng);
                    _population.Add(new EvolvableIndividual(dna, _generation));
                }
            }

            for (int i = 0; i < 10; i++)
            {
                var parentArchetypes = archetypes.OrderBy(x => rng.Next()).Take(2).ToList();
                var dna1 = NewDna.CreateArchetypeDNA(new BotArchetype(parentArchetypes[0]), rng);
                var dna2 = NewDna.CreateArchetypeDNA(new BotArchetype(parentArchetypes[1]), rng);

                var hybridDNA = NewDna.Crossover(dna1, dna2, rng);
                hybridDNA.Mutate(rng, 5);
                _population.Add(new EvolvableIndividual(hybridDNA, _generation));
            }

            while (_population.Count < POPULATION_SIZE)
            {
                if (rng.NextDouble() < 0.6f)
                {
                    var randomArchetype = archetypes[rng.Next(archetypes.Length)];
                    var dna = NewDna.CreateArchetypeDNA(new BotArchetype(randomArchetype), rng);
                    _population.Add(new EvolvableIndividual(dna, _generation));
                }
                else
                {
                    var dna = NewDna.CreateRandom(rng);
                    _population.Add(new EvolvableIndividual(dna, _generation));
                }
            }

            Console.WriteLine($"Optimized population initialized: {_population.Count} individuals");
        }

        private NewDna OptimizeDNAForSpecialist(NewDna dna, string archetype, Random rng)
        {
            ArchetypePurityCalculator.EnforceArchetype(dna, archetype, rng, 0.9f);

            var keyGenes = GetKeyArchetypeGenes(archetype);
            foreach (var geneIndex in keyGenes)
            {
                dna.Genes[geneIndex] = Math.Min(10, dna.Genes[geneIndex] * 1.3f);
            }

            return dna;
        }

        private int[] GetKeyArchetypeGenes(string archetype)
        {
            return archetype switch
            {
                "Aggro" => new[] { NewDna.STYLE_AGGRO, NewDna.TARGETING_FACE_VS_BOARD_BIAS, NewDna.COMBAT_TRADING_EFFICIENCY },
                "Control" => new[] { NewDna.STYLE_CONTROL, NewDna.LOGIC_HAND_SIZE_OPTIMIZATION, NewDna.LOGIC_PATIENCE_FACTOR },
                "Combo" => new[] { NewDna.STYLE_COMBO, NewDna.LOGIC_TUTOR_PRECISION, NewDna.TRIGGER_SETUP_RECOGNITION },
                "Midrange" => new[] { NewDna.STYLE_MIDRANGE, NewDna.UNIT_STAT_VS_EFFECT_WEIGHT, NewDna.COMBAT_TRADING_EFFICIENCY },
                "Sacrifice" => new[] { NewDna.TRIGGER_SACRIFICE_VISION, NewDna.TRIGGER_DEATH_STRATEGY, NewDna.TARGETING_FRIENDLY_SACRIFICE_VALUE },
                _ => Array.Empty<int>()
            };
        }

        private float CalculateEnhancedCompositeFitness(EvolvableIndividual individual)
        {
            if (individual?.Dna == null)
                return 1000;

            int cacheKey = individual.Id;
            if (_individualFitnessCache.TryGetValue(cacheKey, out float cached))
                return cached;

            string archetype = individual.Dna.ClassifyArchetype() ?? "Unknown";
            var rng = _threadRng.Value;

            float baseFitness = individual.BaseFitness;
            float deckNovelty = individual.DeckNoveltyScore;
            float synergyBonus = CalculateDeckSynergyBonus(individual);

            // Use both consistency bonuses
            float deckConsistency = CalculateDeckConsistencyBonus(individual);
            float strategyConsistency = CalculateConsistencyBonus(individual, archetype);
            float consistencyBonus = (deckConsistency * 0.7f) + (strategyConsistency * 0.3f);

            float rareCardBonus = CalculateEnhancedRareCardBonus(individual);
            float explorationBonus = CalculateExplorationBonus(individual, archetype);
            float hybridBonus = _hybridDiscovery.CalculateHybridPotentialBonus(individual);
            float specialistBonus = _specialistOptimizer.EvaluateSpecialistQuality(individual);
            float antiMetaBonus = _metagameTracker.GetAntiMetaBonus(individual) * 0.1f;

            // CRITICAL FIX: Remove the 1000f floor - it's forcing all values to 1000
            float totalFitness = _fitnessOptimizer.CalculateOptimizedFitness(
                individual,
                baseFitness,
                deckNovelty,
                synergyBonus,
                consistencyBonus,
                rareCardBonus + specialistBonus,
                explorationBonus,
                hybridBonus
            );

            float archetypeBalanceBonus = CalculateArchetypeBalanceBonus(individual, archetype);
            totalFitness += archetypeBalanceBonus;
            totalFitness += antiMetaBonus;

            // FIX: Remove the Math.Max(totalFitness, 1000f) - this is causing the problem!
            // Instead, use a reasonable minimum to avoid negative values destroying selection pressure
            totalFitness = Math.Max(totalFitness, 10f);  // Changed from 1000f to 10f

            _individualFitnessCache[cacheKey] = totalFitness;
            return totalFitness;
        }

        private float CalculateArchetypeBalanceBonus(EvolvableIndividual individual, string archetype)
        {
            if (_currentArchetypeDistribution.TryGetValue(archetype, out float prevalence))
            {
                // Scale these WAY down - they were too aggressive
                if (prevalence > 0.25f)  // If archetype has >25% representation
                {
                    return -Math.Abs(individual.BaseFitness) * (prevalence - 0.25f) * 0.1f;  // Reduced from 2.0f
                }
                else if (prevalence < 0.10f)  // If archetype has <10% representation
                {
                    return Math.Abs(individual.BaseFitness) * (0.10f - prevalence) * 0.15f;  // Reduced from 3.0f
                }
            }
            return 0f;
        }

        private float CalculateEnhancedRareCardBonus(EvolvableIndividual individual)
        {
            var deck = GetCachedDeck(individual);
            var uniqueCards = deck.Distinct();

            float bonus = 0;
            int rareCardsCount = 0;
            int totalGamesThreshold = 100;

            foreach (var cardId in uniqueCards)
            {
                if (_cardStats.TryGetValue(cardId, out var stat))
                {
                    bool isUnderused = stat.UseRate < 0.15f && stat.GamesWithCard > totalGamesThreshold;
                    bool isIgnored = stat.UseRate < 0.05f;
                    bool isExperimental = stat.UseRate < 0.25f && stat.WinRate > 0.55f;

                    if (isIgnored)
                    {
                        float rarityMultiplier = 1.0f + (0.05f - stat.UseRate) * 20f;
                        float cardBonus = 8000f * rarityMultiplier;

                        if (stat.WinRate > 0.45f)
                            cardBonus *= 1.5f;

                        bonus += cardBonus;
                        rareCardsCount++;
                    }
                    else if (isUnderused && stat.WinRate > 0.48f)
                    {
                        bonus += 3000f * (0.15f - stat.UseRate);
                        rareCardsCount++;
                    }
                    else if (isExperimental)
                    {
                        bonus += 5000f * stat.WinRate;
                        rareCardsCount++;
                    }
                }
            }

            if (rareCardsCount > 12)
                bonus *= 0.7f;
            if (rareCardsCount > 15)
                bonus *= 0.5f;

            return Math.Min(bonus, individual.BaseFitness * 0.08f);
        }

        private float CalculateExplorationBonus(EvolvableIndividual individual, string archetype)
        {
            var deck = GetCachedDeck(individual);
            float bonus = 0;

            var candidates = _cardExploration.GetCandidatesForArchetype(archetype);

            foreach (var candidate in candidates.Take(5))
            {
                if (deck.Contains(candidate.CardId))
                {
                    float cardBonus = candidate.ExplorationScore * 6000f;

                    if (candidate.CurrentUseRate < 0.1f)
                        cardBonus *= 1.5f;

                    if (candidate.WinRate > 0.52f)
                        cardBonus *= 1.3f;

                    bonus += cardBonus;
                }
            }

            return bonus;
        }

        private void BreedOptimizedPopulation()
        {
            var newPopulation = new List<EvolvableIndividual>();
            var rng = _threadRng.Value;
            _individualDeckCache.Clear();
            _individualFitnessCache.Clear();

            // FIX: Implement archetype quotas for balanced breeding
            var archetypeQuotas = CalculateArchetypeQuotas();
            var archetypeCounts = new Dictionary<string, int>
            {
                ["Aggro"] = 0,
                ["Control"] = 0,
                ["Combo"] = 0,
                ["Midrange"] = 0,
                ["Sacrifice"] = 0,
                ["Hybrid"] = 0,
                ["Unknown"] = 0
            };

            // FIX: Select elites per archetype, not globally
            var byArchetype = _population
                .GroupBy(x => x.Dna.ClassifyArchetype())
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.CompositeFitness).ToList());

            var elites = new List<EvolvableIndividual>();

            // Take 20% best from each archetype as elites
            foreach (var kvp in byArchetype)
            {
                string archetype = kvp.Key;
                var archetypeGroup = kvp.Value;

                int archetypeElites = Math.Max(1, (int)(archetypeGroup.Count * 0.2f));
                elites.AddRange(archetypeGroup.Take(archetypeElites).Select(e => e.Clone()));

                archetypeCounts[archetype] += archetypeElites;
            }

            newPopulation.AddRange(elites);

            // Fill population according to archetype quotas
            var allArchetypes = new[] { "Aggro", "Control", "Combo", "Midrange", "Sacrifice", "Hybrid" };

            // First, ensure minimum representation for each archetype
            foreach (var archetype in allArchetypes)
            {
                if (!byArchetype.ContainsKey(archetype))
                    byArchetype[archetype] = new List<EvolvableIndividual>();

                int targetCount = archetypeQuotas[archetype];
                int currentCount = archetypeCounts.GetValueOrDefault(archetype, 0);

                if (currentCount < targetCount)
                {
                    // Create new specialists for this archetype
                    int needed = targetCount - currentCount;

                    for (int i = 0; i < needed; i++)
                    {
                        if (byArchetype[archetype].Count > 0 && rng.NextDouble() < 0.7f)
                        {
                            // Breed within archetype - FIXED: Use TournamentSelectWithDiversity
                            var parent1 = TournamentSelectWithDiversity(byArchetype[archetype], archetype);
                            var parent2 = TournamentSelectWithDiversity(byArchetype[archetype], archetype);

                            var childDNA = NewDna.Crossover(parent1.Dna, parent2.Dna, rng);
                            childDNA = OptimizeSpecialistDNA(childDNA, archetype, rng);

                            if (rng.NextDouble() < MUTATION_RATE)
                                childDNA.Mutate(rng, _stagnationCounter);

                            newPopulation.Add(new EvolvableIndividual(childDNA, _generation));
                            archetypeCounts[archetype]++;
                        }
                        else
                        {
                            // Create fresh specialist
                            var dna = NewDna.CreateArchetypeDNA(new BotArchetype(archetype), rng);
                            newPopulation.Add(new EvolvableIndividual(dna, _generation));
                            archetypeCounts[archetype]++;
                        }
                    }
                }
            }

            // Add hybrids (5-10% of population)
            int hybridTarget = POPULATION_SIZE / 10;
            var promisingHybrids = _hybridDiscovery.GetPromisingHybrids(5);

            for (int i = 0; i < hybridTarget; i++)
            {
                if (promisingHybrids.Count > 0 && rng.NextDouble() < 0.7f)
                {
                    var hybridPattern = promisingHybrids[rng.Next(promisingHybrids.Count)];
                    var dna1 = NewDna.CreateArchetypeDNA(new BotArchetype(hybridPattern.PrimaryArchetype), rng);
                    var dna2 = NewDna.CreateArchetypeDNA(new BotArchetype(hybridPattern.SecondaryArchetype), rng);

                    var childDNA = NewDna.Crossover(dna1, dna2, rng);
                    AdjustDNAForHybrid(childDNA, hybridPattern, rng);

                    if (rng.NextDouble() < 0.5f)
                        childDNA.Mutate(rng, _stagnationCounter);

                    newPopulation.Add(new EvolvableIndividual(childDNA, _generation));
                    archetypeCounts["Hybrid"]++;
                }
                else
                {
                    // Create random hybrid between two different archetypes
                    var archetypes = allArchetypes.Where(a => a != "Hybrid").ToArray();
                    if (archetypes.Length >= 2)
                    {
                        var archetype1 = archetypes[rng.Next(archetypes.Length)];
                        var archetype2 = archetypes.Where(a => a != archetype1).ToArray();
                        if (archetype2.Length > 0)
                        {
                            var dna1 = NewDna.CreateArchetypeDNA(new BotArchetype(archetype1), rng);
                            var dna2 = NewDna.CreateArchetypeDNA(new BotArchetype(archetype2[rng.Next(archetype2.Length)]), rng);

                            var childDNA = NewDna.Crossover(dna1, dna2, rng);
                            newPopulation.Add(new EvolvableIndividual(childDNA, _generation));
                            archetypeCounts["Hybrid"]++;
                        }
                    }
                }
            }

            // Fill remaining slots with diversity-focused bots
            int remaining = POPULATION_SIZE - newPopulation.Count;
            for (int i = 0; i < remaining; i++)
            {
                // Prioritize underrepresented archetypes
                var underrepresented = archetypeCounts
                    .Where(kvp => kvp.Key != "Hybrid" && kvp.Key != "Unknown")
                    .Where(kvp => (float)kvp.Value / newPopulation.Count < 0.12f)
                    .Select(kvp => kvp.Key)
                    .ToList();

                string archetype;
                if (underrepresented.Count > 0 && rng.NextDouble() < 0.8f)
                {
                    archetype = underrepresented[rng.Next(underrepresented.Count)];
                }
                else
                {
                    archetype = allArchetypes[rng.Next(allArchetypes.Length)];
                }

                var dna = NewDna.CreateArchetypeDNA(new BotArchetype(archetype), rng);
                dna = ForceExplorationCards(dna, archetype, rng, 1);
                newPopulation.Add(new EvolvableIndividual(dna, _generation));
            }

            _population = newPopulation.Take(POPULATION_SIZE).ToList();
        }



        private EvolvableIndividual TournamentSelect(List<EvolvableIndividual> individuals, Random rng, int size = 3)
        {
            if (individuals.Count == 0) return null;
            if (individuals.Count <= size) return individuals[rng.Next(individuals.Count)];

            var tournament = individuals
                .OrderBy(x => rng.Next())
                .Take(size)
                .OrderByDescending(x => x.CompositeFitness)
                .First();

            return tournament;
        }

        private NewDna OptimizeSpecialistDNA(NewDna dna, string archetype, Random rng)
        {
            var childDNA = dna.Clone();

            var target = _specialistOptimizer.GetSpecialistTarget(archetype);
            if (target != null)
            {
                ArchetypePurityCalculator.EnforceArchetype(childDNA, archetype, rng, 0.95f);

                if (rng.NextDouble() < 0.6f)
                {
                    childDNA = ForceExplorationCards(childDNA, archetype, rng, 1);
                }
            }

            if (rng.NextDouble() < MUTATION_RATE)
                childDNA.Mutate(rng, _stagnationCounter);

            return childDNA;
        }

        private NewDna ForceExplorationCards(NewDna dna, string archetype, Random rng, int count)
        {
            for (int i = 0; i < 5; i++)
            {
                if (rng.NextDouble() < 0.3f)
                {
                    int geneIndex = rng.Next(NewDna.TOTAL_GENES);
                    dna.Genes[geneIndex] = Math.Clamp(dna.Genes[geneIndex] + (float)(rng.NextDouble() * 2 - 1), 0, 10);
                }
            }

            return dna;
        }

        private void AdjustDNAForHybrid(NewDna dna, HybridDiscoveryEngine.HybridPattern pattern, Random rng)
        {
            var primaryGenes = GetKeyArchetypeGenes(pattern.PrimaryArchetype);
            var secondaryGenes = GetKeyArchetypeGenes(pattern.SecondaryArchetype);

            float primaryBoost = pattern.Ratio / (1 + pattern.Ratio);
            float secondaryBoost = 1 - primaryBoost;

            foreach (var geneIndex in primaryGenes)
            {
                dna.Genes[geneIndex] = Math.Min(10, dna.Genes[geneIndex] * (1 + primaryBoost * 0.3f));
            }

            foreach (var geneIndex in secondaryGenes)
            {
                dna.Genes[geneIndex] = Math.Min(10, dna.Genes[geneIndex] * (1 + secondaryBoost * 0.3f));
            }
        }

        private void EnhancedCheckStagnation()
        {
            float currentBestFitness = _population.Max(x => x.CompositeFitness);
            float diversity = CalculatePopulationDiversity();

            if (currentBestFitness > _bestFitness * 1.01f)
            {
                _bestFitness = currentBestFitness;
                _stagnationCounter = Math.Max(0, _stagnationCounter - 2);
                CardCache.Clear();
            }
            else
            {
                _stagnationCounter++;
            }

            if (_stagnationCounter > 10)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"⚠️  STAGNATION DETECTED - PERFORMING DIVERSITY BOOST");
                Console.ResetColor();

                PerformDiversityBoost();
                _stagnationCounter = 0;
            }
        }

        private void PerformDiversityBoost()
        {
            int replaceCount = POPULATION_SIZE * 2 / 5;
            var rng = _threadRng.Value;

            var keepers = _population
                .OrderByDescending(x => x.CompositeFitness)
                .Take(POPULATION_SIZE / 5)
                .ToList();

            _population.Clear();
            _population.AddRange(keepers);
            _individualDeckCache.Clear();
            _individualFitnessCache.Clear();

            for (int i = 0; i < replaceCount; i++)
            {
                var underrepresented = _currentArchetypeDistribution
                    .Where(kv => kv.Value < 0.15f)
                    .Select(kv => kv.Key)
                    .ToList();

                string archetype;
                if (underrepresented.Count > 0 && rng.NextDouble() < 0.8f)
                {
                    archetype = underrepresented[rng.Next(underrepresented.Count)];
                }
                else
                {
                    var allArchetypes = _archetypePool.GetAllArchetypes();
                    archetype = allArchetypes[rng.Next(allArchetypes.Count)].Name;
                }

                var dna = NewDna.CreateArchetypeDNA(new BotArchetype(archetype), rng);

                for (int j = 0; j < NewDna.TOTAL_GENES; j++)
                {
                    if (rng.NextDouble() < 0.6f)
                    {
                        dna.Genes[j] = (float)rng.NextDouble() * 10;
                    }
                }

                _population.Add(new EvolvableIndividual(dna, _generation));
            }

            while (_population.Count < POPULATION_SIZE)
            {
                var archetypes = _archetypePool.GetAllArchetypes();
                var parent1 = NewDna.CreateArchetypeDNA(archetypes[rng.Next(archetypes.Count)], rng);
                var parent2 = NewDna.CreateArchetypeDNA(archetypes[rng.Next(archetypes.Count)], rng);

                var childDNA = NewDna.Crossover(parent1, parent2, rng);

                for (int j = 0; j < NewDna.TOTAL_GENES; j++)
                {
                    if (rng.NextDouble() < 0.7f)
                    {
                        childDNA.Genes[j] = Math.Clamp(childDNA.Genes[j] + (float)(rng.NextDouble() * 4 - 2), 0, 10);
                    }
                }

                _population.Add(new EvolvableIndividual(childDNA, _generation));
            }
        }

        private void SaveBestSpecialists()
        {
            var byArchetype = _population
                .GroupBy(x => x.Dna.ClassifyArchetype())
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.CompositeFitness).First());

            try
            {
                var specialistsData = new List<object>();

                foreach (var kvp in byArchetype)
                {
                    var specialist = kvp.Value;
                    var rng = new Random();
                    var deck = GetCachedDeck(specialist);

                    var cardGroups = deck
                        .GroupBy(cardId => cardId)
                        .Select(g => new
                        {
                            CardId = g.Key,
                            CardName = CardCache.GetCardName(g.Key),
                            Cost = CardCache.GetCardCost(g.Key),
                            Count = g.Count(),
                            CardData = CardCache.GetCardData(g.Key)
                        })
                        .OrderBy(c => c.Cost)
                        .ThenBy(c => c.CardName)
                        .ToList();

                    float purity = ArchetypePurityCalculator.CalculatePurity(specialist.Dna, kvp.Key);

                    specialistsData.Add(new
                    {
                        Archetype = kvp.Key,
                        IndividualId = specialist.Id,
                        Purity = purity,
                        CompositeFitness = specialist.CompositeFitness,
                        BaseFitness = specialist.BaseFitness,
                        Deck = deck,
                        DeckAnalysis = cardGroups,
                        ManaCurve = CalculateAverageDeckCost(deck),
                        CardCount = cardGroups.Count
                    });
                }

                string json = JsonSerializer.Serialize(specialistsData, new JsonSerializerOptions { WriteIndented = true });
                Directory.CreateDirectory("EvolutionData/Specialists");
                File.WriteAllText($"EvolutionData/Specialists/gen_{_generation:D4}_best_specialists.json", json);

                Console.WriteLine($"\n💾 Saved best specialists for generation {_generation}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to save specialists: {ex.Message}");
            }
        }

        private void CheckForAutoResume()
        {
            if (File.Exists("EvolutionData/latest_population.json"))
            {
                try
                {
                    string json = File.ReadAllText("EvolutionData/latest_population.json");
                    var data = JsonSerializer.Deserialize<PopulationData>(json);

                    if (data != null)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"\n💾 Auto-resume available: Generation {data.Generation}");
                        Console.WriteLine($"   Population: {data.Population?.Count ?? 0} bots");
                        Console.WriteLine($"   Last save: {data.Timestamp:yyyy-MM-dd HH:mm}");
                        Console.ResetColor();
                    }
                }
                catch { }
            }
        }

        private void ProcessDataManagement()
        {
            try
            {
                Console.WriteLine("Checking for existing data...");

                bool hasExistingPopulation = File.Exists("EvolutionData/latest_population.json");
                bool hasHallOfFame = File.Exists("EvolutionData/archetype_hall_of_fame.json");

                if (!hasExistingPopulation && !hasHallOfFame)
                {
                    Console.WriteLine("No existing data found. Starting fresh.");
                    EnsureDirectories();
                    return;
                }

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("\n⚠️  EXISTING DATA DETECTED!");
                Console.WriteLine($"   Population data: {(hasExistingPopulation ? "FOUND" : "NOT FOUND")}");
                Console.WriteLine($"   Hall of Fame: {(hasHallOfFame ? "FOUND" : "NOT FOUND")}");
                Console.ResetColor();

                Console.WriteLine("\nWhat would you like to do?");
                Console.WriteLine("1. Continue with existing data (DEFAULT)");
                Console.WriteLine("2. Delete ALL data and start fresh");
                Console.WriteLine("3. Delete only analytics/performance logs (keep populations)");
                Console.Write("\nYour choice (1-3, Enter for 1): ");

                string input = Console.ReadLine()?.Trim() ?? "1";

                switch (input)
                {
                    case "2":
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("\n🗑️  DELETING ALL DATA...");
                        Console.ResetColor();
                        DeleteAllData();
                        break;

                    case "3":
                        Console.WriteLine("\n🧹 Cleaning up analytics logs only...");
                        CleanupAnalyticsOnly();
                        break;

                    default:
                        Console.WriteLine("\n✓ Continuing with existing data...");
                        EnsureDirectories();
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during data check: {ex.Message}");
                EnsureDirectories();
            }
        }

        private bool HandleResumeDecision()
        {
            if (File.Exists("EvolutionData/latest_population.json"))
            {
                Console.WriteLine("\n📂 Saved population detected!");
                Console.WriteLine("   [Enter] Auto-resume from save");
                Console.WriteLine("   [N] Start fresh (keep Hall of Fame)");
                Console.WriteLine("   [D] Delete everything and start fresh");
                Console.Write("\nChoice (Enter/N/D): ");

                var key = Console.ReadKey(true);

                if (key.Key == ConsoleKey.Enter)
                {
                    Console.WriteLine("\n✓ Resuming from saved state...");
                    return true;
                }
                else if (key.Key == ConsoleKey.D)
                {
                    Console.WriteLine("\n🗑️  Deleting all data...");
                    DeleteAllData();
                    return false;
                }
                else
                {
                    Console.WriteLine("\n✓ Starting fresh (Hall of Fame preserved)...");
                    return false;
                }
            }
            else
            {
                Console.WriteLine("\nNo saved data found. Starting fresh.");
                return false;
            }
        }

        private void DeleteAllData()
        {
            try
            {
                BackupExistingData();

                string[] filesToDelete =
                {
                    "EvolutionData/latest_population.json",
                    "EvolutionData/hall_of_fame.json",
                    "EvolutionData/archetype_hall_of_fame.json"
                };

                string[] directoriesToDelete =
                {
                    "EvolutionData/Analytics",
                    "EvolutionData/Checkpoints",
                    "EvolutionData/BestDecks",
                    "EvolutionData/CSV",
                    "EvolutionData/Reports",
                    "EvolutionData/CardStats",
                    "EvolutionData/Performance"
                };

                foreach (var file in filesToDelete)
                {
                    try
                    {
                        if (File.Exists(file))
                        {
                            File.Delete(file);
                            Console.WriteLine($"  Deleted: {file}");
                        }
                    }
                    catch { }
                }

                foreach (var dir in directoriesToDelete)
                {
                    try
                    {
                        if (Directory.Exists(dir))
                        {
                            Directory.Delete(dir, true);
                            Console.WriteLine($"  Deleted: {dir}");
                        }
                    }
                    catch { }
                }

                EnsureDirectories();
                Console.WriteLine("✓ All data deleted successfully.\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to delete data: {ex.Message}");
            }
        }

        private void CleanupAnalyticsOnly()
        {
            try
            {
                string[] analyticsDirs =
                {
                    "EvolutionData/Analytics",
                    "EvolutionData/Performance",
                    "EvolutionData/Reports",
                    "EvolutionData/CSV"
                };

                foreach (var dir in analyticsDirs)
                {
                    try
                    {
                        if (Directory.Exists(dir))
                        {
                            Directory.Delete(dir, true);
                            Console.WriteLine($"  Cleared: {dir}");
                        }
                    }
                    catch { }
                }

                EnsureDirectories();
                Console.WriteLine("✓ Analytics cleared.\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to clean analytics: {ex.Message}");
            }
        }

        private void BackupExistingData()
        {
            try
            {
                if (!Directory.Exists("EvolutionData")) return;

                string backupDir = $"EvolutionData/Backups/Backup_{DateTime.Now:yyyyMMdd_HHmmss}";
                Directory.CreateDirectory(backupDir);

                string[] importantFiles =
                {
                    "latest_population.json",
                    "archetype_hall_of_fame.json"
                };

                foreach (var file in importantFiles)
                {
                    string source = $"EvolutionData/{file}";
                    if (File.Exists(source))
                    {
                        File.Copy(source, $"{backupDir}/{file}", true);
                    }
                }

                Console.WriteLine($"📦 Backup created: {backupDir}");
            }
            catch { }
        }

        private void EnsureDirectories()
        {
            Directory.CreateDirectory("EvolutionData");
            Directory.CreateDirectory("EvolutionData/Analytics");
            Directory.CreateDirectory("EvolutionData/Checkpoints");
            Directory.CreateDirectory("EvolutionData/BestDecks");
            Directory.CreateDirectory("EvolutionData/CSV");
            Directory.CreateDirectory("EvolutionData/Reports");
            Directory.CreateDirectory("EvolutionData/CardStats");
            Directory.CreateDirectory("EvolutionData/Performance");
            Directory.CreateDirectory("EvolutionData/Backups");
        }

        private void PrintHardwareInfo()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n=== HARDWARE CONFIGURATION ===");
            Console.WriteLine($"Logical Processors: {Environment.ProcessorCount}");
            Console.WriteLine($"Configured Threads: {OPTIMAL_PARALLELISM} (1.5x for I/O bound)");
            Console.WriteLine($"Population Size: {POPULATION_SIZE}");
            Console.WriteLine($"Matches per Bot: {OPTIMAL_MATCHES} random + 2 archetype = 8 total");
            Console.WriteLine($"Max Moves per Game: {OPTIMAL_MAX_MOVES}");
            Console.WriteLine($"Mutation Rate: {MUTATION_RATE:P0}");
            Console.WriteLine($"Anti-Stagnation: ACTIVE");
            Console.ResetColor();
        }

        private ParallelOptions GetTurboParallelOptions()
        {
            return new ParallelOptions
            {
                MaxDegreeOfParallelism = OPTIMAL_PARALLELISM
            };
        }

        private void LogPerformanceMetrics()
        {
            double secondsPerGen = _generationTimer.Elapsed.TotalSeconds;
            double gamesPerSecond = _totalGamesLastGen / _generationTimer.Elapsed.TotalSeconds;

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"⏱️  Performance: {secondsPerGen:F1}s/gen | {gamesPerSecond:F1} games/s");
            Console.ResetColor();

            if (_generation % 5 == 0)
            {
                LogCardPopularity();
            }

            try
            {
                string logFile = $"EvolutionData/Performance/perf_log.csv";
                bool fileExists = File.Exists(logFile);

                using (var writer = new StreamWriter(logFile, true))
                {
                    if (!fileExists)
                        writer.WriteLine("Generation,SecondsPerGen,GamesPerSecond,Timestamp");

                    writer.WriteLine($"{_generation},{secondsPerGen:F2},{gamesPerSecond:F2},{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                }
            }
            catch { }
        }

        private bool LoadLatestPopulation()
        {
            try
            {
                string filePath = "EvolutionData/latest_population.json";
                if (!File.Exists(filePath))
                {
                    Console.WriteLine("No population file found.");
                    return false;
                }

                Console.WriteLine($"Loading population from: {filePath}");

                string backupPath = $"EvolutionData/Backups/corrupted_population_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                File.Copy(filePath, backupPath, true);
                Console.WriteLine($"Backup created: {backupPath}");

                string json = File.ReadAllText(filePath);

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true,
                    Converters = { new EvolvableIndividualConverter() },
                    MaxDepth = 256
                };

                var data = JsonSerializer.Deserialize<PopulationData>(json, options);

                if (data == null)
                {
                    Console.WriteLine("Failed to deserialize population data.");
                    return false;
                }

                if (data.Population == null || data.Population.Count == 0)
                {
                    Console.WriteLine("Population is empty.");
                    return false;
                }

                int originalCount = data.Population.Count;
                data.Population = data.Population
                    .Where(ind => ind != null)
                    .Where(ind => ind.Dna != null)
                    .Where(ind => ind.Dna.GetGenesArray().Length == NewDna.TOTAL_GENES)
                    .ToList();

                Console.WriteLine($"Filtered: {originalCount} -> {data.Population.Count} valid individuals");

                if (data.Population.Count == 0)
                {
                    Console.WriteLine("No valid individuals after filtering.");
                    return false;
                }

                _population = data.Population;
                _generation = data.Generation;
                _bestFitness = data.BestFitness;
                _stagnationCounter = 0;

                int maxId = _population.Max(ind => ind.Id);
                EvolvableIndividual.SetNextId(maxId + 1);

                if (_population.Count < POPULATION_SIZE)
                {
                    int needed = POPULATION_SIZE - _population.Count;
                    Console.WriteLine($"Augmenting population by {needed} individuals...");
                    AugmentPopulationToSize(POPULATION_SIZE);
                }

                Console.WriteLine($"✓ Successfully loaded {_population.Count} individuals from generation {_generation}");
                return true;
            }
            catch (JsonException jsonEx)
            {
                Console.WriteLine($"JSON error: {jsonEx.Message}");
                Console.WriteLine($"Line: {jsonEx.LineNumber}, Position: {jsonEx.BytePositionInLine}");

                TryRepairCorruptedJson();
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected error: {ex.Message}");
                return false;
            }
        }

        private void TryRepairCorruptedJson()
        {
            try
            {
                string filePath = "EvolutionData/latest_population.json";
                if (!File.Exists(filePath)) return;

                string json = File.ReadAllText(filePath);

                json = json.Replace(",}", "}").Replace(",]", "]");

                var lines = json.Split('\n').ToList();
                for (int i = 0; i < lines.Count; i++)
                {
                    if (lines[i].Contains(": {") && !lines[i].Contains("\":"))
                    {
                        int colonIndex = lines[i].IndexOf(':');
                        if (colonIndex > 0)
                        {
                            string beforeColon = lines[i].Substring(0, colonIndex).Trim();
                            if (!beforeColon.StartsWith("\"") || !beforeColon.EndsWith("\""))
                            {
                                lines[i] = $"\"{beforeColon.Trim('"')}\": {lines[i].Substring(colonIndex + 1)}";
                            }
                        }
                    }
                }

                json = string.Join("\n", lines);
                File.WriteAllText(filePath, json);
                Console.WriteLine("Attempted to repair JSON file. Try loading again.");
            }
            catch { }
        }

        private void AugmentPopulationToSize(int targetSize)
        {
            var rng = _threadRng.Value;

            while (_population.Count < targetSize)
            {
                if (_population.Count > 0)
                {
                    var parent1 = _population[rng.Next(_population.Count)];
                    var parent2 = _population[rng.Next(_population.Count)];
                    var childDna = NewDna.Crossover(parent1.Dna, parent2.Dna, rng);

                    if (rng.NextDouble() < 0.4f)
                        childDna.Mutate(rng, _stagnationCounter);

                    _population.Add(new EvolvableIndividual(childDna, _generation));
                }
                else
                {
                    var dna = NewDna.CreateRandom(rng);
                    _population.Add(new EvolvableIndividual(dna, _generation));
                }
            }
        }

        private void SaveLatestPopulation()
        {
            try
            {
                var data = new PopulationData
                {
                    Generation = _generation,
                    Population = _population,
                    BestFitness = _bestFitness,
                    Timestamp = DateTime.Now
                };

                string json = JsonSerializer.Serialize(data,
                    new JsonSerializerOptions { WriteIndented = true });

                Directory.CreateDirectory("EvolutionData");
                File.WriteAllText("EvolutionData/latest_population.json", json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to save population: {ex.Message}");
            }
        }

        private void UpdateCardStatistics()
        {
            // Resetuj statystyki dla WSZYSTKICH kolekcjonowalnych ID (czyści duchy)
            foreach (var cardId in _collectibleCardIds)
            {
                var stat = _cardStats.GetOrAdd(cardId, id => new CardStatsEnhanced(id, CardCache.GetCardName(id)));
                stat.ResetUsageStats();
                stat.TotalDecks = _population.Count;
            }

            var deckCardPresence = new ConcurrentDictionary<int, int>();
            var cardCopyCounts = new ConcurrentDictionary<int, int>();

            foreach (var individual in _population)
            {
                var deck = GetCachedDeck(individual);
                var uniqueCards = deck.Distinct();
                foreach (var cardId in uniqueCards)
                    deckCardPresence.AddOrUpdate(cardId, 1, (k, v) => v + 1);
                foreach (var cardId in deck)
                    cardCopyCounts.AddOrUpdate(cardId, 1, (k, v) => v + 1);
            }

            // Przelicz statystyki dla KAŻDEJ karty z gry
            foreach (var cardId in _collectibleCardIds)
            {
                deckCardPresence.TryGetValue(cardId, out int deckCount);
                cardCopyCounts.TryGetValue(cardId, out int totalCopies);

                if (_cardStats.TryGetValue(cardId, out var stat))
                    stat.UpdateUsageStats(deckCount, totalCopies, _population.Count);
            }
        }
        private void DebugCardStats()
        {
            if (_generation % 5 == 0)  // Check every 5 generations
            {
                Console.WriteLine("\n🔍 DEBUG - CARD STATS VERIFICATION:");

                var topCards = _cardStats.Values
                    .Where(s => s.DeckCount > 0)
                    .OrderByDescending(s => s.UseRate)
                    .Take(5)
                    .ToList();

                foreach (var card in topCards)
                {
                    Console.WriteLine($"  {card.CardName}:");
                    Console.WriteLine($"    UseRate: {card.UseRate:P1}, WinRate: {card.WinRate:P1}");
                    Console.WriteLine($"    Games: {card.GamesWithCard}, Wins: {card.WinsWithCard}");
                    Console.WriteLine($"    DeckCount: {card.DeckCount}/{_population.Count}");
                }

                // Check for any cards with games but no wins recorded
                var suspicious = _cardStats.Values
                    .Where(s => s.GamesWithCard > 10 && s.WinRate == 0)
                    .ToList();

                if (suspicious.Count > 0)
                {
                    Console.WriteLine($"⚠️  WARNING: {suspicious.Count} cards have games but 0% win rate!");
                }
            }
        }

        private void UpdateWinRateStats(EvolvableIndividual winner, EvolvableIndividual loser, GameResult result)
        {
            if (winner == null || loser == null) return;

            try
            {
                var winnerDeck = GetCachedDeck(winner);
                var loserDeck = GetCachedDeck(loser);

                EnsureCardsInStats(winnerDeck.Concat(loserDeck).Distinct());

                // Increment games for winner's cards as WINS
                foreach (var cardId in winnerDeck.Distinct())
                {
                    if (_cardStats.TryGetValue(cardId, out var stat))
                    {
                        stat.IncrementGames(true);  // true = won
                    }
                }

                // Increment games for loser's cards as LOSSES
                foreach (var cardId in loserDeck.Distinct())
                {
                    if (_cardStats.TryGetValue(cardId, out var stat))
                    {
                        stat.IncrementGames(false); // false = lost
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating win rate stats: {ex.Message}");
            }
        }

        private void UpdateCrossArchetypeStats(EvolvableIndividual player, EvolvableIndividual opponent, bool playerWon)
        {
            string playerArchetype = player.Dna.ClassifyArchetype();
            string opponentArchetype = opponent.Dna.ClassifyArchetype();

            var stats = _crossArchetypeStats.GetOrAdd(playerArchetype,
                _ => new CrossArchetypeStats());

            stats.GamesAgainst.AddOrUpdate(opponentArchetype,
                1,
                (key, oldValue) => oldValue + 1);

            if (playerWon)
            {
                stats.WinsAgainst.AddOrUpdate(opponentArchetype,
                    1,
                    (key, oldValue) => oldValue + 1);
            }

            int games = stats.GamesAgainst.GetOrAdd(opponentArchetype, 0);
            int wins = stats.WinsAgainst.GetOrAdd(opponentArchetype, 0);

            stats.WinRates.AddOrUpdate(opponentArchetype,
                games > 0 ? (float)wins / games : 0f,
                (key, oldValue) => games > 0 ? (float)wins / games : 0f);
        }

        private void EnsureCardsInStats(IEnumerable<int> cardIds)
        {
            foreach (var cardId in cardIds)
            {
                // Use GetOrAdd with factory method
                _cardStats.GetOrAdd(cardId, id =>
                {
                    // Create new stats with constructor
                    var newStat = new CardStatsEnhanced(id, CardCache.GetCardName(id))
                    {
                        TotalDecks = _population.Count
                    };
                    return newStat;
                });
            }
        }

        private void PerformSpeciation()
        {
            _species.Clear();

            if (_population.Count == 0) return;

            var firstSpecies = new Species(_population[0], SPECIATION_THRESHOLD);
            _species.Add(firstSpecies);
            _population[0].SpeciesId = firstSpecies.Id;

            foreach (var individual in _population.Skip(1))
            {
                bool placed = false;

                foreach (var species in _species)
                {
                    if (species.IsCompatible(individual.Dna))
                    {
                        species.AddMember(individual);
                        individual.SpeciesId = species.Id;
                        placed = true;
                        break;
                    }
                }

                if (!placed)
                {
                    var newSpecies = new Species(individual, SPECIATION_THRESHOLD);
                    _species.Add(newSpecies);
                    individual.SpeciesId = newSpecies.Id;
                }
            }

            _species = _species.Where(s => s.Members.Count >= 3).ToList();

            var unassigned = _population.Where(i => !_species.Any(s => s.Id == i.SpeciesId)).ToList();
            foreach (var individual in unassigned)
            {
                Species closestSpecies = null;
                float minDistance = float.MaxValue;

                foreach (var species in _species)
                {
                    float distance = NewDna.CalculateDistance(individual.Dna, species.Centroid);
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        closestSpecies = species;
                    }
                }

                if (closestSpecies != null)
                {
                    closestSpecies.AddMember(individual);
                    individual.SpeciesId = closestSpecies.Id;
                }
            }
        }

        private void EvaluatePopulation()
        {
            _crossArchetypeStats.Clear();
            _totalGamesLastGen = 0;
            _individualFitnessCache.Clear();

            UpdateCardStatistics();
            GlobalStats = _cardStats;

            var parallelOptions = GetTurboParallelOptions();

            Parallel.ForEach(_population, parallelOptions, individual =>
            {
                individual.ResetFitness();
                individual.Behaviors.Clear();
                individual.CompositeFitness = CalculateEnhancedCompositeFitness(individual);
            });

            var opponentPool = CreateOpponentPool();

            Parallel.ForEach(_population, parallelOptions, individual =>
            {
                var localRng = _threadRng.Value;
                float totalScore = 0;
                int gamesPlayed = 0;

                foreach (var opponent in opponentPool.Take(OPTIMAL_MATCHES))
                {
                    if (opponent == individual) continue;

                    var result = PlayMatch(individual, opponent, localRng);
                    totalScore += result.score;
                    gamesPlayed++;

                    individual.RecordBehavior(result);

                    var individualDeck = GetCachedDeck(individual);
                    var opponentDeck = GetCachedDeck(opponent);

                    if (result.score > 0)
                    {
                        _synergyLearner.LearnFromGame(individual, opponent, individualDeck, opponentDeck);
                        UpdateWinRateStats(individual, opponent, result.result);
                        UpdateCrossArchetypeStats(individual, opponent, true);
                    }
                    else if (result.score < 0)
                    {
                        _synergyLearner.LearnFromGame(opponent, individual, opponentDeck, individualDeck);
                        UpdateWinRateStats(opponent, individual, result.result);
                        UpdateCrossArchetypeStats(individual, opponent, false);
                    }
                }
                individual.BaseFitness = totalScore / Math.Max(1, gamesPlayed);
                individual.CompositeFitness = CalculateEnhancedCompositeFitness(individual);

                Interlocked.Add(ref _totalGamesLastGen, gamesPlayed);
            });

            _metagameTracker.UpdateAfterGeneration(_population);
            _currentArchetypeDistribution = _metagameTracker.GetArchetypePrevalence();

            foreach (var individual in _population)
            {
                var deck = GetCachedDeck(individual);
                float novelty = _diversityEnforcer.CalculateDeckNovelty(deck);
                individual.DeckNoveltyScore = novelty;
                _diversityEnforcer.AddDeck(deck);
            }
        }

        private int[] GetCachedDeck(EvolvableIndividual individual)
        {
            int cacheKey = individual.Id;
            if (!_individualDeckCache.TryGetValue(cacheKey, out var deck))
            {
                deck = individual.Dna.BuildDeck(_collectibleCardIds, _threadRng.Value);
                _individualDeckCache[cacheKey] = deck;
            }
            return deck;
        }

        private float CalculateDeckSynergyBonus(EvolvableIndividual individual)
        {
            var rng = _threadRng.Value;
            var deck = GetCachedDeck(individual);

            float bonus = 0;

            var cardList = deck.Select(id => CardCache.GetCard(id)).Where(c => c != null).ToList();

            for (int i = 0; i < cardList.Count; i++)
            {
                for (int j = i + 1; j < cardList.Count; j++)
                {
                    float pairSynergy = CalculateCardPairSynergy(cardList[i], cardList[j]);
                    bonus += pairSynergy;
                }
            }

            return bonus * 0.1f;
        }

        private float CalculateCardPairSynergy(CardData card1, CardData card2)
        {
            float synergy = 0;

            bool DoesCardMark(CardData card)
            {
                return card.Effects?.Any(e =>
                    e.Actions?.Any(a => a.StatusKeyword == Keyword.Marked) == true) == true;
            }

            bool DoesCardBenefitFromMark(CardData card)
            {
                return card.Effects?.Any(e =>
                    e.Trigger == TriggerType.OnStatusApplied &&
                    e.Condition?.SubConditions?.Any(sc =>
                        sc.Condition == ConditionType.IsStatus && sc.TargetParam == "Marked") == true) == true;
            }

            bool DoesCardSacrifice(CardData card)
            {
                return card.Effects?.Any(e =>
                    e.Actions?.Any(a => a.Type == ActionType.SacrificeUnit) == true) == true;
            }

            bool DoesCardBenefitFromSacrifice(CardData card)
            {
                return card.Effects?.Any(e =>
                    e.Trigger == TriggerType.OnSacrificed ||
                    e.Trigger == TriggerType.OnOtherUnitSacrificed) == true;
            }

            bool DoesCardDraw(CardData card)
            {
                return card.Effects?.Any(e =>
                    e.Actions?.Any(a => a.Type == ActionType.DrawCard) == true) == true;
            }

            bool DoesCardBenefitFromDraw(CardData card)
            {
                return card.Effects?.Any(e =>
                    e.Trigger == TriggerType.OnFriendlyCardDrawn ||
                    (e.Zone == EffectZone.Hand && e.Trigger == TriggerType.OnFriendlyActionPlayed)) == true;
            }

            bool card1Marks = DoesCardMark(card1);
            bool card2Marks = DoesCardMark(card2);
            bool card1BenefitsFromMark = DoesCardBenefitFromMark(card1);
            bool card2BenefitsFromMark = DoesCardBenefitFromMark(card2);

            bool card1Sacrifices = DoesCardSacrifice(card1);
            bool card2Sacrifices = DoesCardSacrifice(card2);
            bool card1BenefitsFromSacrifice = DoesCardBenefitFromSacrifice(card1);
            bool card2BenefitsFromSacrifice = DoesCardBenefitFromSacrifice(card2);

            bool card1Draws = DoesCardDraw(card1);
            bool card2Draws = DoesCardDraw(card2);
            bool card1BenefitsFromDraw = DoesCardBenefitFromDraw(card1);
            bool card2BenefitsFromDraw = DoesCardBenefitFromDraw(card2);

            if ((card1Marks && card2BenefitsFromMark) || (card2Marks && card1BenefitsFromMark))
                synergy += 50.0f;

            if ((card1Sacrifices && card2BenefitsFromSacrifice) || (card2Sacrifices && card1BenefitsFromSacrifice))
                synergy += 60.0f;

            if ((card1Draws && card2BenefitsFromDraw) || (card2Draws && card1BenefitsFromDraw))
                synergy += 40.0f;

            var sharedSubtypes = (card1.Subtypes ?? new List<string>())
                .Intersect(card2.Subtypes ?? new List<string>())
                .Count();

            synergy += sharedSubtypes * 15.0f;

            return synergy;
        }

        private float CalculateDeckConsistencyBonus(EvolvableIndividual individual)
        {
            var rng = _threadRng.Value;
            var deck = GetCachedDeck(individual);

            float bonus = 0;
            string archetype = individual.Dna.ClassifyArchetype();

            int archetypeCards = 0;
            foreach (var cardId in deck)
            {
                try
                {
                    var card = CardCache.GetCard(cardId);

                    bool fitsArchetype = archetype switch
                    {
                        "Aggro" => IsAggroCard(card),
                        "Control" => IsControlCard(card),
                        "Combo" => IsComboCard(card),
                        "Midrange" => IsMidrangeCard(card),
                        "Sacrifice" => IsSacrificeCard(card),
                        _ => false
                    };

                    if (fitsArchetype) archetypeCards++;
                }
                catch { }
            }

            float consistencyRatio = (float)archetypeCards / deck.Length;
            if (consistencyRatio >= 0.6f)
                bonus += individual.BaseFitness * consistencyRatio * 0.1f;

            return bonus;
        }

        private bool IsAggroCard(CardData card)
        {
            return (card.Type == CardType.Unit && card.Cost <= 3 && card.Attack >= 2) ||
                   (card.Type == CardType.Spell && card.Effects?.Any(e =>
                       e.Actions?.Any(a => a.Type == ActionType.DealDamage &&
                                          a.Target == TargetType.EnemyHero) == true) == true) ||
                   (card.Keywords?.Contains(Keyword.Flying) == true) ||
                   (card.Effects?.Any(e => e.Actions?.Any(a => a.Type == ActionType.BonusAttack) == true) == true);
        }

        private bool IsControlCard(CardData card)
        {
            return (card.Type == CardType.Spell && card.Effects?.Any(e =>
                       e.Actions?.Any(a => a.Type == ActionType.DestroyUnit ||
                                          a.Type == ActionType.Silence ||
                                          a.Type == ActionType.ReturnToHand) == true) == true) ||
                   (card.Type == CardType.Unit && card.Health >= 4) ||
                   (card.Effects?.Any(e => e.Actions?.Any(a => a.Type == ActionType.DrawCard) == true) == true);
        }

        private bool IsComboCard(CardData card)
        {
            return (card.Effects?.Any(e => e.Actions?.Any(a => a.Type == ActionType.TutorCard) == true) == true) ||
                   (card.Effects?.Any(e => e.Trigger == TriggerType.OnStatusApplied) == true) ||
                   (card.Effects?.Any(e => e.Actions?.Count > 1) == true);
        }

        private bool IsMidrangeCard(CardData card)
        {
            if (card.Type == CardType.Unit)
            {
                float statEfficiency = (card.Attack + card.Health) / (float)Math.Max(1, card.Cost);
                return statEfficiency >= 1.5f && card.Cost >= 2 && card.Cost <= 5;
            }
            return false;
        }

        private bool IsSacrificeCard(CardData card)
        {
            return (card.Effects?.Any(e => e.Trigger == TriggerType.OnSacrificed ||
                                          e.Trigger == TriggerType.OnOtherUnitSacrificed ||
                                          e.Trigger == TriggerType.OnFriendlyUnitDied ||
                                          e.Trigger == TriggerType.OnDeath) == true) ||
                   (card.Effects?.Any(e => e.Actions?.Any(a => a.Type == ActionType.SacrificeUnit) == true) == true) ||
                   (card.Subtypes?.Contains("Token") == true);
        }

        private float CalculateAverageDeckCost(int[] deck)
        {
            float totalCost = 0;
            foreach (var cardId in deck)
            {
                try
                {
                    totalCost += CardCache.GetCardCost(cardId);
                }
                catch { }
            }
            return deck.Length > 0 ? totalCost / deck.Length : 0;
        }

        private int CountControlCards(int[] deck)
        {
            int count = 0;
            foreach (var cardId in deck)
            {
                try
                {
                    var card = CardCache.GetCard(cardId);
                    if (card.Effects == null) continue;

                    bool isControl = card.Effects.Any(e => e.Actions != null && e.Actions.Any(a =>
                        a.Type == ActionType.DestroyUnit ||
                        a.Type == ActionType.DealDamage ||
                        a.Type == ActionType.Silence ||
                        a.Type == ActionType.ReturnToHand ||
                        (a.Type == ActionType.ApplyStatus &&
                            (a.StatusKeyword == Keyword.Stunned || a.StatusKeyword == Keyword.Marked))
                    ));

                    if (isControl) count++;
                }
                catch { }
            }
            return count;
        }

        private int CountComboPairs(int[] deck)
        {
            var costGroups = deck.GroupBy(cardId =>
            {
                try
                {
                    return CardCache.GetCardCost(cardId);
                }
                catch { return -1; }
            }).Where(g => g.Key >= 0 && g.Count() >= 2);

            return costGroups.Count();
        }

        private List<EvolvableIndividual> CreateOpponentPool()
        {
            var pool = new List<EvolvableIndividual>();
            var rng = _threadRng.Value;

            var hallOfFameChampions = _hallOfFame.GetAllChampions();
            pool.AddRange(hallOfFameChampions);

            foreach (var species in _species)
            {
                if (species.Members.Count > 0)
                {
                    pool.Add(species.GetRepresentative());
                }
            }

            int randomSamples = Math.Min(8, _population.Count / 4);
            var shuffled = _population.OrderBy(x => rng.Next()).Take(randomSamples);
            pool.AddRange(shuffled);

            var underrepresentedSpecialists = _hallOfFame.GetSpecialistsForUnderrepresentedArchetypes(_currentArchetypeDistribution);
            pool.AddRange(underrepresentedSpecialists);

            foreach (var archetype in _archetypePool.GetAllArchetypes())
            {
                var dna = NewDna.CreateArchetypeDNA(archetype, rng);
                pool.Add(new EvolvableIndividual(dna, _generation));
            }

            return pool.Distinct().ToList();
        }

        private void UpdateHallOfFame()
        {
            float avgFitness = _population.Average(ind => ind.BaseFitness);

            var versatilityScores = new ConcurrentDictionary<int, float>();
            Parallel.ForEach(_population, individual =>
            {
                versatilityScores[individual.Id] = CalculateVersatilityScore(individual);
            });

            var byArchetype = _population
                .GroupBy(x => x.Dna.ClassifyArchetype())
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var (archetype, group) in byArchetype)
            {
                if (group.Count == 0) continue;

                var bestInArchetype = group.OrderByDescending(x => x.BaseFitness).First();
                versatilityScores.TryGetValue(bestInArchetype.Id, out float versatility);
                _hallOfFame.AddCandidate(bestInArchetype, _generation, avgFitness, versatility);
            }

            var top5 = _population.OrderByDescending(x => x.BaseFitness).Take(5).ToList();
            foreach (var ind in top5)
            {
                versatilityScores.TryGetValue(ind.Id, out float versatility);
                _hallOfFame.AddCandidate(ind, _generation, avgFitness, versatility);
            }
        }

        private float CalculateVersatilityScore(EvolvableIndividual individual)
        {
            string archetype = individual.Dna.ClassifyArchetype();

            if (!_crossArchetypeStats.TryGetValue(archetype, out var stats))
                return 0.5f;

            if (stats.GamesAgainst.IsEmpty)
                return 0.3f;

            var winRates = stats.WinRates.Values.ToList();
            if (winRates.Count == 0) return 0.3f;

            float avgWinRate = winRates.Average();
            float variance = winRates.Sum(w => (w - avgWinRate) * (w - avgWinRate)) / winRates.Count;

            float versatility = Math.Max(0, 1.0f - variance * 4.0f);

            return Math.Clamp(versatility, 0.1f, 0.9f);
        }

        private (float score, GameResult result) PlayMatch(
            EvolvableIndividual player1,
            EvolvableIndividual player2,
            Random rng)
        {
            var seed = rng.Next();
            var deterministicRng = new CardGame.Core.Application.DeterministicRng(seed);
            var factory = new CardGame.Core.Cards.Factories.CardFactory(
                CardLibrary.Instance, deterministicRng);

            var deck1 = GetCachedDeck(player1);
            var deck2 = GetCachedDeck(player2);

            var engine = new GameEngine(
                GameState.Initial(
                    1,
                    deck1.Select(id => factory.CreateCard(id, 1)).ToList(),
                    deck2.Select(id => factory.CreateCard(id, 2)).ToList(),
                    deterministicRng),
                seed);

            var strategy1 = new OptimizedNewEvolvableStrategy(player1.Dna, _statsSnapshot);
            var strategy2 = new OptimizedNewEvolvableStrategy(player2.Dna, _statsSnapshot);

            return SimulateGame(engine, strategy1, strategy2, player1, player2);
        }

        private (float score, GameResult result) SimulateGame(
    GameEngine engine,
    IAIStrategy strategy1,
    IAIStrategy strategy2,
    EvolvableIndividual player1,
    EvolvableIndividual player2)
        {
            // Elita dostaje Depth 5, reszta 4
            float currentBest = _bestFitness;
            bool isElite = player1.CompositeFitness > currentBest * 0.9f || player2.CompositeFitness > currentBest * 0.9f;
            int depth = isElite ? 3 : 3;

            var solver1 = new BotSolver(engine, 1, strategy1, 3, depth);
            var solver2 = new BotSolver(engine, 2, strategy2, 3, depth);

            int maxMoves = OPTIMAL_MAX_MOVES;
            int moves = 0;

            // Mulligan
            while (engine.CurrentState.CurrentPhase == GamePhase.Mulligan && moves++ < 10)
            {
                for (int pid = 1; pid <= 2; pid++)
                {
                    if (!engine.CurrentState.PlayersReady.Contains(pid))
                    {
                        var pState = engine.CurrentState.GetPlayer(pid);
                        var toReplace = pState.Hand.Where(c => c.Definition.BaseStats.BloodCost > 3).Select(c => c.InstanceId).ToList();
                        engine.ExecuteCommand(new ConfirmMulliganCommand(pid, toReplace));
                    }
                }
            }

            while (!engine.CurrentState.IsGameOver() && moves++ < maxMoves)
            {
                var state = engine.CurrentState;
                var solver = state.ActivePlayerId == 1 ? solver1 : solver2;
                var bestMove = solver.FindBestMoves(state).FirstOrDefault();
                if (bestMove?.Command != null) engine.ExecuteCommand(bestMove.Command);
                else engine.ExecuteCommand(new EndPhaseCommand(state.ActivePlayerId));
            }

            var finalState = engine.CurrentState;

            // Obliczamy winnerId tutaj
            int? winnerId = null;
            if (finalState.PlayerB.Health <= 0) winnerId = 1;
            else if (finalState.PlayerA.Health <= 0) winnerId = 2;

            // Przekazujemy winnerId do CalculateMatchScore
            float score = CalculateMatchScore(finalState, true, winnerId);

            var result = new GameResult
            {
                WinnerId = winnerId,
                Player1Health = finalState.PlayerA.Health,
                Player2Health = finalState.PlayerB.Health,
                Turns = moves / 2
            };

            return (score, result);
        }

        private float CalculateMatchScore(GameState state, bool isPlayer1, int? winnerId)
        {
            var player = isPlayer1 ? state.PlayerA : state.PlayerB;
            var opponent = isPlayer1 ? state.PlayerB : state.PlayerA;

            float score = 0;

            // 1. HP difference (scaled down)
            float hpDiff = player.Health - opponent.Health;
            score += hpDiff * 20f;  // Reduced from 50f

            // 2. Damage dealt to hero
            float damageDealt = 20 - opponent.Health;
            score += damageDealt * 50f;  // Reduced from 100f

            // 3. Board Control (scaled down)
            var playerUnits = state.Board.GetAllUnits().Where(u => u.OwnerPlayerId == player.PlayerId).ToList();
            var opponentUnits = state.Board.GetAllUnits().Where(u => u.OwnerPlayerId != player.PlayerId).ToList();

            score += playerUnits.Sum(u => u.CurrentStats.Attack * 5f + u.CurrentStats.Health * 4f);
            score -= opponentUnits.Sum(u => u.CurrentStats.Attack * 3f + u.CurrentStats.Health * 2f);

            // 4. Hand size
            score += player.Hand.Count * 10f;  // Reduced from 30f

            // 5. WIN/LOSS BONUS - THIS IS CRITICAL!
            if (state.IsGameOver() && winnerId.HasValue)
            {
                bool iWon = (isPlayer1 && winnerId == 1) || (!isPlayer1 && winnerId == 2);

                // Make win/loss bonuses MORE significant relative to other factors
                if (iWon)
                {
                    score += 1000f;  // Increased from 3000f but still significant
                }
                else
                {
                    score -= 800f;   // Increased from -2000f
                }
            }

            return score;
        }

        private EvolvableIndividual TournamentSelectWithDiversity(List<EvolvableIndividual> group, string archetype)
        {
            if (group.Count == 0) return null;
            if (group.Count == 1) return group[0];

            var tournament = group
                .OrderBy(x => _threadRng.Value.Next())
                .Take(Math.Min(TOURNAMENT_SIZE * 2, group.Count))
                .ToList();

            var adjustedFitness = tournament.Select(ind =>
            {
                float penalty = 1.0f;

                if (!string.IsNullOrEmpty(archetype) && _currentArchetypeDistribution.ContainsKey(archetype))
                {
                    float prevalence = _currentArchetypeDistribution[archetype];
                    if (prevalence > 0.35f)
                    {
                        penalty = 0.7f;
                    }
                    else if (prevalence < 0.15f)
                    {
                        penalty = 1.3f;
                    }
                }

                return new { Individual = ind, AdjustedFitness = ind.CompositeFitness * penalty };
            }).ToList();

            return adjustedFitness.OrderByDescending(x => x.AdjustedFitness).First().Individual;
        }

        private void CheckStagnation()
        {
            EnhancedCheckStagnation();
        }

        private float CalculatePopulationDiversity()
        {
            if (_population.Count < 2) return 0;

            var rng = _threadRng.Value;
            float totalDistance = 0;
            int samples = Math.Min(50, _population.Count * (_population.Count - 1) / 2);

            for (int i = 0; i < samples; i++)
            {
                var idx1 = rng.Next(_population.Count);
                var idx2 = rng.Next(_population.Count);

                if (idx1 != idx2)
                    totalDistance += NewDna.CalculateDistance(_population[idx1].Dna, _population[idx2].Dna);
            }

            return totalDistance / samples;
        }

        private void LogGenerationStats()
        {
            var best = _population.OrderByDescending(x => x.CompositeFitness).First();
            var avgFitness = _population.Average(x => x.CompositeFitness);
            var avgBaseFitness = _population.Average(x => x.BaseFitness);

            Console.WriteLine($"Best: #{best.Id} ({best.Dna.ClassifyArchetype()}) | " +
                            $"Composite: {best.CompositeFitness:F0} | Base: {best.BaseFitness:F0}");
            Console.WriteLine($"Avg Composite: {avgFitness:F0} | Avg Base: {avgBaseFitness:F0}");
            Console.WriteLine($"Stagnation: {_stagnationCounter} gens | Diversity: {CalculatePopulationDiversity():F3}");

            var archetypeCounts = _population
                .GroupBy(ind => ind.Dna.ClassifyArchetype())
                .Select(g => new { Archetype = g.Key, Count = g.Count(), AvgFitness = g.Average(x => x.CompositeFitness) })
                .OrderByDescending(x => x.Count);

            Console.WriteLine("\n=== ARCHETYPE DISTRIBUTION ===");
            foreach (var arch in archetypeCounts)
            {
                string marker = "";
                float prevalence = (float)arch.Count / _population.Count;

                if (prevalence < 0.15f) marker = "⬆️";
                else if (prevalence > 0.35f) marker = "⬇️";

                Console.WriteLine($"  {arch.Archetype}: {arch.Count} bots ({prevalence:P0}) {marker} | " +
                                $"Avg Fit: {arch.AvgFitness:F0}");
            }
            if (_generation % 5 == 0)
            {
                PrintDetailedStats();
            }
        }

        private void PerformAnalytics()
        {
            CardAnalyticsV2.ProcessAndSave(_population, _generation, _cardStats.ToDictionary(kvp => kvp.Key, kvp => kvp.Value));

            if (_generation % 10 == 0)
            {
                CardAnalyticsV2.ExportToCsv(_population, _generation);
                CardAnalyticsV2.ExportCardStatsToCsv(_cardStats.ToDictionary(kvp => kvp.Key, kvp => kvp.Value), _generation);
                CardAnalyticsV2.GenerateMetaReport(_population, _generation, _cardStats.ToDictionary(kvp => kvp.Key, kvp => kvp.Value));
            }

            GenerateCardAnalyticsReport();
        }

        private void SaveBestDecks()
        {
            var top10 = _population
                .OrderByDescending(x => x.BaseFitness)
                .Take(10)
                .ToList();

            try
            {
                var bestDecksData = new List<object>();
                foreach (var ind in top10)
                {
                    var rng = new Random();
                    var deck = GetCachedDeck(ind);

                    var cardGroups = deck
                        .GroupBy(cardId => cardId)
                        .Select(g => new
                        {
                            CardId = g.Key,
                            CardName = CardCache.GetCardName(g.Key),
                            Cost = CardCache.GetCardCost(g.Key),
                            Count = g.Count(),
                            CardData = CardCache.GetCardData(g.Key)
                        })
                        .OrderBy(c => c.Cost)
                        .ThenBy(c => c.CardName)
                        .ToList();

                    var cardsByCost = cardGroups
                        .GroupBy(c => c.Cost)
                        .OrderBy(g => g.Key)
                        .ToDictionary(g => g.Key, g => g.ToList());

                    bestDecksData.Add(new
                    {
                        IndividualId = ind.Id,
                        BaseFitness = ind.BaseFitness,
                        CompositeFitness = ind.CompositeFitness,
                        Archetype = ind.Dna.ClassifyArchetype(),
                        ArchetypePurity = ArchetypePurityCalculator.CalculatePurity(ind.Dna, ind.Dna.ClassifyArchetype()),
                        Deck = deck,
                        DeckByCost = cardsByCost,
                        DeckAnalysis = cardGroups
                    });
                }

                string json = JsonSerializer.Serialize(bestDecksData, new JsonSerializerOptions { WriteIndented = true });
                Directory.CreateDirectory("EvolutionData/BestDecks");
                File.WriteAllText($"EvolutionData/BestDecks/gen_{_generation:D4}_top10.json", json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to save best decks: {ex.Message}");
            }
        }

        private void GenerateCardAnalyticsReport()
        {
            try
            {
                var report = new List<object>();

                foreach (var stat in _cardStats.Values.OrderByDescending(s => s.UseRate).ThenByDescending(s => s.WinRate))
                {
                    report.Add(new
                    {
                        CardId = stat.CardId,
                        CardName = stat.CardName,
                        UseRate = stat.UseRate,
                        DeckCount = stat.DeckCount,
                        TotalDecks = stat.TotalDecks,
                        AverageDensity = stat.AverageDensity,
                        WinRate = stat.WinRate,
                        GamesPlayed = stat.GamesWithCard,
                        Wins = stat.WinsWithCard,
                        TotalCopies = stat.TotalCopiesInPopulation
                    });
                }

                string json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
                Directory.CreateDirectory("EvolutionData/Analytics");
                string fileName = $"EvolutionData/Analytics/card_analytics_gen{_generation:D4}.json";
                File.WriteAllText(fileName, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to generate card analytics: {ex.Message}");
            }
        }

        private void SaveCheckpoint()
        {
            try
            {
                var checkpoint = new EvolutionCheckpoint
                {
                    Generation = _generation,
                    Population = _population,
                    Species = _species.Select(s => new SpeciesSnapshot(s)).ToList(),
                    BestFitness = _bestFitness,
                    Timestamp = DateTime.Now
                };

                string json = JsonSerializer.Serialize(checkpoint,
                    new JsonSerializerOptions { WriteIndented = true, MaxDepth = 10 });

                Directory.CreateDirectory("EvolutionData/Checkpoints");
                File.WriteAllText($"EvolutionData/Checkpoints/checkpoint_gen{_generation:D4}.json", json);

                Console.WriteLine($"Checkpoint saved: Generation {_generation}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to save checkpoint: {ex.Message}");
            }
        }

        private void LoadCollectibleIds()
        {
            _collectibleCardIds = CardLibrary.Instance.GetAllIds()
                .Where(id => id < 900)
                .ToArray();

            if (_collectibleCardIds.Length == 0)
            {
                throw new InvalidOperationException("No collectible cards found. Check the card data.");
            }

            Console.WriteLine($"Loaded {_collectibleCardIds.Length} collectible cards");
        }

        private float CalculateConsistencyBonus(EvolvableIndividual individual, string archetype)
        {
            var deck = GetCachedDeck(individual);
            float consistencyScore = 0;

            switch (archetype)
            {
                case "Aggro":
                    float avgCost = CalculateAverageDeckCost(deck);
                    if (avgCost < 3.0f) consistencyScore += 0.5f;
                    if (avgCost < 2.5f) consistencyScore += 0.5f;
                    break;

                case "Control":
                    int controlCards = CountControlCards(deck);
                    if (controlCards > 8) consistencyScore += 1.0f;
                    break;

                case "Combo":
                    int comboPairs = CountComboPairs(deck);
                    consistencyScore += comboPairs * 0.1f;
                    break;
            }

            return consistencyScore * 3000f;
        }

        private void CreateImmigrantIndividuals(int count)
        {
            var rng = _threadRng.Value;
            for (int i = 0; i < count; i++)
            {
                if (rng.NextDouble() < 0.7f)
                {
                    var randomArchetype = _archetypePool.GetRandomArchetype(rng);
                    var dna = NewDna.CreateArchetypeDNA(randomArchetype, rng);
                    dna.ApplyArchetypeBias(randomArchetype, rng, strength: 0.95f);
                    _population.Add(new EvolvableIndividual(dna, _generation));
                }
                else
                {
                    var dna = NewDna.CreateRandom(rng);
                    _population.Add(new EvolvableIndividual(dna, _generation));
                }
            }
        }

        private void PrintDetailedStats()
        {
            var top5 = _population.OrderByDescending(x => x.CompositeFitness).Take(5).ToList();
            Console.WriteLine("\n=== TOP 5 BOTS ===");
            for (int i = 0; i < top5.Count; i++)
            {
                var bot = top5[i];
                Console.WriteLine($"{i + 1}. ID: {bot.Id} | Arch: {bot.Dna.ClassifyArchetype()} | " +
                                $"CompFit: {bot.CompositeFitness:F0} | BaseFit: {bot.BaseFitness:F0}");
            }
        }

        private void AnalyzeConvergence()
        {
            // Adaptive frequency - check more often if we're stagnating
            int checkFrequency = _stagnationCounter > 5 ? 3 : 8;

            if (_generation % checkFrequency == 0)
            {
                float diversity = CalculatePopulationDiversity();
                float avgFitness = _population.Average(x => x.CompositeFitness);
                float bestFitness = _population.Max(x => x.CompositeFitness);

                float convergenceRisk = CalculateConvergenceRisk(diversity, avgFitness, bestFitness);

                Console.WriteLine($"\n=== CONVERGENCE ANALYSIS ===");
                Console.WriteLine($"Diversity: {diversity:F3}");
                Console.WriteLine($"Avg/Best Ratio: {(avgFitness / Math.Max(1, bestFitness)):P1}");
                Console.WriteLine($"Stagnation: {_stagnationCounter} gens | Risk: {convergenceRisk:P0}");

                // Progressive intervention based on risk level
                if (convergenceRisk > 0.7f)
                {
                    Console.WriteLine("🔴 CRITICAL: High convergence risk!");
                    Console.WriteLine("   Performing aggressive diversity boost...");
                    PerformDiversityBoost();  // Use your existing function
                }
                else if (convergenceRisk > 0.4f)
                {
                    Console.WriteLine("🟡 WARNING: Moderate convergence risk!");
                    Console.WriteLine("   Adding extra immigrants...");
                    CreateImmigrantIndividuals(15);
                }
                else if (diversity < 0.2f && _stagnationCounter > 6)
                {
                    Console.WriteLine("⚪️  NOTICE: Early convergence signs");
                    Console.WriteLine("   Adding light immigrants...");
                    CreateImmigrantIndividuals(8);
                }
            }
        }

        private float CalculateConvergenceRisk(float diversity, float avgFitness, float bestFitness)
        {
            float risk = 0f;

            // Diversity component (40% weight)
            risk += (1.0f - Math.Clamp(diversity / 0.3f, 0f, 1f)) * 0.4f;

            // Stagnation component (30% weight)
            float stagnationRisk = Math.Clamp(_stagnationCounter / 15f, 0f, 1f);
            risk += stagnationRisk * 0.3f;

            // Fitness ratio component (30% weight)
            float fitnessRatio = avgFitness / Math.Max(1, bestFitness);
            risk += (1.0f - fitnessRatio) * 0.3f;

            return Math.Clamp(risk, 0f, 1f);
        }

        private void LogCardPopularity()
        {
            if (_generation % 25 == 0)
            {
                var topCards = _cardStats.Values
                    .Where(s => s.DeckCount > 0)
                    .OrderByDescending(s => s.UseRate)
                    .Take(10)
                    .ToList();

                Console.WriteLine("\n=== TOP 10 CARDS (Use Rate) ===");
                foreach (var card in topCards)
                {
                    Console.WriteLine($"  {card.CardName}: {card.UseRate:P1} | Win Rate: {card.WinRate:P1} | " +
                                    $"Decks: {card.DeckCount}/{_population.Count}");
                }
            }
        }
    }

    public class EvolvableIndividualConverter : System.Text.Json.Serialization.JsonConverter<EvolvableIndividual>
    {
        public override EvolvableIndividual Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            try
            {
                using (JsonDocument doc = JsonDocument.ParseValue(ref reader))
                {
                    var root = doc.RootElement;

                    if (!root.TryGetProperty("Dna", out var dnaElement))
                        return CreateRandomIndividual();

                    if (!dnaElement.TryGetProperty("Genes", out var genesElement))
                        return CreateRandomIndividual();

                    var genesList = new List<float>();
                    foreach (var gene in genesElement.EnumerateArray())
                    {
                        if (gene.ValueKind == JsonValueKind.Number)
                            genesList.Add(gene.GetSingle());
                        else
                            genesList.Add(5.0f);
                    }

                    while (genesList.Count < NewDna.TOTAL_GENES)
                        genesList.Add(5.0f);

                    if (genesList.Count > NewDna.TOTAL_GENES)
                        genesList = genesList.Take(NewDna.TOTAL_GENES).ToList();

                    var dna = new NewDna(genesList.ToArray());

                    int id = root.TryGetProperty("Id", out var idElement) ?
                             idElement.GetInt32() : Random.Shared.Next(10000, 99999);

                    int generation = root.TryGetProperty("Generation", out var genElement) ?
                                    genElement.GetInt32() : 0;

                    var individual = new EvolvableIndividual(dna, generation, id)
                    {
                        BaseFitness = root.TryGetProperty("BaseFitness", out var fitElement) ?
                                     fitElement.GetSingle() : 0f,
                        NoveltyScore = root.TryGetProperty("NoveltyScore", out var novElement) ?
                                      novElement.GetSingle() : 0f,
                        DiversityScore = root.TryGetProperty("DiversityScore", out var divElement) ?
                                        divElement.GetSingle() : 0f,
                        CompositeFitness = root.TryGetProperty("CompositeFitness", out var compElement) ?
                                          compElement.GetSingle() : 0f,
                        DeckNoveltyScore = root.TryGetProperty("DeckNoveltyScore", out var dNovElement) ? dNovElement.GetSingle() : 0f,
                    };

                    if (root.TryGetProperty("SpeciesId", out var speciesElement) &&
                        speciesElement.ValueKind == JsonValueKind.String)
                    {
                        if (Guid.TryParse(speciesElement.GetString(), out var speciesGuid))
                            individual.SpeciesId = speciesGuid;
                    }

                    return individual;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️  Error deserializing individual: {ex.Message}. Creating random individual.");
                return CreateRandomIndividual();
            }
        }

        public override void Write(Utf8JsonWriter writer, EvolvableIndividual value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            writer.WriteNumber("Id", value.Id);
            writer.WriteNumber("Generation", value.Generation);

            writer.WritePropertyName("Dna");
            writer.WriteStartObject();
            writer.WritePropertyName("Genes");
            writer.WriteStartArray();
            foreach (var gene in value.Dna.GetGenesArray())
                writer.WriteNumberValue(gene);
            writer.WriteEndArray();
            writer.WriteEndObject();

            writer.WriteNumber("BaseFitness", value.BaseFitness);
            writer.WriteNumber("NoveltyScore", value.NoveltyScore);
            writer.WriteNumber("DiversityScore", value.DiversityScore);
            writer.WriteNumber("DeckNoveltyScore", value.DeckNoveltyScore);
            writer.WriteNumber("CompositeFitness", value.CompositeFitness);

            if (value.SpeciesId.HasValue)
                writer.WriteString("SpeciesId", value.SpeciesId.Value.ToString());

            writer.WriteEndObject();
        }

        private EvolvableIndividual CreateRandomIndividual()
        {
            var rng = new Random();
            var dna = NewDna.CreateRandom(rng);
            return new EvolvableIndividual(dna, 0);
        }
    }

    public class EvolvableIndividual
    {
        public NewDna Dna { get; set; }
        public float BaseFitness { get; set; }
        public float NoveltyScore { get; set; }
        public float DiversityScore { get; set; }
        public float DeckNoveltyScore { get; set; }
        public float CompositeFitness { get; set; }
        public int Id { get; private set; }
        public int Generation { get; set; }
        public Guid? SpeciesId { get; set; }
        public List<float> Behaviors { get; set; } = new();

        private static int _nextId = 1;

        public EvolvableIndividual(NewDna dna, int generation)
        {
            Dna = dna;
            Generation = generation;
            Id = _nextId++;
        }

        [System.Text.Json.Serialization.JsonConstructor]
        public EvolvableIndividual(NewDna dna, int generation, int id)
        {
            Dna = dna;
            Generation = generation;
            Id = id;
            if (id >= _nextId)
                _nextId = id + 1;
        }

        public void ResetFitness()
        {
            BaseFitness = 0;
            NoveltyScore = 0;
            DiversityScore = 0;
            CompositeFitness = 0;
            Behaviors.Clear();
        }

        public void RecordBehavior((float score, GameResult result) matchResult)
        {
            Behaviors.Add(matchResult.result.Player1Health);
            Behaviors.Add(matchResult.result.Player2Health);
            Behaviors.Add(matchResult.result.Turns);
            Behaviors.Add(matchResult.score);
        }

        public EvolvableIndividual Clone()
        {
            return new EvolvableIndividual(Dna.Clone(), Generation)
            {
                BaseFitness = BaseFitness,
                NoveltyScore = NoveltyScore,
                DiversityScore = DiversityScore,
                DeckNoveltyScore = DeckNoveltyScore,
                CompositeFitness = CompositeFitness,
                SpeciesId = SpeciesId
            };
        }

        public static void SetNextId(int nextId)
        {
            _nextId = nextId;
        }
    }

    public class Species
    {
        public Guid Id { get; } = Guid.NewGuid();
        public NewDna Centroid { get; private set; }
        public List<EvolvableIndividual> Members { get; } = new();
        public float Threshold { get; }
        public float AverageFitness { get; private set; }

        public Species(EvolvableIndividual founder, float threshold)
        {
            Centroid = founder.Dna.Clone();
            Threshold = threshold;
            AddMember(founder);
        }

        public bool IsCompatible(NewDna dna)
        {
            float distance = NewDna.CalculateDistance(Centroid, dna);
            return distance < Threshold;
        }

        public void AddMember(EvolvableIndividual individual)
        {
            Members.Add(individual);
            individual.SpeciesId = Id;
            UpdateCentroid();
        }

        private void UpdateCentroid()
        {
            if (Members.Count == 0) return;
            Centroid = NewDna.Average(Members.Select(m => m.Dna).ToList());
        }

        public List<EvolvableIndividual> GetElites(int count)
        {
            return Members
                .OrderByDescending(m => m.CompositeFitness)
                .Take(Math.Min(count, Members.Count))
                .ToList();
        }

        public EvolvableIndividual GetBest()
        {
            return Members.OrderByDescending(m => m.CompositeFitness).FirstOrDefault();
        }

        public EvolvableIndividual GetRepresentative()
        {
            return Members
                .OrderBy(m => NewDna.CalculateDistance(m.Dna, Centroid))
                .FirstOrDefault();
        }

        public float CalculateDiversity(EvolvableIndividual individual)
        {
            if (Members.Count < 2) return 0;

            float totalDistance = 0;
            int count = 0;

            foreach (var member in Members)
            {
                if (member != individual)
                {
                    totalDistance += NewDna.CalculateDistance(individual.Dna, member.Dna);
                    count++;
                }
            }

            return count > 0 ? totalDistance / count : 0;
        }
    }

    public class BotArchetypePool
    {
        private List<BotArchetype> _archetypes = new();

        public int Count => _archetypes.Count;

        public void Initialize()
        {
            _archetypes.Add(new BotArchetype("Aggro"));
            _archetypes.Add(new BotArchetype("Control"));
            _archetypes.Add(new BotArchetype("Combo"));
            _archetypes.Add(new BotArchetype("Midrange"));
            _archetypes.Add(new BotArchetype("Sacrifice"));
        }

        public List<BotArchetype> GetAllArchetypes() => _archetypes.ToList();

        public BotArchetype GetArchetype(int index) =>
            _archetypes[index % _archetypes.Count];

        public BotArchetype GetRandomArchetype(Random rng) =>
            _archetypes[rng.Next(_archetypes.Count)];

        public List<BotArchetype> GetRandomArchetypes(int count, Random rng) =>
            _archetypes.OrderBy(x => rng.Next()).Take(count).ToList();

        public BotArchetype GetLeastUsedArchetype(List<EvolvableIndividual> population)
        {
            var usage = _archetypes.ToDictionary(a => a, a => 0);

            foreach (var ind in population)
            {
                var archetypeName = ind.Dna.ClassifyArchetype();
                var match = _archetypes.FirstOrDefault(a => a.Name == archetypeName);
                if (match != null)
                    usage[match]++;
            }

            return usage.OrderBy(x => x.Value).First().Key;
        }

        public string ClassifyArchetype(NewDna dna)
        {
            return dna.ClassifyArchetype();
        }
    }

    public class BotArchetype
    {
        public string Name { get; }

        public BotArchetype(string name)
        {
            Name = name;
        }

        public int[] BuildDeck(int[] cardPool, Random rng)
        {
            var deck = new List<int>();

            var allCards = cardPool.Select(id => CardCache.GetCard(id)).ToList();

            if (Name == "Aggro")
                allCards = allCards.OrderBy(c => c.Cost).ThenByDescending(c => c.Attack).ToList();
            else if (Name == "Control")
                allCards = allCards.OrderByDescending(c => c.Health + c.Attack).ThenBy(c => c.Cost).ToList();
            else if (Name == "Combo")
                allCards = allCards.OrderBy(c => c.Cost).ThenBy(c => c.Name).ToList();
            else if (Name == "Midrange")
                allCards = allCards.OrderBy(c => c.Cost).ThenByDescending(c => c.Attack + c.Health).ToList();
            else if (Name == "Sacrifice")
                allCards = allCards.OrderByDescending(c =>
                    (c.Effects?.Count(e => e.Trigger == TriggerType.OnDeath || e.Trigger == TriggerType.OnSacrificed) ?? 0))
                    .ThenBy(c => c.Cost).ToList();

            var counts = new Dictionary<int, int>();
            foreach (var card in allCards)
            {
                if (deck.Count >= 30) break;

                if (!counts.ContainsKey(card.Id)) counts[card.Id] = 0;

                if (counts[card.Id] < 3)
                {
                    deck.Add(card.Id);
                    counts[card.Id]++;
                }
            }

            return deck.ToArray();
        }

        public IAIStrategy CreateStrategy(Dictionary<int, CardStatsEnhanced> cardStats = null)
        {
            var rng = new Random();
            var dna = NewDna.CreateArchetypeDNA(this, rng);
            return new OptimizedNewEvolvableStrategy(dna, cardStats);
        }
    }

    public class EvolutionCheckpoint
    {
        public int Generation { get; set; }
        public List<EvolvableIndividual> Population { get; set; }
        public List<SpeciesSnapshot> Species { get; set; }
        public float BestFitness { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class SpeciesSnapshot
    {
        public Guid Id { get; set; }
        public int MemberCount { get; set; }
        public float AverageFitness { get; set; }

        public SpeciesSnapshot(Species species)
        {
            Id = species.Id;
            MemberCount = species.Members.Count;
            AverageFitness = species.Members.Any() ? species.Members.Average(m => m.CompositeFitness) : 0;
        }
    }

    public class GameResult
    {
        public int? WinnerId { get; set; }
        public int Player1Health { get; set; }
        public int Player2Health { get; set; }
        public int Turns { get; set; }
    }
}