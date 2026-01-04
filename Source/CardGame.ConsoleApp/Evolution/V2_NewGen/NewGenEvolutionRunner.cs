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
    // ========== METAGAME TRACKER ==========
    public class MetagameTracker
    {
        private ConcurrentDictionary<string, float> _archetypePrevalence = new();

        public void UpdateAfterGeneration(List<EvolvableIndividual> population)
        {
            var archetypeCounts = population
                .GroupBy(ind => ind.Dna.ClassifyArchetype())
                .ToDictionary(g => g.Key, g => g.Count());

            int total = population.Count;
            foreach (var archetype in new[] { "Aggro", "Control", "Combo", "Midrange", "Sacrifice" })
            {
                archetypeCounts.TryGetValue(archetype, out int count);
                float prevalence = total > 0 ? (float)count / total : 0;
                _archetypePrevalence[archetype] = prevalence;
            }
        }

        public (string archetype, float prevalence) GetMostPrevalentArchetype()
        {
            if (_archetypePrevalence.IsEmpty)
                return (null, 0);

            var mostPrevalent = _archetypePrevalence.OrderByDescending(kv => kv.Value).First();
            return (mostPrevalent.Key, mostPrevalent.Value);
        }

        public float GetAntiMetaBonus(EvolvableIndividual individual)
        {
            string archetype = individual.Dna.ClassifyArchetype();
            float bonus = 0;

            var mostPrevalent = GetMostPrevalentArchetype();

            if (IsCounterTo(archetype, mostPrevalent.archetype))
            {
                bonus += mostPrevalent.prevalence * 10000f;
            }

            return bonus;
        }

        private bool IsCounterTo(string archetype, string targetArchetype)
        {
            if (targetArchetype == null) return false;

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
    }

    // ========== SYNERGY LEARNER ==========
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

    // ========== DIVERSITY ENFORCER ==========
    public class DiversityEnforcer
    {
        private ConcurrentBag<int[]> _recentDecks = new();
        private readonly object _lock = new object();
        private const int DECK_MEMORY = 100;

        public float CalculateDeckNovelty(int[] deck)
        {
            var recentDecks = _recentDecks.ToArray();
            if (recentDecks.Length == 0) return 1.0f;

            float minSimilarity = float.MaxValue;
            foreach (var existingDeck in recentDecks)
            {
                float similarity = CalculateDeckSimilarity(deck, existingDeck);
                minSimilarity = Math.Min(minSimilarity, similarity);
            }

            return 1.0f - minSimilarity;
        }

        public void AddDeck(int[] deck)
        {
            lock (_lock)
            {
                _recentDecks.Add(deck.ToArray());

                // Ogranicz pamięć do DECK_MEMORY
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

    // ========== KLASY STATYSTYK KART ==========
    public class CardStatsEnhanced
    {
        public int CardId { get; set; }
        public string CardName { get; set; }
        public int TotalCopiesInPopulation { get; set; }
        public int DeckCount { get; set; }
        public int TotalDecks { get; set; }
        public float UseRate => DeckCount > 0 ? (float)DeckCount / TotalDecks : 0;
        public float AverageDensity { get; set; }
        private int _gamesWithCard;
        private int _winsWithCard;
        public int GamesWithCard => _gamesWithCard;
        public int WinsWithCard => _winsWithCard;
        public float WinRate => _gamesWithCard > 0 ? (float)_winsWithCard / _gamesWithCard : 0;
        public float PopularityScore { get; set; }

        private readonly object _lock = new object();

        public void ResetUsageStats()
        {
            lock (_lock)
            {
                TotalCopiesInPopulation = 0;
                DeckCount = 0;
                AverageDensity = 0;
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
            }
        }

        public void IncrementGames(bool won)
        {
            lock (_lock)
            {
                _gamesWithCard++;
                if (won) _winsWithCard++;
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

    // ========== HALL OF FAME (5+1 SYSTEM) ==========
    public class ArchetypeHallOfFame
    {
        private ConcurrentDictionary<string, List<ArchetypeChampion>> _pureChampions = new();
        private ArchetypeChampion _bestHybrid = null;
        private const int MAX_PURE_CHAMPIONS = 2;

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
            string archetype = candidate.Dna.ClassifyArchetype();
            float purity = ArchetypePurityCalculator.CalculatePurity(candidate.Dna, archetype);

            if (candidate.BaseFitness < avgPopulationFitness * 0.8f)
                return;

            if (IsDuplicate(candidate.Dna))
                return;

            if (purity >= 0.7f)
            {
                AddPureChampion(candidate, generation, archetype, purity, versatilityScore);
            }
            else if (purity <= 0.5f && versatilityScore >= 0.7f)
            {
                ConsiderForHybridSlot(candidate, generation, purity, versatilityScore);
            }
        }

        private void AddPureChampion(EvolvableIndividual candidate, int generation,
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
                LastUpdated = generation
            };

            lock (_pureChampions)
            {
                if (_pureChampions.TryGetValue(archetype, out var champions))
                {
                    champions.Add(champion);
                    champions = champions
                        .OrderByDescending(c => c.Individual.BaseFitness)
                        .Take(MAX_PURE_CHAMPIONS)
                        .ToList();
                    _pureChampions[archetype] = champions;
                }
            }

            Save();
        }

        private void ConsiderForHybridSlot(EvolvableIndividual candidate, int generation,
                                         float purity, float versatilityScore)
        {
            bool shouldReplace = false;

            if (_bestHybrid == null)
            {
                shouldReplace = true;
            }
            else
            {
                float fitnessImprovement = candidate.BaseFitness / _bestHybrid.Individual.BaseFitness;
                float versatilityImprovement = versatilityScore / _bestHybrid.VersatilityScore;

                if (fitnessImprovement > 1.1f ||
                   (fitnessImprovement > 1.05f && versatilityImprovement > 1.05f))
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
                        LastUpdated = generation
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
            foreach (var kvp in _pureChampions)
            {
                foreach (var champ in kvp.Value)
                {
                    if (NewDna.CalculateDistance(dna, champ.Dna) < 0.15f)
                        return true;
                }
            }

            if (_bestHybrid != null && NewDna.CalculateDistance(dna, _bestHybrid.Dna) < 0.15f)
                return true;

            return false;
        }

        public EvolvableIndividual GetBest(string archetype)
        {
            if (_pureChampions.TryGetValue(archetype, out var champions) && champions.Count > 0)
                return champions[0].Individual.Clone();

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

        public void PrintStatus()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n=== ARCHETYPE HALL OF FAME (5+1) ===");
            Console.ResetColor();

            Console.WriteLine("PURE MASTERS:");
            foreach (var kvp in _pureChampions.OrderBy(kvp => kvp.Key))
            {
                if (kvp.Value.Count == 0) continue;

                var best = kvp.Value[0];
                Console.WriteLine($"  {kvp.Key}: #{best.Individual.Id} (Base: {best.Individual.BaseFitness:F0}, Purity: {best.PurityScore:P0})");
                if (best.VersatilityScore > 0)
                    Console.WriteLine($"     Versatility: {best.VersatilityScore:P0} | Last: Gen {best.LastUpdated}");
            }

            if (_bestHybrid != null)
            {
                Console.WriteLine("\nBEST HYBRID:");
                Console.WriteLine($"  {_bestHybrid.Archetype}: #{_bestHybrid.Individual.Id} (Base: {_bestHybrid.Individual.BaseFitness:F0})");
                Console.WriteLine($"     Purity: {_bestHybrid.PurityScore:P0} | Versatility: {_bestHybrid.VersatilityScore:P0}");
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
                LastUpdated = LastUpdated
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
                LastUpdated = data.LastUpdated
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
    }

    // ========== KALKULATOR CZYSTOŚCI ==========
    public static class ArchetypePurityCalculator
    {
        public static float CalculatePurity(NewDna dna, string targetArchetype)
        {
            float targetScore = GetArchetypeStrength(dna, targetArchetype);

            string[] allArchetypes = { "Aggro", "Control", "Combo", "Midrange", "Sacrifice" };
            float maxOtherScore = allArchetypes
                .Where(a => a != targetArchetype)
                .Max(a => GetArchetypeStrength(dna, a));

            if (targetScore <= 0) return 0;

            float purity = (targetScore - maxOtherScore) / targetScore;
            return Math.Clamp(purity, 0, 1);
        }

        public static float GetArchetypeStrength(NewDna dna, string archetype)
        {
            return archetype switch
            {
                "Aggro" => dna.Genes[NewDna.STYLE_AGGRO],
                "Control" => dna.Genes[NewDna.STYLE_CONTROL],
                "Combo" => dna.Genes[NewDna.STYLE_COMBO],
                "Midrange" => dna.Genes[NewDna.STYLE_MIDRANGE],
                "Sacrifice" => (dna.Genes[NewDna.TRIGGER_SACRIFICE_VISION] * 0.7f +
                               dna.Genes[NewDna.TRIGGER_DEATH_STRATEGY] * 0.3f),
                _ => 0
            };
        }

        public static Dictionary<string, float> GetAllArchetypeStrengths(NewDna dna)
        {
            return new Dictionary<string, float>
            {
                ["Aggro"] = GetArchetypeStrength(dna, "Aggro"),
                ["Control"] = GetArchetypeStrength(dna, "Control"),
                ["Combo"] = GetArchetypeStrength(dna, "Combo"),
                ["Midrange"] = GetArchetypeStrength(dna, "Midrange"),
                ["Sacrifice"] = GetArchetypeStrength(dna, "Sacrifice")
            };
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
        }

        private static int[] GetArchetypeGenes(string archetype)
        {
            return archetype switch
            {
                "Aggro" => new[] { NewDna.STYLE_AGGRO, NewDna.STYLE_TEMPO_PREFERENCE },
                "Control" => new[] { NewDna.STYLE_CONTROL, NewDna.STYLE_FUTURE_PLANNING_BIAS },
                "Combo" => new[] { NewDna.STYLE_COMBO, NewDna.LOGIC_TUTOR_PRECISION },
                "Midrange" => new[] { NewDna.STYLE_MIDRANGE, NewDna.UNIT_STAT_VS_EFFECT_WEIGHT },
                "Sacrifice" => new[] { NewDna.TRIGGER_SACRIFICE_VISION, NewDna.TRIGGER_DEATH_STRATEGY },
                _ => Array.Empty<int>()
            };
        }
    }

    // ========== STATYSTYKI MIĘDZYARCHETYPOWE ==========
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

    // ========== NOVELTY WEWNĄTRZ ARCHETYPU ==========
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

    // ========== EXTENSION METHODS ==========
    public static class GameStateExtensions
    {
        public static bool IsGameOver(this GameState state)
        {
            return state.PlayerA.Health <= 0 || state.PlayerB.Health <= 0;
        }
    }

    // ========== MAIN RUNNER ==========
    public class EvolutionRunnerV2
    {
        // ========== TURBO KONFIGURACJA dla 8c/16t ==========
        private const int OPTIMAL_PARALLELISM = 24;
        private const int OPTIMAL_POPULATION = 100;
        private const float OPTIMAL_MUTATION = 0.14f;
        private const int OPTIMAL_IMMIGRANTS = 22;
        private const int OPTIMAL_MATCHES = 6;
        private const int OPTIMAL_MAX_MOVES = 120;
        private const int OPTIMAL_TOURNAMENT = 3;
        private const int OPTIMAL_ELITES = 3;

        // ========== KONFIGURACJA ==========
        private const int POPULATION_SIZE = OPTIMAL_POPULATION;
        private const float SPECIATION_THRESHOLD = 0.3f;
        private const float MUTATION_RATE = OPTIMAL_MUTATION;
        private const int IMMIGRANTS_PER_GEN = OPTIMAL_IMMIGRANTS;
        private const int TOURNAMENT_SIZE = OPTIMAL_TOURNAMENT;
        private const int ELITES_PER_SPECIES = OPTIMAL_ELITES;

        // ========== STAN ==========
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

        private static readonly ThreadLocal<Random> _threadRng = new(() =>
            new Random(Guid.NewGuid().GetHashCode() ^ Environment.TickCount));

        private int[] _collectibleCardIds;

        // ========== PERFORMANCE MONITORING ==========
        private Stopwatch _generationTimer = new Stopwatch();
        private int _totalGamesLastGen = 0;

        public async Task RunEvolutionAsync()
        {
            Console.Clear();

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("🚀 EVOLUTION 4.0 TURBO - 8c/16t OPTIMIZED");
            Console.WriteLine("   Finding specialized masters at maximum speed.\n");
            Console.ResetColor();

            CheckForAutoResume();
            ProcessDataManagement();

            CardLibrary.Instance.LoadFromJson("Data/Cards/cards.json");
            LoadCollectibleIds();

            Console.WriteLine("=== EVOLUTION RUNNER 4.0 TURBO ===");

            bool loadPopulation = HandleResumeDecision();

            if (loadPopulation)
            {
                if (LoadLatestPopulation())
                {
                    Console.WriteLine($"✓ Continuing from generation {_generation}...");
                }
                else
                {
                    Console.WriteLine("⚠️  Could not load saved population. Initializing from Hall of Fame...");
                    InitializePopulationFromHallOfFame();
                }
            }
            else
            {
                Console.WriteLine("Starting fresh population...");
                InitializePopulation();
            }

            _hallOfFame.Initialize();
            _archetypePool.Initialize();

            PrintHardwareInfo();

            Console.WriteLine($"Population: {POPULATION_SIZE} | Species Threshold: {SPECIATION_THRESHOLD}");
            Console.WriteLine($"Mutation Rate: {MUTATION_RATE:P0} | Immigrants: {IMMIGRANTS_PER_GEN}");
            Console.WriteLine($"Matches per bot: {OPTIMAL_MATCHES} random + 2 archetype");
            Console.ResetColor();

            bool shouldStop = false;

            EnsureDirectories();

            while (!shouldStop)
            {
                _generation++;
                _generationTimer.Restart();

                Console.WriteLine($"\n=== GENERATION {_generation:D3} ===");

                PerformSpeciation();
                Console.WriteLine($"Species: {_species.Count} | Diversity: {CalculatePopulationDiversity():F3}");

                EvaluatePopulation();
                UpdateHallOfFame();
                LogGenerationStats();
                CheckStagnation();
                BreedNewPopulation();
                SaveLatestPopulation();

                if (_generation % 5 == 0)
                {
                    PerformAnalytics();
                    SaveBestDecks();
                }

                if (_generation % 10 == 0)
                {
                    SaveCheckpoint();
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

        private void InitializePopulation()
        {
            _population.Clear();

            int archetypesCount = _archetypePool.Count;
            int perArchetype = POPULATION_SIZE / (archetypesCount + 1);

            for (int a = 0; a < archetypesCount; a++)
            {
                var archetype = _archetypePool.GetArchetype(a);

                for (int i = 0; i < perArchetype; i++)
                {
                    var dna = NewDna.CreateRandom(_threadRng.Value);
                    dna.ApplyArchetypeBias(archetype, _threadRng.Value, strength: 0.7f);
                    _population.Add(new EvolvableIndividual(dna, _generation));
                }
            }

            while (_population.Count < POPULATION_SIZE)
            {
                var dna = NewDna.CreateRandom(_threadRng.Value);
                _population.Add(new EvolvableIndividual(dna, _generation));
            }

            Console.WriteLine($"Initialized population: {_population.Count} individuals");
        }

        private void InitializePopulationFromHallOfFame()
        {
            _population.Clear();
            var rng = _threadRng.Value;

            Console.WriteLine("Initializing population from Hall of Fame champions...");

            var champions = _hallOfFame.GetAllChampions();

            if (champions == null || champions.Count == 0)
            {
                Console.WriteLine("No champions in Hall of Fame. Starting fresh.");
                InitializePopulation();
                return;
            }

            foreach (var champion in champions)
            {
                if (champion != null && champion.Dna != null)
                {
                    _population.Add(champion.Clone());
                }
            }

            Console.WriteLine($"Added {_population.Count} champions from Hall of Fame");

            if (_population.Count > 0)
            {
                int maxId = _population.Max(ind => ind.Id);
                EvolvableIndividual.SetNextId(maxId + 1);
            }

            while (_population.Count < POPULATION_SIZE)
            {
                if (_population.Count > 0)
                {
                    var parent1 = _population[rng.Next(_population.Count)];
                    var parent2 = _population[rng.Next(_population.Count)];
                    var childDna = NewDna.Crossover(parent1.Dna, parent2.Dna, rng);

                    if (rng.NextDouble() < 0.3f)
                        childDna.Mutate(rng, _stagnationCounter);

                    _population.Add(new EvolvableIndividual(childDna, _generation));
                }
                else
                {
                    var dna = NewDna.CreateRandom(rng);
                    _population.Add(new EvolvableIndividual(dna, _generation));
                }
            }

            Console.WriteLine($"Final population size: {_population.Count} individuals");
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
            foreach (var stat in _cardStats.Values)
            {
                stat.ResetUsageStats();
                stat.TotalDecks = _population.Count;
            }

            var deckCardPresence = new ConcurrentDictionary<int, int>();
            var cardCopyCounts = new ConcurrentDictionary<int, int>();

            foreach (var individual in _population)
            {
                var deck = individual.Dna.BuildDeck(_collectibleCardIds, _threadRng.Value);
                var uniqueCards = deck.Distinct();

                foreach (var cardId in uniqueCards)
                {
                    deckCardPresence.AddOrUpdate(cardId, 1, (key, oldValue) => oldValue + 1);
                }

                foreach (var cardId in deck)
                {
                    cardCopyCounts.AddOrUpdate(cardId, 1, (key, oldValue) => oldValue + 1);
                }
            }

            foreach (var kvp in deckCardPresence)
            {
                int cardId = kvp.Key;
                int deckCount = kvp.Value;
                cardCopyCounts.TryGetValue(cardId, out int totalCopies);

                var stat = _cardStats.GetOrAdd(cardId, new CardStatsEnhanced
                {
                    CardId = cardId,
                    CardName = GetCardNameById(cardId),
                    TotalDecks = _population.Count,
                   
                });

                stat.UpdateUsageStats(deckCount, totalCopies, _population.Count);
            }
        }

        private void UpdateWinRateStats(EvolvableIndividual winner, EvolvableIndividual loser, GameResult result)
        {
            if (winner == null || loser == null) return;

            try
            {
                var winnerDeck = winner.Dna.BuildDeck(_collectibleCardIds, _threadRng.Value);
                var loserDeck = loser.Dna.BuildDeck(_collectibleCardIds, _threadRng.Value);

                EnsureCardsInStats(winnerDeck.Concat(loserDeck).Distinct());

                foreach (var cardId in winnerDeck.Distinct())
                {
                    if (_cardStats.TryGetValue(cardId, out var stat))
                    {
                        stat.IncrementGames(true);
                    }
                }

                foreach (var cardId in loserDeck.Distinct())
                {
                    if (_cardStats.TryGetValue(cardId, out var stat))
                    {
                        stat.IncrementGames(false);
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
                _cardStats.GetOrAdd(cardId, new CardStatsEnhanced
                {
                    CardId = cardId,
                    CardName = GetCardNameById(cardId),
                    TotalDecks = _population.Count,
                  
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

            UpdateCardStatistics();

            var parallelOptions = GetTurboParallelOptions();

            Parallel.ForEach(_population, parallelOptions, individual =>
            {
                individual.ResetFitness();
                individual.Behaviors.Clear();
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

                    var individualDeck = individual.Dna.BuildDeck(_collectibleCardIds, localRng);
                    var opponentDeck = opponent.Dna.BuildDeck(_collectibleCardIds, localRng);

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

                individual.CompositeFitness = CalculateCompositeFitness(individual);

                Interlocked.Add(ref _totalGamesLastGen, gamesPlayed);
            });

            _metagameTracker.UpdateAfterGeneration(_population);

            foreach (var individual in _population)
            {
                var deck = individual.Dna.BuildDeck(_collectibleCardIds, _threadRng.Value);
                float novelty = _diversityEnforcer.CalculateDeckNovelty(deck);
                individual.DeckNoveltyScore = novelty;
                _diversityEnforcer.AddDeck(deck);
            }
        }

        private float CalculateCompositeFitness(EvolvableIndividual individual)
        {
            string archetype = individual.Dna.ClassifyArchetype();
            float purity = ArchetypePurityCalculator.CalculatePurity(individual.Dna, archetype);
            float versatility = CalculateVersatilityScore(individual);
            float avgFitness = _population.Average(ind => ind.BaseFitness);

            float baseScore = individual.BaseFitness * 0.5f;
            float typeBonus = 0f;

            if (purity >= 0.7f)
            {
                if (individual.BaseFitness > avgFitness * 0.8f)
                {
                    typeBonus = individual.BaseFitness * purity * 0.25f;
                }
            }
            else if (purity <= 0.6f && versatility >= 0.6f)
            {
                if (individual.BaseFitness > avgFitness * 0.9f)
                {
                    typeBonus = individual.BaseFitness * versatility * 0.15f;
                }
            }

            float noveltyWeight = Math.Max(0.02f, 0.2f - _generation * 0.0003f);
            float noveltyBonus = individual.NoveltyScore * 500f * noveltyWeight;

            float rareCardBonus = CalculateRareCardBonus(individual);
            rareCardBonus = Math.Min(rareCardBonus, baseScore * 0.03f);

            float consistencyBonus = CalculateDeckConsistencyBonus(individual);
            float toolboxPenalty = CalculateToolboxPenalty(individual);
            float synergyBonus = CalculateDeckSynergyBonus(individual);
            float antiMetaBonus = _metagameTracker.GetAntiMetaBonus(individual) * 0.1f;
            float deckNoveltyBonus = individual.DeckNoveltyScore * 3000f;

            float totalFitness = baseScore + typeBonus + noveltyBonus + rareCardBonus +
                                 consistencyBonus - toolboxPenalty + synergyBonus +
                                 antiMetaBonus + deckNoveltyBonus;

            if (individual.BaseFitness < avgFitness * 0.5f)
                totalFitness *= 0.3f;

            return Math.Max(totalFitness, 1000);
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

        private float CalculateToolboxPenalty(EvolvableIndividual individual)
        {
            var rng = _threadRng.Value;
            var deck = individual.Dna.BuildDeck(_collectibleCardIds, rng);
            int uniqueCards = deck.Distinct().Count();

            if (uniqueCards <= 15) return 0;

            float penalty = 0;
            int excess = uniqueCards - 15;

            string archetype = individual.Dna.ClassifyArchetype();
            float archetypeModifier = archetype == "Combo" ? 0.5f :
                                      archetype == "Control" ? 0.8f : 1.0f;

            penalty = excess * 800f * archetypeModifier;

            return penalty;
        }

        private float CalculateDeckConsistencyBonus(EvolvableIndividual individual)
        {
            var rng = _threadRng.Value;
            var deck = individual.Dna.BuildDeck(_collectibleCardIds, rng);
            var cardLibrary = CardLibrary.Instance;

            float bonus = 0;
            string archetype = individual.Dna.ClassifyArchetype();

            int archetypeCards = 0;
            foreach (var cardId in deck)
            {
                try
                {
                    var card = cardLibrary.GetCard(cardId);

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

        private float CalculateDeckSynergyBonus(EvolvableIndividual individual)
        {
            var rng = _threadRng.Value;
            var deck = individual.Dna.BuildDeck(_collectibleCardIds, rng);
            var cardLibrary = CardLibrary.Instance;

            float bonus = 0;

            var cardList = deck.Select(id =>
            {
                try { return cardLibrary.GetCard(id); }
                catch { return null; }
            }).Where(c => c != null).ToList();

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

        private float CalculateRareCardBonus(EvolvableIndividual individual)
        {
            var deck = individual.Dna.BuildDeck(_collectibleCardIds, _threadRng.Value);
            var uniqueCards = deck.Distinct();

            float bonus = 0;
            int rareCardsCount = 0;

            foreach (var cardId in uniqueCards)
            {
                if (_cardStats.TryGetValue(cardId, out var stat) && stat.UseRate < 0.1f)
                {
                    float rarity = 0.1f - stat.UseRate;
                    bonus += rarity * rarity * 5000f;
                    rareCardsCount++;
                }
            }

            if (rareCardsCount > 10)
                bonus *= 0.5f;

            return bonus;
        }

        private float CalculateAverageDeckCost(int[] deck)
        {
            float totalCost = 0;
            foreach (var cardId in deck)
            {
                try
                {
                    var card = CardLibrary.Instance.GetCard(cardId);
                    totalCost += card.Cost;
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
                    var card = CardLibrary.Instance.GetCard(cardId);
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
                    var card = CardLibrary.Instance.GetCard(cardId);
                    return card.Cost;
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

            var top10 = _population.OrderByDescending(x => x.BaseFitness).Take(10).ToList();
            foreach (var ind in top10)
            {
                versatilityScores.TryGetValue(ind.Id, out float versatility);
                _hallOfFame.AddCandidate(ind, _generation, avgFitness, versatility);
            }

            if (_generation % 10 == 0)
            {
                _hallOfFame.PrintStatus();
            }
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

            var deck1 = player1.Dna.BuildDeck(_collectibleCardIds, rng);
            var deck2 = player2.Dna.BuildDeck(_collectibleCardIds, rng);

            var engine = new GameEngine(
                GameState.Initial(
                    1,
                    deck1.Select(id => factory.CreateCard(id, 1)).ToList(),
                    deck2.Select(id => factory.CreateCard(id, 2)).ToList(),
                    deterministicRng),
                seed);

            var strategy1 = new NewEvolvableStrategy(player1.Dna);
            var strategy2 = new NewEvolvableStrategy(player2.Dna);

            return SimulateGame(engine, strategy1, strategy2, player1, player2);
        }

        private (float score, GameResult result) PlayMatchAgainstArchetype(
            EvolvableIndividual player,
            BotArchetype archetype,
            Random rng)
        {
            var seed = rng.Next();
            var deterministicRng = new CardGame.Core.Application.DeterministicRng(seed);
            var factory = new CardGame.Core.Cards.Factories.CardFactory(
                CardLibrary.Instance, deterministicRng);

            var playerDeck = player.Dna.BuildDeck(_collectibleCardIds, rng);
            var archetypeDeck = archetype.BuildDeck(_collectibleCardIds, rng);

            var engine = new GameEngine(
                GameState.Initial(
                    1,
                    playerDeck.Select(id => factory.CreateCard(id, 1)).ToList(),
                    archetypeDeck.Select(id => factory.CreateCard(id, 2)).ToList(),
                    deterministicRng),
                seed);

            var playerStrategy = new NewEvolvableStrategy(player.Dna);
            var archetypeStrategy = archetype.CreateStrategy();

            return SimulateGame(engine, playerStrategy, archetypeStrategy, player, null);
        }

        private (float score, GameResult result) SimulateGame(
            GameEngine engine,
            IAIStrategy strategy1,
            IAIStrategy strategy2,
            EvolvableIndividual player1,
            EvolvableIndividual player2)
        {
            var solver1 = new BotSolver(engine, 1, strategy1, 3, 3);
            var solver2 = new BotSolver(engine, 2, strategy2, 3, 3);

            int maxMoves = OPTIMAL_MAX_MOVES;
            int moves = 0;

            while (engine.CurrentState.CurrentPhase == GamePhase.Mulligan && moves++ < 10)
            {
                if (!engine.CurrentState.PlayersReady.Contains(1))
                    engine.ExecuteCommand(new ConfirmMulliganCommand(1, new()));
                if (!engine.CurrentState.PlayersReady.Contains(2))
                    engine.ExecuteCommand(new ConfirmMulliganCommand(2, new()));
            }

            while (!engine.CurrentState.IsGameOver() && moves++ < maxMoves)
            {
                var state = engine.CurrentState;
                var solver = state.ActivePlayerId == 1 ? solver1 : solver2;

                var bestMove = solver.FindBestMoves(state).FirstOrDefault();
                if (bestMove?.Command != null)
                {
                    engine.ExecuteCommand(bestMove.Command);
                }
                else
                {
                    engine.ExecuteCommand(new EndPhaseCommand(state.ActivePlayerId));
                }
            }

            var finalState = engine.CurrentState;
            int? winnerId = null;
            if (finalState.PlayerB.Health <= 0) winnerId = 1;
            else if (finalState.PlayerA.Health <= 0) winnerId = 2;

            var result = new GameResult
            {
                WinnerId = winnerId,
                Player1Health = finalState.PlayerA.Health,
                Player2Health = finalState.PlayerB.Health,
                Turns = moves / 2
            };

            float score = CalculateMatchScore(finalState, winnerId == 1);

            return (score, result);
        }

        private float CalculateMatchScore(GameState state, bool isPlayer1)
        {
            var player = isPlayer1 ? state.PlayerA : state.PlayerB;
            var opponent = isPlayer1 ? state.PlayerB : state.PlayerA;

            float score = 0;

            score += player.Health * 50f;
            score -= opponent.Health * 30f;

            var playerUnits = state.Board.GetAllUnits()
                .Where(u => u.OwnerPlayerId == player.PlayerId)
                .ToList();

            var opponentUnits = state.Board.GetAllUnits()
                .Where(u => u.OwnerPlayerId == opponent.PlayerId)
                .ToList();

            score += playerUnits.Sum(u => u.CurrentStats.Attack * 20f + u.CurrentStats.Health * 15f);
            score -= opponentUnits.Sum(u => u.CurrentStats.Attack * 10f + u.CurrentStats.Health * 8f);

            score += player.Hand.Count * 40f;
            score -= opponent.Hand.Count * 20f;

            if (state.IsGameOver() && (
                (isPlayer1 && state.PlayerA.Health > 0) ||
                (!isPlayer1 && state.PlayerB.Health > 0)))
            {
                score += 30000f;
            }

            if (state.IsGameOver() && (
                (isPlayer1 && state.PlayerA.Health <= 0) ||
                (!isPlayer1 && state.PlayerB.Health <= 0)))
            {
                score -= 20000f;
            }

            return score;
        }

        private void BreedNewPopulation()
        {
            var newPopulation = new List<EvolvableIndividual>();
            var rng = _threadRng.Value;

            var byArchetype = _population
                .GroupBy(x => x.Dna.ClassifyArchetype())
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.BaseFitness).ToList());

            foreach (var (archetype, group) in byArchetype)
            {
                int elitesToTake = Math.Min(OPTIMAL_ELITES + 1, group.Count);
                var elites = group.Take(elitesToTake).Select(e => e.Clone());
                newPopulation.AddRange(elites);
            }

            foreach (var (archetype, group) in byArchetype)
            {
                int targetFromThisArchetype = (int)(POPULATION_SIZE * 0.7f / byArchetype.Count);
                int currentFromThisArchetype = newPopulation.Count(p => p.Dna.ClassifyArchetype() == archetype);

                while (currentFromThisArchetype < targetFromThisArchetype && group.Count >= 2)
                {
                    var parent1 = TournamentSelect(group, TOURNAMENT_SIZE);
                    var parent2 = TournamentSelect(group.Where(g => g != parent1).ToList(), TOURNAMENT_SIZE);

                    if (parent1 == null || parent2 == null) break;

                    var childDna = NewDna.Crossover(parent1.Dna, parent2.Dna, rng);
                    ArchetypePurityCalculator.EnforceArchetype(childDna, archetype, rng, 0.4f);

                    if (rng.NextDouble() < MUTATION_RATE)
                        childDna.Mutate(rng, Math.Max(1, _stagnationCounter / 5));

                    newPopulation.Add(new EvolvableIndividual(childDna, _generation));
                    currentFromThisArchetype++;
                }
            }

            while (newPopulation.Count < POPULATION_SIZE)
            {
                if (rng.NextDouble() < 0.7f)
                {
                    var randomArchetype = _archetypePool.GetRandomArchetype(rng);
                    var dna = NewDna.CreateArchetypeDNA(randomArchetype, rng);
                    dna.ApplyArchetypeBias(randomArchetype, rng, strength: 0.95f);
                    newPopulation.Add(new EvolvableIndividual(dna, _generation));
                }
                else
                {
                    var dna = NewDna.CreateRandom(rng);
                    newPopulation.Add(new EvolvableIndividual(dna, _generation));
                }
            }

            _population = newPopulation.Take(POPULATION_SIZE).ToList();
        }

        private EvolvableIndividual TournamentSelect(List<EvolvableIndividual> group, int tournamentSize)
        {
            if (group.Count == 0) return null;
            if (group.Count == 1) return group[0];

            var tournament = group
                .OrderBy(x => _threadRng.Value.Next())
                .Take(Math.Min(tournamentSize, group.Count))
                .OrderByDescending(x => x.CompositeFitness)
                .First();

            return tournament;
        }

        private void CheckStagnation()
        {
            var currentBest = _population.Max(x => x.CompositeFitness);

            if (currentBest > _bestFitness * 1.01f)
            {
                _bestFitness = currentBest;
                _stagnationCounter = 0;
            }
            else
            {
                _stagnationCounter++;
            }

            if (_stagnationCounter > 15)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Stagnation detected - increasing diversity...");
                Console.ResetColor();

                int replaceCount = POPULATION_SIZE / 4;
                var rng = _threadRng.Value;

                for (int i = 0; i < replaceCount; i++)
                {
                    int index = rng.Next(_population.Count);
                    var newDna = NewDna.CreateRandom(rng);

                    var newArchetype = _archetypePool.GetRandomArchetype(rng);
                    newDna.ApplyArchetypeBias(newArchetype, rng, strength: 0.8f);

                    _population[index] = new EvolvableIndividual(newDna, _generation);
                }

                _stagnationCounter = 0;
            }
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

            Console.WriteLine($"Best: {best.Id} | Composite: {best.CompositeFitness:F0} | Base: {best.BaseFitness:F0}");
            Console.WriteLine($"Avg Composite: {avgFitness:F0} | Avg Base: {avgBaseFitness:F0}");
            Console.WriteLine($"Stagnation: {_stagnationCounter} gens");

            var archetypeCounts = _population
                .GroupBy(ind => ind.Dna.ClassifyArchetype())
                .Select(g => new { Archetype = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count);

            Console.WriteLine("\n=== ARCHETYPE DISTRIBUTION ===");
            foreach (var arch in archetypeCounts)
            {
                Console.WriteLine($"  {arch.Archetype}: {arch.Count} bots ({(float)arch.Count / _population.Count:P0})");
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
                    var deck = ind.Dna.BuildDeck(_collectibleCardIds, rng);

                    var deckAnalysis = deck
                        .GroupBy(cardId => cardId)
                        .Select(g => new
                        {
                            CardId = g.Key,
                            CardName = GetCardNameById(g.Key),
                            Count = g.Count(),
                            CardData = GetCardData(g.Key)
                        })
                        .ToList();

                    bestDecksData.Add(new
                    {
                        IndividualId = ind.Id,
                        BaseFitness = ind.BaseFitness,
                        CompositeFitness = ind.CompositeFitness,
                        Archetype = ind.Dna.ClassifyArchetype(),
                        ArchetypePurity = ArchetypePurityCalculator.CalculatePurity(ind.Dna, ind.Dna.ClassifyArchetype()),
                        Deck = deck,
                        DeckAnalysis = deckAnalysis
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

        private string GetCardNameById(int cardId)
        {
            try
            {
                var card = CardLibrary.Instance.GetCard(cardId);
                return card.Name;
            }
            catch
            {
                return $"Card_{cardId}";
            }
        }

        private object GetCardData(int cardId)
        {
            try
            {
                var card = CardLibrary.Instance.GetCard(cardId);
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
            catch
            {
                return null;
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

            Console.WriteLine($"Loaded {_collectibleCardIds.Length} collectible cards");
        }

        // ========== DODATKOWE METODY, KTÓRE MOGŁY ZOSTAĆ POMINIĘTE ==========

        private float CalculateConsistencyBonus(EvolvableIndividual individual, string archetype)
        {
            var deck = individual.Dna.BuildDeck(_collectibleCardIds, _threadRng.Value);
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

            var worst5 = _population.OrderBy(x => x.CompositeFitness).Take(5).ToList();
            Console.WriteLine("\n=== WORST 5 BOTS ===");
            for (int i = 0; i < worst5.Count; i++)
            {
                var bot = worst5[i];
                Console.WriteLine($"{i + 1}. ID: {bot.Id} | Arch: {bot.Dna.ClassifyArchetype()} | " +
                                $"CompFit: {bot.CompositeFitness:F0} | BaseFit: {bot.BaseFitness:F0}");
            }
        }

        private void AnalyzeConvergence()
        {
            if (_generation % 20 == 0)
            {
                float diversity = CalculatePopulationDiversity();
                float avgFitness = _population.Average(x => x.CompositeFitness);
                float bestFitness = _population.Max(x => x.CompositeFitness);

                Console.WriteLine($"\n=== CONVERGENCE ANALYSIS ===");
                Console.WriteLine($"Diversity: {diversity:F3}");
                Console.WriteLine($"Avg/Best Ratio: {(avgFitness / bestFitness):P1}");
                Console.WriteLine($"Stagnation: {_stagnationCounter} generations");

                if (diversity < 0.1f && _stagnationCounter > 10)
                {
                    Console.WriteLine("⚠️  WARNING: Population is converging!");
                    Console.WriteLine("   Adding extra immigrants...");
                    CreateImmigrantIndividuals(10);
                }
            }
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

    // ========== IMPROVED JSON CONVERTER ==========
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
                writer.WriteString("SpeciesId", value.SpeciesId.Value);

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
            var cardLibrary = CardLibrary.Instance;

            var allCards = cardPool.Select(id => cardLibrary.GetCard(id)).ToList();

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

        public IAIStrategy CreateStrategy()
        {
            var rng = new Random();
            var dna = NewDna.CreateArchetypeDNA(this, rng);
            return new NewEvolvableStrategy(dna);
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