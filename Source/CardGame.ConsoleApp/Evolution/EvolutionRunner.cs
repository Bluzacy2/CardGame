using CardGame.Core.AI.Interfaces;
using CardGame.Core.AI.Logic;
using CardGame.Core.AI.Logic.Mcts;
using CardGame.Core.AI.Strategies;
using CardGame.Core.Application;
using CardGame.Core.Cards.Data;
using CardGame.Core.Commands.Implementations;
using CardGame.Core.State.Enums;
using CardGame.Core.State.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CardGame.ConsoleApp.Evolution
{
    public class EvolutionRunner
    {
        private List<GeneticIndividual> _population = new();
        private readonly HallOfFame _hof = new();
        private const int PopulationSize = 100;
        private static readonly ThreadLocal<Random> _threadRng = new(() => new Random());
        private int[] _collectibleCardIds;
        private int _stagnationTimer = 0;
        private float _masterDecayFactor = 1.0f;
        private Dictionary<int, float> _usageCache = new();

        public async Task RunEvolutionAsync()
        {
            Console.Clear();
            CardLibrary.Instance.LoadFromJson("Data/Cards/cards.json");
            LoadCollectibleIds();
            InitializePopulation();
            _hof.Load();

            string bestBotPath = "best_bot_dna.json";
            float highestFitnessEver = -1000000;
            GeneticIndividual currentMaster = null;

            if (File.Exists(bestBotPath))
            {
                try
                {
                    currentMaster = JsonSerializer.Deserialize<GeneticIndividual>(File.ReadAllText(bestBotPath));
                    highestFitnessEver = currentMaster.Fitness;
                }
                catch { }
            }

            var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };
            int g = 1;
            bool shouldStop = false;

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("=== EVOLUTION V16.4 (PRESTIGE ELITE - QUALITY FOCUS) ===");
            Console.WriteLine("Mode: High-Rigor MCTS Exam & Genetic Polishing");
            Console.ResetColor();

            while (!shouldStop)
            {
                UpdateUsageCache();
                ResetStats();

                // Wolniejszy Master Decay (0.01 zamiast 0.05)
                if (_stagnationTimer > 10) _masterDecayFactor -= 0.01f;

                // 1. FAZA: MECZE W POPULACJI
                Parallel.For(0, _population.Count, parallelOptions, i =>
                {
                    var localRng = _threadRng.Value;
                    var subject = _population[i];
                    var champions = _hof.Champions.Where(c => c != null).ToList();

                    for (int m = 0; m < 6; m++)
                    {
                        GeneticIndividual opponent = (m < 2 && champions.Count > 0)
                            ? champions[localRng.Next(champions.Count)]
                            : _population[localRng.Next(PopulationSize)];

                        if (subject.Id == opponent.Id) continue;

                        int startId = (m % 2 == 0) ? 1 : 2;
                        var result = SimulateRobustMatch(subject, opponent, 4, 3, startId);

                        lock (subject)
                        {
                            subject.GamesPlayed++;
                            float score = (startId == 1) ? result.p1Score : result.p2Score;

                            // Surowsza kara za bycie kopią (Diversity promuje unikalne mózgi)
                            if (champions.Count > 0)
                            {
                                float sim = CalculateSimilarity(subject, champions[0]);
                                score *= (1.0f - (Math.Max(0, sim - 0.4f) * 0.6f));
                            }

                            subject.Fitness += score;
                            if ((startId == 1 && result.winner == 1) || (startId == 2 && result.winner == 2))
                                subject.Wins++;
                        }
                    }
                });

                // 2. FAZA: RYGORYSTYCZNY EGZAMIN MCTS (TOP 12)
                var topTier = _population.OrderByDescending(x => x.Fitness).Take(12).ToList();
                Console.WriteLine($" >> [GEN {g:D3}] MCTS Pentakill Exam (5 trials/bot)...");

                Parallel.ForEach(topTier, parallelOptions, (elite) =>
                {
                    int mctsTrials = 5; // Więcej prób = wyższa wiarygodność
                    float totalMctsScore = 0;
                    int mctsWins = 0;

                    for (int t = 0; t < mctsTrials; t++)
                    {
                        var res = RunCoEvolutionaryMctsTest(elite, currentMaster, 160);
                        totalMctsScore += res.score;
                        if (res.won) mctsWins++;
                    }

                    lock (elite)
                    {
                        elite.Fitness += (totalMctsScore / mctsTrials) * 6.0f;
                        if (mctsWins > 0) elite.Fitness += (mctsWins * 40000f);
                        if (mctsWins == mctsTrials) elite.Fitness += 150000f; // Bonus za wybitną stabilność
                    }
                });

                var sorted = _population.OrderByDescending(x => x.Fitness).ToList();
                _hof.ProcessPopulation(sorted, g);
                var best = sorted[0];

                Console.WriteLine($"[GEN {g:D3}] Master:{best.Id} | Fit:{best.Fitness,8:F0} | WR:{(best.WinRate * 100),4:F1}% | Stag:{_stagnationTimer} | Decay:{_masterDecayFactor:P0}");

                // Wyższy próg ewolucyjny (+5000 pkt)
                if (best.Fitness > (highestFitnessEver * _masterDecayFactor) + 5000)
                {
                    highestFitnessEver = best.Fitness;
                    _stagnationTimer = 0;
                    _masterDecayFactor = 1.0f;
                    currentMaster = best.Clone();
                    File.WriteAllText(bestBotPath, JsonSerializer.Serialize(best, new JsonSerializerOptions { WriteIndented = true }));
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($" >>> NEW ELITE MASTER: {best.Id} (Fitness Jump) <<< ");
                    Console.ResetColor();
                }
                else _stagnationTimer++;

                if (g % 10 == 0) CardAnalytics.ProcessAndSave(sorted, g);
                _population = BreedTournament(sorted, g, _stagnationTimer);
                SaveFullPopulation();

                await Task.Delay(50);
                while (Console.KeyAvailable) { if (Console.ReadKey(true).Key == ConsoleKey.Enter) shouldStop = true; }
                g++;
            }
        }

        private (int winner, float p1Score, float p2Score) SimulateRobustMatch(GeneticIndividual b1, GeneticIndividual b2, int beam, int depth, int startId)
        {
            var seed = _threadRng.Value.Next();
            var rng = new DeterministicRng(seed);
            var factory = new CardGame.Core.Cards.Factories.CardFactory(CardLibrary.Instance, rng);
            var engine = new GameEngine(GameState.Initial(startId,
                b1.DeckDNA.Select(id => factory.CreateCard(id, 1)).ToList(),
                b2.DeckDNA.Select(id => factory.CreateCard(id, 2)).ToList(), rng), seed);

            var s1 = new BotSolver(engine, 1, new EvolvableStrategy(b1.StrategyDNA), beam, depth);
            var s2 = new BotSolver(engine, 2, new EvolvableStrategy(b2.StrategyDNA), beam, depth);

            int moves = 0;
            // Dociąg i Mulligan
            while (engine.CurrentState.CurrentPhase == GamePhase.Mulligan && moves++ < 10)
            {
                if (!engine.CurrentState.PlayersReady.Contains(1)) engine.ExecuteCommand(new ConfirmMulliganCommand(1, new()));
                if (!engine.CurrentState.PlayersReady.Contains(2)) engine.ExecuteCommand(new ConfirmMulliganCommand(2, new()));
            }

            int lastTurn = -1; GamePhase lastPhase = GamePhase.None; int stagnantMoves = 0;

            while (!engine.IsGameOver && moves++ < 250)
            {
                var cur = engine.CurrentState;
                if (cur.TurnNumber == lastTurn && cur.CurrentPhase == lastPhase) stagnantMoves++;
                else { stagnantMoves = 0; lastTurn = cur.TurnNumber; lastPhase = cur.CurrentPhase; }
                if (stagnantMoves > 12) break;

                var solver = cur.ActivePlayerId == 1 ? s1 : s2;
                var best = solver.FindBestMoves(cur).FirstOrDefault();
                if (best?.Command != null) engine.ExecuteCommand(best.Command);
                else engine.ExecuteCommand(new EndPhaseCommand(cur.ActivePlayerId));
            }

            var fs = engine.CurrentState;
            float Calc(PlayerState p, PlayerState o, BoardState b) =>
                (30 - o.Health) * 220f + p.Health * 70f +
                b.GetAllUnits().Where(u => u.OwnerPlayerId == p.PlayerId).Sum(u => u.CurrentStats.Attack * 35f + u.CurrentStats.Health * 25f) +
                p.Hand.Count * 100f;

            float p1S = Calc(fs.PlayerA, fs.PlayerB, fs.Board);
            float p2S = Calc(fs.PlayerB, fs.PlayerA, fs.Board);
            if (engine.WinnerId == 1) p1S += 60000f; else if (engine.WinnerId == 2) p2S += 60000f;

            return (engine.WinnerId ?? 0, p1S, p2S);
        }

        private (bool won, float score) RunCoEvolutionaryMctsTest(GeneticIndividual b, GeneticIndividual master, int time)
        {
            var seed = _threadRng.Value.Next();
            var rng = new DeterministicRng(seed);
            var factory = new CardGame.Core.Cards.Factories.CardFactory(CardLibrary.Instance, rng);
            var engine = new GameEngine(GameState.Initial(1,
                b.DeckDNA.Select(id => factory.CreateCard(id, 1)).ToList(),
                b.DeckDNA.Select(id => factory.CreateCard(id, 2)).ToList(), rng), seed);

            while (engine.CurrentState.CurrentPhase == GamePhase.Mulligan)
            {
                engine.ExecuteCommand(new ConfirmMulliganCommand(1, new()));
                engine.ExecuteCommand(new ConfirmMulliganCommand(2, new()));
            }

            var s1 = new BotSolver(engine, 1, new EvolvableStrategy(b.StrategyDNA), 4, 4);
            IAIStrategy masterHeuristic = (master != null) ? new EvolvableStrategy(master.StrategyDNA) : new StandardStrategy();
            var s2 = new MctsSolver(engine, 2, masterHeuristic);

            int moves = 0;
            while (!engine.IsGameOver && moves++ < 150)
            {
                var cur = engine.CurrentState;
                if (cur.ActivePlayerId == 1)
                {
                    var best = s1.FindBestMoves(cur).FirstOrDefault();
                    if (best != null) engine.ExecuteCommand(best.Command);
                    else engine.ExecuteCommand(new EndPhaseCommand(1));
                }
                else engine.ExecuteCommand(s2.FindBestMove(cur, time).Command);
            }
            float fit = (30 - engine.CurrentState.PlayerB.Health) * 350f + engine.CurrentState.PlayerA.Health * 120f;
            return (engine.WinnerId == 1, fit);
        }

        private List<GeneticIndividual> BreedTournament(List<GeneticIndividual> sorted, int gen, int stag)
        {
            var next = new List<GeneticIndividual>();
            var localRng = _threadRng.Value;

            // ELITYZM (Więcej elit, by szlifować sukcesy)
            int eliteCount = (stag > 8) ? 2 : 6;
            for (int i = 0; i < eliteCount; i++) next.Add(sorted[i].Clone());

            // IMIGRANCI (Zawsze potrzebna świeża krew)
            int immigrants = (stag > 5) ? 25 : 10;
            while (next.Count < eliteCount + immigrants) next.Add(CreateRandomIndividual(gen));

            while (next.Count < PopulationSize)
            {
                var pA = TournamentSelect(sorted, 10, localRng);
                var pB = TournamentSelect(sorted, 10, localRng);
                float[] dna = new float[DNA.TOTAL_GENES];
                float weight = (float)localRng.NextDouble();

                for (int i = 0; i < DNA.TOTAL_GENES; i++)
                {
                    double roll = localRng.NextDouble();
                    if (roll < 0.03) // Mutacja ekstremalna (Rzadziej niż wcześniej)
                        dna[i] = localRng.Next(0, 2) == 0 ? 0.3f : 9.7f;
                    else if (roll < 0.25) // NOWOŚĆ: Mikro-tuning (Szlifowanie wartości)
                        dna[i] = Math.Clamp((pA.StrategyDNA[i] * weight) + (pB.StrategyDNA[i] * (1f - weight)) + (float)(localRng.NextDouble() * 0.4 - 0.2), 0, 10);
                    else // Standardowy Crossover
                        dna[i] = Math.Clamp((pA.StrategyDNA[i] * weight) + (pB.StrategyDNA[i] * (1f - weight)), 0, 10);
                }

                // Chunk Crossover dla Decku
                int[] childDeck = new int[30];
                int split = localRng.Next(5, 25);
                for (int i = 0; i < 30; i++) childDeck[i] = (i < split) ? pA.DeckDNA[i] : pB.DeckDNA[i];

                // Mutacja Decku (rzadsza, by nie psuć synergii)
                if (localRng.NextDouble() < (stag > 6 ? 0.5 : 0.15))
                {
                    int changes = (stag > 8) ? 8 : 2;
                    for (int j = 0; j < changes; j++) childDeck[localRng.Next(30)] = _collectibleCardIds[localRng.Next(_collectibleCardIds.Length)];
                }
                LegalizeDeck(childDeck, localRng);
                next.Add(new GeneticIndividual(dna, childDeck, gen));
            }
            return next;
        }

        private void UpdateUsageCache()
        {
            var allCards = _population.SelectMany(p => p.DeckDNA).ToList();
            if (!allCards.Any()) return;
            int total = allCards.Count;
            _usageCache = allCards.GroupBy(id => id).ToDictionary(g => g.Key, g => (float)g.Count() / total);
        }

        private float CalculateSimilarity(GeneticIndividual a, GeneticIndividual b)
        {
            var d1 = a.DeckDNA.OrderBy(x => x).ToArray(); var d2 = b.DeckDNA.OrderBy(x => x).ToArray();
            int shared = 0; for (int i = 0; i < 30; i++) if (d1[i] == d2[i]) shared++;
            return shared / 30f;
        }

        private GeneticIndividual CreateRandomIndividual(int gen)
        {
            float[] dna = new float[DNA.TOTAL_GENES];
            for (int j = 0; j < DNA.TOTAL_GENES; j++) dna[j] = (float)(_threadRng.Value.NextDouble() * 10.0);
            int[] deck = new int[30];
            for (int j = 0; j < 30; j++) deck[j] = _collectibleCardIds[_threadRng.Value.Next(_collectibleCardIds.Length)];
            LegalizeDeck(deck, _threadRng.Value);
            return new GeneticIndividual(dna, deck, gen);
        }

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

        private void InitializePopulation()
        {
            string path = "EvolutionData/current_population.json";
            if (File.Exists(path))
            {
                try
                {
                    var data = JsonSerializer.Deserialize<List<GeneticIndividual>>(File.ReadAllText(path));
                    if (data != null && data.Count == PopulationSize) { _population = data; return; }
                }
                catch { }
            }
            _population.Clear();
            for (int i = 0; i < PopulationSize; i++) _population.Add(CreateRandomIndividual(0));
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
                    int r; do { r = _collectibleCardIds[rng.Next(_collectibleCardIds.Length)]; } while (counts.ContainsKey(r) && counts[r] >= 3);
                    deck[i] = id = r;
                }
                if (!counts.ContainsKey(id)) counts[id] = 0; counts[id]++;
            }
        }

        private void ResetStats() { foreach (var i in _population) { i.Wins = 0; i.GamesPlayed = 0; i.Fitness = 0; } }
        private void SaveFullPopulation()
        {
            if (!Directory.Exists("EvolutionData")) Directory.CreateDirectory("EvolutionData");
            File.WriteAllText("EvolutionData/current_population.json", JsonSerializer.Serialize(_population));
        }
    }
}