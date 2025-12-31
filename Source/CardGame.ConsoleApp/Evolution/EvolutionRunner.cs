using CardGame.Core.AI.Logic;
using CardGame.Core.AI.Strategies;
using CardGame.Core.Application;
using CardGame.Core.Cards.Data;
using CardGame.Core.Commands.Implementations;
using CardGame.Core.State.Enums;
using CardGame.Core.State.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.IO;

namespace CardGame.ConsoleApp.Evolution
{
    public class EvolutionRunner
    {
        private List<GeneticIndividual> _population = new();
        private readonly EvolutionLeague _league = new();
        private readonly HallOfFame _hof = new();
        private const int PopulationSize = 100;
        private static readonly ThreadLocal<Random> _threadRng = new(() => new Random());
        private int[] _collectibleCardIds;

        public async Task RunEvolutionAsync(int generations)
        {
            Console.Clear();
            if (CardLibrary.Instance.GetAllIds().Length == 0)
                CardLibrary.Instance.LoadFromJson("Data/Cards/cards.json");

            LoadCollectibleIds();
            InitializePopulation();
            _hof.Load();

            string bestBotPath = "best_bot_dna.json";
            float highestFitnessEver = -1000000;

            var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };

            for (int g = 1; g <= generations; g++)
            {
                int currentDepth = (g < 50) ? 2 : (g < 200 ? 3 : 4);
                Console.WriteLine($"[GEN {g}] AI Power: Width 3 / Depth {currentDepth} | Pop: {_population.Count}");
                ResetStats();

                Parallel.For(0, _population.Count, parallelOptions, i =>
                {
                    var localRng = _threadRng.Value;
                    var subject = _population[i];
                    for (int m = 0; m < 5; m++)
                    {
                        GeneticIndividual opponent;
                        if (i < 20 && _hof.Champions.Count > 2 && _hof.Champions[2] != null) opponent = _hof.Champions[2];
                        else if (i < 40 && _hof.Champions.Count > 1 && _hof.Champions[1] != null) opponent = _hof.Champions[1];
                        else opponent = _population[localRng.Next(PopulationSize)];

                        if (subject == opponent) continue;
                        var result = SimulateFastMatchWithFitness(subject, opponent, currentDepth);

                        lock (subject) { subject.GamesPlayed++; subject.Fitness += result.p1Score; if (result.winner == 1) subject.Wins++; }
                        lock (opponent) { opponent.GamesPlayed++; opponent.Fitness += result.p2Score; if (result.winner == 2) opponent.Wins++; }
                    }
                });

                var sorted = _population.OrderByDescending(x => x.Fitness).ToList();
                if (!sorted.Any()) continue;

                var bestInGen = sorted.First();

                
                _hof.ProcessPopulation(sorted);

            
                _hof.Load();
                var absoluteChampion = _hof.Champions
                    .Where(c => c != null)
                    .OrderByDescending(c => c.Fitness)
                    .FirstOrDefault();

            
                Console.WriteLine($"[GEN {g}] Depth: {currentDepth} | Top WR: {bestInGen.WinRate:P0} ({bestInGen.Wins}/{bestInGen.GamesPlayed}) | Fit: {bestInGen.Fitness:F0}");

               
                if (absoluteChampion != null && absoluteChampion.Fitness > highestFitnessEver)
                {
                    highestFitnessEver = absoluteChampion.Fitness;

               
                    var options = new JsonSerializerOptions { WriteIndented = true };
                    File.WriteAllText(bestBotPath, JsonSerializer.Serialize(absoluteChampion, options));

                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"[!] GLOBAL RECORD: {absoluteChampion.Id} (Fit: {absoluteChampion.Fitness:F0}) - Apex Predator Updated!");
                    Console.ResetColor();
                }
                _population = BreedTournament(sorted, g);
                SaveFullPopulation();
            }
        }

        private void LoadCollectibleIds() => _collectibleCardIds = CardLibrary.Instance.GetAllIds().Where(id => id < 900).ToArray();

        private void LegalizeDeck(int[] deck, Random rng)
        {
            var counts = new Dictionary<int, int>();
            for (int i = 0; i < 30; i++)
            {
                int id = deck[i];
                if (id >= 900 || (counts.ContainsKey(id) && counts[id] >= 3))
                {
                    int replacement;
                    do { replacement = _collectibleCardIds[rng.Next(_collectibleCardIds.Length)]; }
                    while (counts.ContainsKey(replacement) && counts[replacement] >= 3);
                    deck[i] = replacement;
                    id = replacement;
                }
                if (!counts.ContainsKey(id)) counts[id] = 0;
                counts[id]++;
            }
        }

        private void InitializePopulation()
        {
            string path = "EvolutionData/current_population.json";
            if (File.Exists(path))
            {
                try
                {
                    var data = JsonSerializer.Deserialize<List<GeneticIndividual>>(File.ReadAllText(path));
                    if (data != null && data.Count > 0) { _population = data; return; }
                }
                catch { }
            }
            for (int i = 0; i < PopulationSize; i++)
            {
                float[] dna = new float[DNA.TOTAL_GENES];
                for (int j = 0; j < DNA.TOTAL_GENES; j++) dna[j] = (float)(_threadRng.Value.NextDouble() * 5.0);
                int[] deck = new int[30];
                for (int j = 0; j < 30; j++) deck[j] = _collectibleCardIds[_threadRng.Value.Next(_collectibleCardIds.Length)];
                LegalizeDeck(deck, _threadRng.Value);
                _population.Add(new GeneticIndividual(dna, deck, 0));
            }
        }

        private (int winner, float p1Score, float p2Score) SimulateFastMatchWithFitness(GeneticIndividual p1, GeneticIndividual p2, int depth)
        {
            var localRng = _threadRng.Value;
            var seed = localRng.Next();
            var rng = new DeterministicRng(seed);
            var factory = new CardGame.Core.Cards.Factories.CardFactory(CardLibrary.Instance, rng);
            var engine = new GameEngine(GameState.Initial(1, p1.DeckDNA.Select(id => factory.CreateCard(id, 1)).ToList(), p2.DeckDNA.Select(id => factory.CreateCard(id, 2)).ToList(), rng), seed);
            var s1 = new BotSolver(engine, 1, new EvolvableStrategy(p1.StrategyDNA), beamWidth: 3, maxDepth: depth);
            var s2 = new BotSolver(engine, 2, new EvolvableStrategy(p2.StrategyDNA), beamWidth: 3, maxDepth: depth);

            int moves = 0;
            int p1Fatigue = 0, p2Fatigue = 0;
            while (!engine.IsGameOver && moves++ < 500)
            {
                if (engine.CurrentState.PlayerA.DrawPile.Count == 0 && engine.CurrentState.PlayerA.Hand.Count == 0) p1Fatigue += 100;
                if (engine.CurrentState.PlayerB.DrawPile.Count == 0 && engine.CurrentState.PlayerB.Hand.Count == 0) p2Fatigue += 100;
                var solver = engine.CurrentState.ActivePlayerId == 1 ? s1 : s2;
                var best = solver.FindBestMoves(engine.CurrentState).FirstOrDefault();
                if (best != null) engine.ExecuteCommand(best.Command);
                else engine.ExecuteCommand(new EndPhaseCommand(engine.CurrentState.ActivePlayerId));
            }

            var s = engine.CurrentState;
            float p1Score = ((30 - s.PlayerB.Health) * 50) + (s.PlayerA.Health * 10) + (s.PlayerA.Hand.Count * 20) + (s.PlayerA.Hand.Sum(c => c.CostReduction) * 100) - p1Fatigue;
            float p2Score = ((30 - s.PlayerA.Health) * 50) + (s.PlayerB.Health * 10) + (s.PlayerB.Hand.Count * 20) + (s.PlayerB.Hand.Sum(c => c.CostReduction) * 100) - p2Fatigue;

            int winner = engine.WinnerId ?? (p1Score > p2Score ? 1 : 2);
            if (winner == 1) p1Score += (engine.WinnerId.HasValue ? 10000 : 3000);
            else p2Score += (engine.WinnerId.HasValue ? 10000 : 3000);

            return (winner, p1Score, p2Score);
        }

        private List<GeneticIndividual> BreedTournament(List<GeneticIndividual> sorted, int gen)
        {
            var next = new List<GeneticIndividual>();
            for (int i = 0; i < 5; i++) next.Add(sorted[i].Clone());
            while (next.Count < PopulationSize)
            {
                var pA = TournamentSelect(sorted, 3, _threadRng.Value);
                var pB = TournamentSelect(sorted, 3, _threadRng.Value);
                float[] dna = new float[DNA.TOTAL_GENES];
                for (int i = 0; i < DNA.TOTAL_GENES; i++) dna[i] = _threadRng.Value.NextDouble() > 0.5 ? GetSafeDNA(pA, i) : GetSafeDNA(pB, i);
                int[] deck = new int[30];
                for (int i = 0; i < 30; i++) deck[i] = _threadRng.Value.NextDouble() > 0.5 ? pA.DeckDNA[i] : pB.DeckDNA[i];
                if (_threadRng.Value.NextDouble() < 0.2) dna[_threadRng.Value.Next(DNA.TOTAL_GENES)] += (float)(_threadRng.Value.NextDouble() * 1.2 - 0.6);
                LegalizeDeck(deck, _threadRng.Value);
                next.Add(new GeneticIndividual(dna, deck, gen));
            }
            return next;
        }

        private float GetSafeDNA(GeneticIndividual bot, int index) => (index < bot.StrategyDNA.Length) ? bot.StrategyDNA[index] : 0f;

        private GeneticIndividual TournamentSelect(List<GeneticIndividual> pool, int size, Random rng)
        {
            GeneticIndividual best = null;
            for (int i = 0; i < size; i++)
            {
                var c = pool[rng.Next(pool.Count)];
                if (best == null || c.Fitness > best.Fitness) best = c;
            }
            return best;
        }

        private void ResetStats() { foreach (var i in _population) { i.Wins = 0; i.GamesPlayed = 0; i.Fitness = 0; } }
        private void SaveFullPopulation()
        {
            if (!Directory.Exists("EvolutionData")) Directory.CreateDirectory("EvolutionData");
            File.WriteAllText("EvolutionData/current_population.json", JsonSerializer.Serialize(_population));
        }
    }
}