using CardGame.Core.AI.Interfaces;
using CardGame.Core.Application;
using CardGame.Core.Cards.Data;
using CardGame.Core.Commands.Implementations;
using CardGame.Core.Commands.Interfaces;
using CardGame.Core.State.Enums;
using CardGame.Core.State.Models;
using System;
using System.Collections.Generic;

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

    // Zmieniona na mutowaln¹ klasê dla Node Pool
    public class SimulationNode
    {
        public GameState State { get; set; }
        public IGameCommand? InitialMove { get; set; }
        public float Score { get; set; }

        public SimulationNode() { }

        public SimulationNode(GameState state, IGameCommand? initialMove, float score)
        {
            State = state;
            InitialMove = initialMove;
            Score = score;
        }

        public void Set(GameState state, IGameCommand? initialMove, float score)
        {
            State = state;
            InitialMove = initialMove;
            Score = score;
        }
    }

    public class BotSolver
    {
        public static bool IsEvolving = true;
        private readonly GameEngine _engineTemplate;
        private readonly MoveGenerator _moveGenerator;
        private readonly IAIStrategy _strategy;
        private readonly VirtualOpponent _virtualOpponent;
        private readonly int _botId;
        private readonly int _beamWidth;
        private readonly int _maxDepth;

        // Optimized evolving parameters
        private readonly int _evolvingBeamWidth;
        private readonly int _evolvingMaxDepth;
        private readonly int _evolvingMoveLimit;

        // Pool for reusing SimulationNode objects
        private readonly List<SimulationNode> _nodePool = new();
        private int _nodePoolIndex = 0;

        private readonly SimulationNode[] _beamBuffer;
        private readonly SimulationNode[] _candidateBuffer;
        public BotSolver(GameEngine engine, int botId, IAIStrategy strategy, int beamWidth = 4, int maxDepth = 5)
        {
            _engineTemplate = engine;
            _botId = botId;
            _strategy = strategy;
            _moveGenerator = new MoveGenerator();
            _virtualOpponent = new VirtualOpponent(engine.Factory);
            _beamWidth = beamWidth;
            _maxDepth = maxDepth;

            // Optimized parameters for evolving mode
            _evolvingBeamWidth = 2;
            _evolvingMaxDepth = 3;
            _evolvingMoveLimit = 3;

            _beamBuffer = new SimulationNode[_evolvingBeamWidth];
            _candidateBuffer = new SimulationNode[_evolvingBeamWidth * _evolvingMoveLimit];
        }

        public List<EvaluatedMove> FindBestMoves(GameState rootState)
        {
            var legalMoves = _moveGenerator.GenerateLegalMoves(rootState, _botId);
            var results = new List<EvaluatedMove>(legalMoves.Count);

            if (IsEvolving)
            {
                // Fast path for evolving - no string allocations
                for (int i = 0; i < legalMoves.Count; i++)
                {
                    var move = legalMoves[i];
                    var score = SimulateRoundFast(rootState, move);
                    results.Add(new EvaluatedMove(move, score, "", ""));
                }
            }
            else
            {
                // Full evaluation for non-evolving
                for (int i = 0; i < legalMoves.Count; i++)
                {
                    var move = legalMoves[i];
                    var sim = SimulateRoundWithReasoning(rootState, move);
                    string desc = GenerateDescription(move, rootState);
                    results.Add(new EvaluatedMove(move, sim.Score, desc, sim.Reasoning));
                }
            }

            // Optimized sorting
            if (results.Count > 1)
            {
                if (results.Count <= 10)
                {
                    InsertionSortDescending(results);
                }
                else
                {
                    results.Sort((a, b) => b.Score.CompareTo(a.Score));
                }
            }

            return results;
        }

        private float SimulateRoundFast(GameState rootState, IGameCommand firstMove)
        {
            // UWAGA: Konstruktor GameEngine nadal muli - patrz ni¿ej sekcja 2!
            var simulator = new GameEngine(rootState, _engineTemplate.Rng.Seed);
            var state = ExecuteFullActionFast(simulator, firstMove);

            if (state == rootState && !(firstMove is EndPhaseCommand))
                return -3000000f;

            ResetNodePool();

            // Pierwszy wêze³
            _beamBuffer[0] = GetNodeFromPool(state, firstMove, _strategy.Evaluate(state, _botId));
            int currentBeamSize = 1;

            for (int d = 1; d < _evolvingMaxDepth; d++)
            {
                int candidateCount = 0;

                for (int b = 0; b < currentBeamSize; b++)
                {
                    var node = _beamBuffer[b];
                    bool isBotTurn = node.State.ActivePlayerId == _botId;

                    var outcome = isBotTurn
                        ? SimulateAndFindBestOutcomeForBotFast(node.State)
                        : SimulateAndFindWorstOutcomeForBotFast(node.State);

                    // Punkt 1: U¿ywamy pre-alokowanej tablicy kandydatów
                    if (candidateCount < _candidateBuffer.Length)
                    {
                        _candidateBuffer[candidateCount++] = GetNodeFromPool(outcome.State, node.InitialMove, outcome.Score);
                    }
                }

                if (candidateCount == 0) break;

                // PUNKT 2: Ultra-szybki sort na tablicy (bez delegatów i alokacji)
                SortNodesDescending(_candidateBuffer, candidateCount);

                // Wybór najlepszych do nastêpnej tury beam
                currentBeamSize = Math.Min(candidateCount, _evolvingBeamWidth);
                for (int i = 0; i < currentBeamSize; i++)
                {
                    _beamBuffer[i] = _candidateBuffer[i];
                }
            }

            return _beamBuffer[0].Score;
        }
        private void SortNodesDescending(SimulationNode[] nodes, int count)
        {
            for (int i = 1; i < count; i++)
            {
                var key = nodes[i];
                int j = i - 1;
                while (j >= 0 && nodes[j].Score < key.Score)
                {
                    nodes[j + 1] = nodes[j];
                    j--;
                }
                nodes[j + 1] = key;
            }
        }
        private (float Score, string Reasoning) SimulateRoundWithReasoning(GameState rootState, IGameCommand firstMove)
        {
            var simulator = new GameEngine(rootState, _engineTemplate.Rng.Seed);
            var state = ExecuteFullAction(simulator, firstMove);

            if (state == rootState && !(firstMove is EndPhaseCommand))
                return (-3000000f, "Ruch nielegalny");

            ResetNodePool();
            var beam = new List<SimulationNode> {
                GetNodeFromPool(state, firstMove, _strategy.Evaluate(state, _botId))
            };

            for (int d = 1; d < _maxDepth; d++)
            {
                var nextCandidates = new List<SimulationNode>(_beamWidth * 2);

                for (int i = 0; i < beam.Count; i++)
                {
                    var node = beam[i];
                    bool isBotTurn = node.State.ActivePlayerId == _botId;
                    var outcome = isBotTurn
                        ? SimulateAndFindBestOutcomeForBot(node.State)
                        : SimulateAndFindWorstOutcomeForBot(node.State);

                    nextCandidates.Add(GetNodeFromPool(outcome.State, node.InitialMove, outcome.Score));
                }

                if (nextCandidates.Count == 0) break;

                // Manual sort for small lists
                if (nextCandidates.Count <= 10)
                {
                    InsertionSortNodesDescending(nextCandidates);
                    if (nextCandidates.Count > _beamWidth)
                        nextCandidates.RemoveRange(_beamWidth, nextCandidates.Count - _beamWidth);
                }
                else
                {
                    nextCandidates.Sort((a, b) => b.Score.CompareTo(a.Score));
                    if (nextCandidates.Count > _beamWidth)
                        nextCandidates.RemoveRange(_beamWidth, nextCandidates.Count - _beamWidth);
                }

                beam = nextCandidates;
            }

            var best = beam[0];
            for (int i = 1; i < beam.Count; i++)
            {
                if (beam[i].Score > best.Score)
                    best = beam[i];
            }

            var finalState = best.State;

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
            if (moves.Count == 0)
                return (currentState, _strategy.Evaluate(currentState, _botId));

            float bestScore = float.MinValue;
            GameState bestState = currentState;

            int moveLimit = Math.Min(moves.Count, 6);
            for (int i = 0; i < moveLimit; i++)
            {
                var sim = new GameEngine(currentState, _engineTemplate.Rng.Seed);
                var resState = ExecuteFullAction(sim, moves[i]);
                float score = _strategy.Evaluate(resState, _botId);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestState = resState;
                }
            }

            return (bestState, bestScore);
        }

        private (GameState State, float Score) SimulateAndFindBestOutcomeForBotFast(GameState currentState)
        {
            var moves = _moveGenerator.GenerateLegalMoves(currentState, _botId);
            if (moves.Count == 0)
                return (currentState, _strategy.Evaluate(currentState, _botId));

            float bestScore = float.MinValue;
            GameState bestState = currentState;

            int moveLimit = Math.Min(moves.Count, _evolvingMoveLimit);
            for (int i = 0; i < moveLimit; i++)
            {
                var sim = new GameEngine(currentState, _engineTemplate.Rng.Seed);
                var resState = ExecuteFullActionFast(sim, moves[i]);
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

            // W trybie ewolucji wy³¹czamy Phantom Hand dla szybkoœci
            GameState stateForOpponent = IsEvolving
                ? currentState // Bez Phantom Hand
                : _virtualOpponent.InjectRealisticPhantomHand(currentState, oppId);

            var enemyMoves = _moveGenerator.GenerateLegalMoves(stateForOpponent, oppId);

            float worstForBot = float.MaxValue;
            GameState worstState = currentState;

            int moveLimit = Math.Min(enemyMoves.Count, 6);
            for (int i = 0; i < moveLimit; i++)
            {
                var sim = new GameEngine(stateForOpponent, _engineTemplate.Rng.Seed);
                var resState = IsEvolving
                    ? ExecuteFullActionFast(sim, enemyMoves[i])
                    : ExecuteFullAction(sim, enemyMoves[i]);

                float score = _strategy.Evaluate(resState, _botId);
                if (score < worstForBot)
                {
                    worstForBot = score;
                    worstState = resState;
                }
            }

            return (worstState, worstForBot);
        }

        private (GameState State, float Score) SimulateAndFindWorstOutcomeForBotFast(GameState currentState)
        {
            var oppId = currentState.ActivePlayerId;

            // W trybie ewolucji NIE u¿ywamy Phantom Hand - znacznie szybsze!
            // U¿ywamy bezpoœrednio currentState
            var enemyMoves = _moveGenerator.GenerateLegalMoves(currentState, oppId);

            if (enemyMoves.Count == 0)
                return (currentState, _strategy.Evaluate(currentState, _botId));

            float worstForBot = float.MaxValue;
            GameState worstState = currentState;

            int moveLimit = Math.Min(enemyMoves.Count, _evolvingMoveLimit);
            for (int i = 0; i < moveLimit; i++)
            {
                var sim = new GameEngine(currentState, _engineTemplate.Rng.Seed);
                var resState = ExecuteFullActionFast(sim, enemyMoves[i]);
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
            while (state.PendingInteraction != null && safety++ < 8)
            {
                var targets = _moveGenerator.GenerateLegalMoves(state, _botId);
                if (targets.Count == 0)
                {
                    state = state.With(clearPending: true);
                    break;
                }

                IGameCommand bestT = targets[0];
                float bestV = float.MinValue;

                int limit = Math.Min(targets.Count, 4);
                for (int i = 0; i < limit; i++)
                {
                    var t = targets[i];
                    var testEngine = new GameEngine(state, _engineTemplate.Rng.Seed);
                    var testState = testEngine.ExecuteCommand(t).NewState;
                    float v = _strategy.Evaluate(testState, _botId);
                    if (v > bestV)
                    {
                        bestV = v;
                        bestT = t;
                    }
                }

                state = engine.ExecuteCommand(bestT).NewState;
            }

            return state;
        }

        private GameState ExecuteFullActionFast(GameEngine engine, IGameCommand cmd)
        {
            var result = engine.ExecuteCommand(cmd);
            var state = result.NewState;

            // Bezpiecznik Mulliganu - u¿ywamy state.ActivePlayerId
            if (state.CurrentPhase == GamePhase.Mulligan)
            {
                var player = state.GetPlayer(state.ActivePlayerId); // Zmiana tutaj
                if (!state.PlayersReady.Contains(player.PlayerId))
                {
                    var toReplace = player.Hand
                        .Where(c => c.Definition.BaseStats.BloodCost > 3)
                        .Select(c => c.InstanceId)
                        .ToList();
                    state = engine.ExecuteCommand(new ConfirmMulliganCommand(player.PlayerId, toReplace)).NewState;
                    return state;
                }
            }

            int safety = 0;
            while (state.PendingInteraction != null && safety++ < 8)
            {
                var targets = _moveGenerator.GenerateLegalMoves(state, _botId);
                if (targets.Count == 0) { state = state.With(clearPending: true); break; }
                state = engine.ExecuteCommand(targets[0]).NewState;
            }

            return state;
        }

        private string GenerateDeepReasoning(GameState start, GameState end)
        {
            var pS = start.GetPlayer(_botId);
            var pE = end.GetPlayer(_botId);
            var eS = start.GetOpponent(_botId);
            var eE = end.GetOpponent(_botId);

            if (eE.Health <= 0) return "!!! LETHAL IN SIGHT !!!";

            var r = new List<string>(5);

            int cD = pE.Hand.Count - pS.Hand.Count;
            if (cD != 0) r.Add($"Cards:{(cD > 0 ? "+" : "")}{cD}");

            int hD = eS.Health - eE.Health;
            if (hD > 0) r.Add($"EnemyDmg:{hD}");

            if (pE.CurrentBlood > 0 && start.TurnNumber == end.TurnNumber)
                r.Add($"Save:{pE.CurrentBlood}B");

            int kills = start.Board.GetAllUnits().Count(u => u.OwnerPlayerId != _botId) -
                       end.Board.GetAllUnits().Count(u => u.OwnerPlayerId != _botId);
            if (kills > 0) r.Add($"Kills:{kills}");

            return r.Count == 0 ? "Hold Position" : "WIZJA: " + string.Join(", ", r);
        }

        private string GenerateDescription(IGameCommand move, GameState state)
        {
            if (move is PlayUnitCommand pu)
            {
                var hand = state.GetPlayer(_botId).Hand;
                for (int i = 0; i < hand.Count; i++)
                {
                    var card = hand[i];
                    if (card.InstanceId == pu.CardInstanceId)
                        return $"Play {card.Definition.Name ?? "Unit"}";
                }
                return "Play Unit";
            }

            if (move is PlaySpellCommand ps)
            {
                var hand = state.GetPlayer(_botId).Hand;
                for (int i = 0; i < hand.Count; i++)
                {
                    var card = hand[i];
                    if (card.InstanceId == ps.CardInstanceId)
                        return $"Cast {card.Definition.Name ?? "Spell"}";
                }
                return "Cast Spell";
            }

            return move is EndPhaseCommand ? "End Turn" : move.GetType().Name;
        }

        // Node pooling methods - POPRAWIONE
        private SimulationNode GetNodeFromPool(GameState state, IGameCommand? initialMove, float score)
        {
            SimulationNode node;

            if (_nodePoolIndex < _nodePool.Count)
            {
                // Reuse existing node
                node = _nodePool[_nodePoolIndex];
                node.Set(state, initialMove, score);
            }
            else
            {
                // Create new node
                node = new SimulationNode(state, initialMove, score);
                _nodePool.Add(node);
            }

            _nodePoolIndex++;
            return node;
        }

        private void ResetNodePool()
        {
            _nodePoolIndex = 0;
        }

        // Optimized sorting methods
        private static void InsertionSortDescending(List<EvaluatedMove> list)
        {
            for (int i = 1; i < list.Count; i++)
            {
                var current = list[i];
                int j = i - 1;

                while (j >= 0 && list[j].Score < current.Score)
                {
                    list[j + 1] = list[j];
                    j--;
                }
                list[j + 1] = current;
            }
        }

        private static void InsertionSortNodesDescending(List<SimulationNode> list)
        {
            for (int i = 1; i < list.Count; i++)
            {
                var current = list[i];
                int j = i - 1;

                while (j >= 0 && list[j].Score < current.Score)
                {
                    list[j + 1] = list[j];
                    j--;
                }
                list[j + 1] = current;
            }
        }
    }
}