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

        // Parametry Beam Search (zmienione z const na pola)
        private readonly int _beamWidth;
        private readonly int _maxDepth;

        public BotSolver(GameEngine engine, int botId, IAIStrategy strategy, int beamWidth = 4, int maxDepth = 5)
        {
            _engineTemplate = engine;
            _botId = botId;
            _strategy = strategy;
            _moveGenerator = new MoveGenerator();
            _virtualOpponent = new VirtualOpponent(engine.Factory);
            _beamWidth = beamWidth;
            _maxDepth = maxDepth;
        }

        public List<EvaluatedMove> FindBestMoves(GameState rootState)
        {
            var legalMoves = _moveGenerator.GenerateLegalMoves(rootState, _botId);
            var results = new List<EvaluatedMove>();

            foreach (var move in legalMoves)
            {
                var sim = SimulateRoundWithReasoning(rootState, move);
                string desc = GenerateDescription(move, rootState);
                results.Add(new EvaluatedMove(move, sim.Score, desc, sim.Reasoning));
            }

            return results.OrderByDescending(r => r.Score).ToList();
        }

        private (float Score, string Reasoning) SimulateRoundWithReasoning(GameState rootState, IGameCommand firstMove)
        {
            var simulator = new GameEngine(rootState, _engineTemplate.Rng.Seed);
            GameState state = ExecuteFullAction(simulator, firstMove);

            if (state == rootState && !(firstMove is EndPhaseCommand))
                return (-3000000f, "Ruch nielegalny");

            var beam = new List<SimulationNode> {
                new SimulationNode(state, firstMove, _strategy.Evaluate(state, _botId))
            };

            for (int d = 1; d < _maxDepth; d++)
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

                if (!nextCandidates.Any()) break;
                beam = nextCandidates.OrderByDescending(n => n.Score).Take(_beamWidth).ToList();
            }

            var best = beam.OrderByDescending(n => n.Score).First();
            GameState finalState = best.State;

            if (finalState.CurrentPhase != GamePhase.Combat && finalState.TurnNumber == rootState.TurnNumber)
            {
                var endSim = new GameEngine(finalState, _engineTemplate.Rng.Seed);
                finalState = endSim.ExecuteCommand(new EndPhaseCommand(finalState.ActivePlayerId)).NewState;
            }

            return (best.Score, GenerateDeepReasoning(rootState, finalState));
        }

        private (GameState State, float Score) SimulateAndFindBestOutcomeForBot(GameState currentState)
        {
            var moves = _moveGenerator.GenerateLegalMoves(currentState, _botId);
            float bestScore = float.MinValue;
            GameState bestState = currentState;

            foreach (var move in moves.Take(6))
            {
                var sim = new GameEngine(currentState, _engineTemplate.Rng.Seed);
                var resState = ExecuteFullAction(sim, move);
                float score = _strategy.Evaluate(resState, _botId);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestState = resState;
                }
            }
            return (bestState, bestScore);
        }

        private (GameState State, float Score) SimulateAndFindWorstOutcomeForBot(GameState currentState)
        {
            var oppId = currentState.ActivePlayerId;
            var stateWithPhantom = _virtualOpponent.InjectRealisticPhantomHand(currentState, oppId);
            var enemyMoves = _moveGenerator.GenerateLegalMoves(stateWithPhantom, oppId);

            float worstForBot = float.MaxValue;
            GameState worstState = currentState;

            foreach (var move in enemyMoves.Take(6))
            {
                var sim = new GameEngine(stateWithPhantom, _engineTemplate.Rng.Seed);
                var resState = ExecuteFullAction(sim, move);
                float score = _strategy.Evaluate(resState, _botId);
                if (score < worstForBot)
                {
                    worstForBot = score;
                    worstState = resState;
                }
            }
            return (worstState, worstForBot);
        }

        private GameState ExecuteFullAction(GameEngine engine, IGameCommand cmd)
        {
            var result = engine.ExecuteCommand(cmd);
            var state = result.NewState;

            int safety = 0;
            // ZMNIEJSZONO LIMIT: max 8 interakcji na akcjê (np. wybór celu), by ubiæ pêtle 0-cost
            while (state.PendingInteraction != null && safety++ < 8)
            {
                var targets = _moveGenerator.GenerateLegalMoves(state, _botId);
                if (!targets.Any())
                {
                    state = state.With(clearPending: true);
                    break;
                }

                IGameCommand bestT = targets.First();
                float bestV = float.MinValue;

                // Ograniczamy liczbê rozwa¿anych celów dla wydajnoœci
                foreach (var t in targets.Take(4))
                {
                    var testEngine = new GameEngine(state, _engineTemplate.Rng.Seed);
                    var testState = testEngine.ExecuteCommand(t).NewState;
                    float v = _strategy.Evaluate(testState, _botId);
                    if (v > bestV) { bestV = v; bestT = t; }
                }
                state = engine.ExecuteCommand(bestT).NewState;
            }
            return state;
        }

        private string GenerateDeepReasoning(GameState start, GameState end)
        {
            var pS = start.GetPlayer(_botId); var pE = end.GetPlayer(_botId);
            var eS = start.GetOpponent(_botId); var eE = end.GetOpponent(_botId);
            if (eE.Health <= 0) return "!!! LETHAL IN SIGHT !!!";
            List<string> r = new List<string>();
            int cD = pE.Hand.Count - pS.Hand.Count;
            if (cD != 0) r.Add($"Cards:{(cD > 0 ? "+" : "")}{cD}");
            int hD = eS.Health - eE.Health;
            if (hD > 0) r.Add($"EnemyDmg:{hD}");
            if (pE.CurrentBlood > 0 && start.TurnNumber == end.TurnNumber) r.Add($"Save:{pE.CurrentBlood}B");
            int kills = start.Board.GetAllUnits().Count(u => u.OwnerPlayerId != _botId) -
                        end.Board.GetAllUnits().Count(u => u.OwnerPlayerId != _botId);
            if (kills > 0) r.Add($"Kills:{kills}");
            return r.Count == 0 ? "Hold Position" : "WIZJA: " + string.Join(", ", r);
        }

        private string GenerateDescription(IGameCommand move, GameState state)
        {
            if (move is PlayUnitCommand pu)
            {
                var card = state.GetPlayer(_botId).Hand.FirstOrDefault(c => c.InstanceId == pu.CardInstanceId);
                return $"Play {card?.Definition.Name ?? "Unit"}";
            }
            if (move is PlaySpellCommand ps)
            {
                var card = state.GetPlayer(_botId).Hand.FirstOrDefault(c => c.InstanceId == ps.CardInstanceId);
                return $"Cast {card?.Definition.Name ?? "Spell"}";
            }
            return move is EndPhaseCommand ? "End Turn" : move.GetType().Name;
        }
    }
}