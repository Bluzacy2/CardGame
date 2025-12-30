using CardGame.Core.AI.Interfaces;
using CardGame.Core.Application;
using CardGame.Core.Commands.Implementations;
using CardGame.Core.Commands.Interfaces;
using CardGame.Core.State.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CardGame.Core.AI.Logic
{
    // NEW CLASS: Represents a move that has been evaluated by the AI.
    public class EvaluatedMove
    {
        public IGameCommand Command { get; }
        public float Score { get; }
        public string Description { get; }

        public EvaluatedMove(IGameCommand command, float score, string description = "")
        {
            Command = command;
            Score = score;
            Description = description;
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

        // MODIFIED: This method now returns a list of all considered moves with their scores.
        public List<EvaluatedMove> FindBestMoves(GameState rootState)
        {
            var initialMoves = _moveGenerator.GenerateLegalMoves(rootState, _botId);
            var evaluatedMoves = new List<EvaluatedMove>();

            foreach (var move in initialMoves)
            {
                float moveScore = SimulateMove(rootState, move);
                evaluatedMoves.Add(new EvaluatedMove(move, moveScore));
            }
            
            // Return all evaluated moves, sorted from best to worst.
            return evaluatedMoves.OrderByDescending(m => m.Score).ToList();
        }

        private float SimulateMove(GameState rootState, IGameCommand move)
        {
            var beam = new List<SimulationNode>();
            var simulator = new GameEngine(rootState, _engineTemplate.Rng.Seed);
            var result = simulator.ExecuteCommand(move);

            // If the move is invalid or leads to no state change, penalize it heavily.
            if (result.NewState == rootState && !(move is EndPhaseCommand)) 
            {
                 return float.MinValue;
            }

            // For EndPhase, we must simulate the opponent's best response.
            if (move is EndPhaseCommand)
            {
                var outcome = SimulateAndFindWorstOutcomeForBot(result.NewState);
                return outcome.Score;
            }

            beam.Add(new SimulationNode(result.NewState, move, _strategy.Evaluate(result.NewState, _botId)));

            for (int depth = 1; depth < MaxDepth; depth++)
            {
                var nextCandidates = new List<SimulationNode>();
                foreach (var node in beam)
                {
                    bool isBotTurn = node.State.ActivePlayerId == _botId;
                    var outcome = isBotTurn
                        ? SimulateAndFindBestOutcomeForBot(node.State)
                        : SimulateAndFindWorstOutcomeForBot(node.State);
                    
                    nextCandidates.Add(new SimulationNode(outcome.State, node.InitialMove, outcome.Score));
                }
                beam = nextCandidates.OrderByDescending(n => n.Score).Take(BeamWidth).ToList();
                if (!beam.Any()) break;
            }

            return beam.Any() ? beam.Average(n => n.Score) : _strategy.Evaluate(result.NewState, _botId);
        }

        private (GameState State, float Score) SimulateAndFindBestOutcomeForBot(GameState currentState)
        {
            var moves = _moveGenerator.GenerateLegalMoves(currentState, _botId);
            float bestScore = float.MinValue;
            GameState bestState = currentState;

            foreach (var move in moves)
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

            // Increased simulation width for opponent's turn to be more cautious
            foreach (var enemyMove in enemyMoves.OrderByDescending(m => _strategy.Evaluate(new GameEngine(stateWithPhantomHand, _engineTemplate.Rng.Seed).ExecuteCommand(m).NewState, 3- _botId)).Take(7))
            {
                var simulator = new GameEngine(stateWithPhantomHand, _engineTemplate.Rng.Seed);
                var result = simulator.ExecuteCommand(enemyMove);
                
                GameState resultingState = result.NewState;

                if (enemyMove is EndPhaseCommand)
                {
                    resultingState = resultingState.With(activePlayerId: _botId);
                }

                if (resultingState == stateWithPhantomHand && !(enemyMove is EndPhaseCommand))
                    continue;

                moveFound = true;
                float currentScore = _strategy.Evaluate(resultingState, _botId);
                if (currentScore < worstScoreForBot)
                {
                    worstScoreForBot = currentScore;
                    worstStateForBot = resultingState;
                }
            }

            if (!moveFound)
            {
                worstScoreForBot = _strategy.Evaluate(currentState.With(activePlayerId: _botId), _botId);
            }

            return (worstStateForBot, worstScoreForBot);
        }
    }
}