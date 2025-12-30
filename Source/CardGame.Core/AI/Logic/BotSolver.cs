using CardGame.Core.AI.Interfaces;
using CardGame.Core.Application;
using CardGame.Core.Cards.Data;
using CardGame.Core.Commands.Implementations;
using CardGame.Core.Commands.Interfaces;
using CardGame.Core.State.Enums;
using CardGame.Core.State.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CardGame.Core.AI.Logic
{
    public class EvaluatedMove
    {
        public IGameCommand Command { get; }
        public float Score { get; }
        public string Description { get; }
        public string DeepReasoning { get; }

        public EvaluatedMove(IGameCommand command, float score, string description = "", string deepReasoning = "")
        {
            Command = command;
            Score = score;
            Description = description;
            DeepReasoning = deepReasoning;
        }
    }

    public class SimulationNode
    {
        public GameState State { get; }
        public IGameCommand? InitialMove { get; }
        public float Score { get; }

        public SimulationNode(GameState state, IGameCommand? initialMove, float score)
        {
            State = state;
            InitialMove = initialMove;
            Score = score;
        }
    }

    public class BotSolver
    {
        private readonly GameEngine _engineTemplate;
        private readonly MoveGenerator _moveGenerator;
        private readonly IAIStrategy _strategy;
        private readonly VirtualOpponent _virtualOpponent;
        private readonly int _botId;

        private const int BeamWidth = 5;
        private const int MaxDepth = 3;

        public BotSolver(GameEngine engine, int botId, IAIStrategy strategy)
        {
            _engineTemplate = engine;
            _botId = botId;
            _strategy = strategy;
            _moveGenerator = new MoveGenerator();
            _virtualOpponent = new VirtualOpponent(engine.Factory);
        }

        // G£ÓWNA METODA: Pobiera najlepsze ruchy z g³êbokim uzasadnieniem
        public List<EvaluatedMove> FindBestMoves(GameState rootState)
        {
            var legalMoves = _moveGenerator.GenerateLegalMoves(rootState, _botId);
            var results = new List<EvaluatedMove>();

            foreach (var move in legalMoves)
            {
                // Wywo³ujemy symulacjê, która zwraca parê (Wynik, Uzasadnienie)
                var sim = SimulateMoveWithReasoning(rootState, move);
                string description = GenerateDescription(move, rootState);

                results.Add(new EvaluatedMove(move, sim.Score, description, sim.Reasoning));
            }

            return results.OrderByDescending(r => r.Score).ToList();
        }

        private (float Score, string Reasoning) SimulateMoveWithReasoning(GameState rootState, IGameCommand move)
        {
            var simulator = new GameEngine(rootState, _engineTemplate.Rng.Seed);
            var result = simulator.ExecuteCommand(move);

            // Kara za ruchy "puste" (zablokowane przez silnik)
            if (result.NewState == rootState && !(move is EndPhaseCommand))
                return (-1000000f, "Ruch nielegalny/zablokowany");

            var stateAfterAction = result.NewState;

            // Projekcja walki: Jeœli ruch koñczy fazê, symulujemy Combat do skutku
            if (stateAfterAction.CurrentPhase == GamePhase.Combat)
            {
                var combatResult = simulator.ExecuteCommand(new EndPhaseCommand(stateAfterAction.ActivePlayerId));
                stateAfterAction = combatResult.NewState;
            }

            // --- Inicjalizacja Beam Search ---
            var beam = new List<SimulationNode> {
                new SimulationNode(stateAfterAction, move, _strategy.Evaluate(stateAfterAction, _botId))
            };

            for (int depth = 1; depth < MaxDepth; depth++)
            {
                var nextCandidates = new List<SimulationNode>();
                foreach (var node in beam)
                {
                    bool isBotTurn = node.State.ActivePlayerId == _botId;

                    // Minimax: Szukamy najlepszego dla nas lub zak³adamy najgorsze od przeciwnika
                    var outcome = isBotTurn
                        ? SimulateAndFindBestOutcomeForBot(node.State)
                        : SimulateAndFindWorstOutcomeForBot(node.State);

                    nextCandidates.Add(new SimulationNode(outcome.State, node.InitialMove, outcome.Score));
                }

                if (!nextCandidates.Any()) break;
                beam = nextCandidates.OrderByDescending(n => n.Score).Take(BeamWidth).ToList();
            }

            var bestNode = beam.OrderByDescending(n => n.Score).FirstOrDefault();
            if (bestNode == null) return (_strategy.Evaluate(stateAfterAction, _botId), "Analiza powierzchowna");

            // Generowanie opisu zysków na podstawie porównania stanu startowego i koñcowego symulacji
            string deepReasoning = GenerateDeepReasoning(rootState, bestNode.State);

            return (bestNode.Score, deepReasoning);
        }

        private string GenerateDeepReasoning(GameState start, GameState end)
        {
            var pStart = start.GetPlayer(_botId);
            var pEnd = end.GetPlayer(_botId);
            var eStart = start.GetOpponent(_botId);
            var eEnd = end.GetOpponent(_botId);

            List<string> gains = new List<string>();

            // Bilans kart
            int cardDiff = pEnd.Hand.Count - pStart.Hand.Count;
            if (cardDiff > 0) gains.Add($"+{cardDiff} karty");
            else if (cardDiff < 0) gains.Add($"{cardDiff} kart");

            // Bilans HP wrogiego bohatera
            int dmgDealt = eStart.Health - eEnd.Health;
            if (dmgDealt > 0) gains.Add($"{dmgDealt} dmg w wroga");

            // Bilans jednostek
            int myUnitsEnd = end.Board.GetAllUnits().Count(u => u.OwnerPlayerId == _botId);
            int myUnitsStart = start.Board.GetAllUnits().Count(u => u.OwnerPlayerId == _botId);
            int enUnitsEnd = end.Board.GetAllUnits().Count(u => u.OwnerPlayerId != _botId);
            int enUnitsStart = start.Board.GetAllUnits().Count(u => u.OwnerPlayerId != _botId);

            int myDiff = myUnitsEnd - myUnitsStart;
            int enDiff = enUnitsStart - enUnitsEnd;

            if (myDiff != 0) gains.Add(myDiff > 0 ? $"+{myDiff} jedn." : $"{myDiff} jedn.");
            if (enDiff > 0) gains.Add($"Zabite: {enDiff}");

            // Marki
            int marksEnd = end.Board.GetAllUnits().Count(u => u.OwnerPlayerId != _botId && u.CurrentStats.Keywords.Contains(Keyword.Marked));
            if (marksEnd > 0) gains.Add($"Marki: {marksEnd}");

            if (gains.Count == 0) return "Stabilizacja pozycji";
            return "WIZJA: " + string.Join(", ", gains);
        }

        private (GameState State, float Score) SimulateAndFindBestOutcomeForBot(GameState currentState)
        {
            var moves = _moveGenerator.GenerateLegalMoves(currentState, _botId);
            float bestScore = float.MinValue;
            GameState bestState = currentState;

            foreach (var move in moves.Take(5)) // Top 5 dla wydajnoœci
            {
                var simulator = new GameEngine(currentState, _engineTemplate.Rng.Seed);
                var result = simulator.ExecuteCommand(move);
                if (result.NewState == currentState && !(move is EndPhaseCommand)) continue;

                float score = _strategy.Evaluate(result.NewState, _botId);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestState = result.NewState;
                }
            }
            return (bestState, bestScore == float.MinValue ? _strategy.Evaluate(currentState, _botId) : bestScore);
        }

        private (GameState State, float Score) SimulateAndFindWorstOutcomeForBot(GameState currentState)
        {
            var activePlayerId = currentState.ActivePlayerId;
            var stateWithPhantomHand = _virtualOpponent.InjectPhantomHand(currentState, activePlayerId);
            var enemyMoves = _moveGenerator.GenerateLegalMoves(stateWithPhantomHand, activePlayerId);

            float worstScoreForBot = float.MaxValue;
            GameState worstStateForBot = currentState;

            bool moveFound = false;
            foreach (var enemyMove in enemyMoves.Take(5))
            {
                var simulator = new GameEngine(stateWithPhantomHand, _engineTemplate.Rng.Seed);
                var result = simulator.ExecuteCommand(enemyMove);

                if (result.NewState == stateWithPhantomHand && !(enemyMove is EndPhaseCommand))
                    continue;

                moveFound = true;
                float currentScore = _strategy.Evaluate(result.NewState, _botId);
                if (currentScore < worstScoreForBot)
                {
                    worstScoreForBot = currentScore;
                    worstStateForBot = result.NewState;
                }
            }

            if (!moveFound)
                worstScoreForBot = _strategy.Evaluate(currentState, _botId);

            return (worstStateForBot, worstScoreForBot);
        }

        private string GenerateDescription(IGameCommand move, GameState state)
        {
            if (move is PlayUnitCommand pu)
            {
                var card = state.GetPlayer(_botId).Hand.FirstOrDefault(c => c.InstanceId == pu.CardInstanceId);
                if (card == null) return "Wystawienie jednostki";
                if (card.Definition.Id == "4") return "MARK: Namierzenie";
                if (card.Definition.Id == "12") return "SACR: Paliwo (Cat)";
                if (card.CurrentStats.Keywords.Contains(Keyword.SplashDamage)) return "POS: Splash-Value";
                return $"Graj {card.Definition.Name}";
            }
            if (move is PlaySpellCommand ps)
            {
                var card = state.GetPlayer(_botId).Hand.FirstOrDefault(c => c.InstanceId == ps.CardInstanceId);
                return $"CZAR: {card?.Definition.Name ?? "Magia"}";
            }
            if (move is SelectTargetCommand) return "INTERAKCJA: Celowanie";
            if (move is EndPhaseCommand) return "PAS: Koniec fazy";

            return move.GetType().Name;
        }
    }
}