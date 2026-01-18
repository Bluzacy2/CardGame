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
    #region Supporting Types
    /// <summary>
    /// Represents a game move along with its AI evaluation score and descriptive information.
    /// </summary>
    public class EvaluatedMove
    {
        /// <summary>
        /// Gets the command representing the game move.
        /// </summary>
        public IGameCommand Command { get; }

        /// <summary>
        /// Gets the AI evaluation score for this move.
        /// </summary>
        public float Score { get; }

        /// <summary>
        /// Gets a brief description of the move.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Gets detailed reasoning behind the AI's evaluation.
        /// </summary>
        public string DeepReasoning { get; }

        /// <summary>
        /// Initializes a new instance of the EvaluatedMove class.
        /// </summary>
        /// <param name="command">The game command to execute.</param>
        /// <param name="score">The evaluation score for this move.</param>
        /// <param name="description">Optional brief description of the move.</param>
        /// <param name="deepReasoning">Optional detailed reasoning for the evaluation.</param>
        public EvaluatedMove(IGameCommand command, float score, string description = "", string deepReasoning = "")
        {
            Command = command;
            Score = score;
            Description = description;
            DeepReasoning = deepReasoning;
        }
    }

    /// <summary>
    /// Mutable class for node pooling to reduce garbage collection during AI simulations.
    /// </summary>
    public class SimulationNode
    {
        /// <summary>
        /// Gets or sets the game state at this node.
        /// </summary>
        public GameState State { get; set; }

        /// <summary>
        /// Gets or sets the initial move that led to this game state.
        /// </summary>
        public IGameCommand? InitialMove { get; set; }

        /// <summary>
        /// Gets or sets the evaluation score for this node.
        /// </summary>
        public float Score { get; set; }

        /// <summary>
        /// Initializes a new instance of the SimulationNode class.
        /// </summary>
        public SimulationNode() { }

        /// <summary>
        /// Initializes a new instance of the SimulationNode class with specified values.
        /// </summary>
        /// <param name="state">The game state at this node.</param>
        /// <param name="initialMove">The initial move that led to this state.</param>
        /// <param name="score">The evaluation score for this node.</param>
        public SimulationNode(GameState state, IGameCommand? initialMove, float score)
        {
            State = state;
            InitialMove = initialMove;
            Score = score;
        }

        /// <summary>
        /// Updates the node with new values for reuse in the object pool.
        /// </summary>
        /// <param name="state">The new game state.</param>
        /// <param name="initialMove">The new initial move.</param>
        /// <param name="score">The new evaluation score.</param>
        public void Set(GameState state, IGameCommand? initialMove, float score)
        {
            State = state;
            InitialMove = initialMove;
            Score = score;
        }
    }
    #endregion

    /// <summary>
    /// AI solver that uses beam search and simulation to evaluate potential game moves.
    /// </summary>
    public class BotSolver
    {
        #region Constants and Static Fields
        /// <summary>
        /// Determines whether the AI is in evolving mode (faster, less accurate) or full evaluation mode.
        /// </summary>
        public static bool IsEvolving = true;
        #endregion

        #region Private Fields
        private readonly GameEngine _engineTemplate;
        private readonly MoveGenerator _moveGenerator;
        private readonly IAIStrategy _strategy;
        private readonly VirtualOpponent _virtualOpponent;
        private readonly int _botId;
        private readonly int _beamWidth;
        private readonly int _maxDepth;
        private readonly int _evolvingBeamWidth;
        private readonly int _evolvingMaxDepth;
        private readonly int _evolvingMoveLimit;
        private readonly List<SimulationNode> _nodePool = new();
        private int _nodePoolIndex = 0;
        private readonly SimulationNode[] _beamBuffer;
        private readonly SimulationNode[] _candidateBuffer;
        #endregion

        #region Constructor
        /// <summary>
        /// Initializes a new instance of the BotSolver with the specified configuration.
        /// </summary>
        /// <param name="engine">The game engine template for simulations.</param>
        /// <param name="botId">The identifier of the bot player.</param>
        /// <param name="strategy">The AI strategy for state evaluation.</param>
        /// <param name="beamWidth">The width of the beam search in full evaluation mode.</param>
        /// <param name="maxDepth">The maximum search depth in full evaluation mode.</param>
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
        #endregion

        #region Public Methods
        /// <summary>
        /// Evaluates all legal moves from the current game state and returns them sorted by score.
        /// </summary>
        /// <param name="rootState">The current game state to analyze.</param>
        /// <returns>A list of evaluated moves sorted in descending order of score.</returns>
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
                    var simulationResult = SimulateRoundWithReasoning(rootState, move);
                    string description = GenerateDescription(move, rootState);
                    results.Add(new EvaluatedMove(move, simulationResult.Score, description, simulationResult.Reasoning));
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
        #endregion

        #region Simulation Methods
        private float SimulateRoundFast(GameState rootState, IGameCommand firstMove)
        {
            var simulator = new GameEngine(rootState, _engineTemplate.Rng.Seed);
            var state = ExecuteFullActionFast(simulator, firstMove);

            if (state == rootState && !(firstMove is EndPhaseCommand))
                return -3000000f;

            ResetNodePool();

            // First node
            _beamBuffer[0] = GetNodeFromPool(state, firstMove, _strategy.Evaluate(state, _botId));
            int currentBeamSize = 1;

            for (int depth = 1; depth < _evolvingMaxDepth; depth++)
            {
                int candidateCount = 0;

                for (int beamIndex = 0; beamIndex < currentBeamSize; beamIndex++)
                {
                    var node = _beamBuffer[beamIndex];
                    bool isBotTurn = node.State.ActivePlayerId == _botId;

                    var outcome = isBotTurn
                        ? SimulateAndFindBestOutcomeForBotFast(node.State)
                        : SimulateAndFindWorstOutcomeForBotFast(node.State);

                    if (candidateCount < _candidateBuffer.Length)
                    {
                        _candidateBuffer[candidateCount++] = GetNodeFromPool(outcome.State, node.InitialMove, outcome.Score);
                    }
                }

                if (candidateCount == 0) break;

                SortNodesDescending(_candidateBuffer, candidateCount);

                currentBeamSize = Math.Min(candidateCount, _evolvingBeamWidth);
                for (int i = 0; i < currentBeamSize; i++)
                {
                    _beamBuffer[i] = _candidateBuffer[i];
                }
            }

            return _beamBuffer[0].Score;
        }

        private (float Score, string Reasoning) SimulateRoundWithReasoning(GameState rootState, IGameCommand firstMove)
        {
            var simulator = new GameEngine(rootState, _engineTemplate.Rng.Seed);
            var state = ExecuteFullAction(simulator, firstMove);

            if (state == rootState && !(firstMove is EndPhaseCommand))
                return (-3000000f, "Illegal move");

            ResetNodePool();
            var beam = new List<SimulationNode> {
                GetNodeFromPool(state, firstMove, _strategy.Evaluate(state, _botId))
            };

            for (int depth = 1; depth < _maxDepth; depth++)
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
                var endSimulator = new GameEngine(finalState, _engineTemplate.Rng.Seed);
                finalState = endSimulator.ExecuteCommand(new EndPhaseCommand(finalState.ActivePlayerId)).NewState;
            }

            return (best.Score, GenerateDeepReasoning(rootState, finalState));
        }
        #endregion

        #region Outcome Simulation
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
                var simulator = new GameEngine(currentState, _engineTemplate.Rng.Seed);
                var resultState = ExecuteFullAction(simulator, moves[i]);
                float score = _strategy.Evaluate(resultState, _botId);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestState = resultState;
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
                var simulator = new GameEngine(currentState, _engineTemplate.Rng.Seed);
                var resultState = ExecuteFullActionFast(simulator, moves[i]);
                float score = _strategy.Evaluate(resultState, _botId);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestState = resultState;
                }
            }

            return (bestState, bestScore);
        }

        private (GameState State, float Score) SimulateAndFindWorstOutcomeForBot(GameState currentState)
        {
            int opponentId = currentState.ActivePlayerId;

            GameState stateForOpponent = IsEvolving
                ? currentState
                : _virtualOpponent.InjectRealisticPhantomHand(currentState, opponentId);

            var enemyMoves = _moveGenerator.GenerateLegalMoves(stateForOpponent, opponentId);

            float worstForBot = float.MaxValue;
            GameState worstState = currentState;

            int moveLimit = Math.Min(enemyMoves.Count, 6);
            for (int i = 0; i < moveLimit; i++)
            {
                var simulator = new GameEngine(stateForOpponent, _engineTemplate.Rng.Seed);
                var resultState = IsEvolving
                    ? ExecuteFullActionFast(simulator, enemyMoves[i])
                    : ExecuteFullAction(simulator, enemyMoves[i]);

                float score = _strategy.Evaluate(resultState, _botId);
                if (score < worstForBot)
                {
                    worstForBot = score;
                    worstState = resultState;
                }
            }

            return (worstState, worstForBot);
        }

        private (GameState State, float Score) SimulateAndFindWorstOutcomeForBotFast(GameState currentState)
        {
            int opponentId = currentState.ActivePlayerId;
            var enemyMoves = _moveGenerator.GenerateLegalMoves(currentState, opponentId);

            if (enemyMoves.Count == 0)
                return (currentState, _strategy.Evaluate(currentState, _botId));

            float worstForBot = float.MaxValue;
            GameState worstState = currentState;

            int moveLimit = Math.Min(enemyMoves.Count, _evolvingMoveLimit);
            for (int i = 0; i < moveLimit; i++)
            {
                var simulator = new GameEngine(currentState, _engineTemplate.Rng.Seed);
                var resultState = ExecuteFullActionFast(simulator, enemyMoves[i]);
                float score = _strategy.Evaluate(resultState, _botId);

                if (score < worstForBot)
                {
                    worstForBot = score;
                    worstState = resultState;
                }
            }

            return (worstState, worstForBot);
        }
        #endregion

        #region Command Execution
        private GameState ExecuteFullAction(GameEngine engine, IGameCommand command)
        {
            var result = engine.ExecuteCommand(command);
            var state = result.NewState;

            int safetyCounter = 0;
            while (state.PendingInteraction != null && safetyCounter++ < 8)
            {
                var targets = _moveGenerator.GenerateLegalMoves(state, _botId);
                if (targets.Count == 0)
                {
                    state = state.With(clearPending: true);
                    break;
                }

                IGameCommand bestTarget = targets[0];
                float bestValue = float.MinValue;

                int limit = Math.Min(targets.Count, 4);
                for (int i = 0; i < limit; i++)
                {
                    var target = targets[i];
                    var testEngine = new GameEngine(state, _engineTemplate.Rng.Seed);
                    var testState = testEngine.ExecuteCommand(target).NewState;
                    float value = _strategy.Evaluate(testState, _botId);
                    if (value > bestValue)
                    {
                        bestValue = value;
                        bestTarget = target;
                    }
                }

                state = engine.ExecuteCommand(bestTarget).NewState;
            }

            return state;
        }

        private GameState ExecuteFullActionFast(GameEngine engine, IGameCommand command)
        {
            var result = engine.ExecuteCommand(command);
            var state = result.NewState;

            // Mulligan safety - we use state.ActivePlayerId
            if (state.CurrentPhase == GamePhase.Mulligan)
            {
                var player = state.GetPlayer(state.ActivePlayerId);
                if (!state.PlayersReady.Contains(player.PlayerId))
                {
                    var toReplace = player.Hand
                        .Where(card => card.Definition.BaseStats.BloodCost > 3)
                        .Select(card => card.InstanceId)
                        .ToList();
                    state = engine.ExecuteCommand(new ConfirmMulliganCommand(player.PlayerId, toReplace)).NewState;
                    return state;
                }
            }

            int safetyCounter = 0;
            while (state.PendingInteraction != null && safetyCounter++ < 8)
            {
                var targets = _moveGenerator.GenerateLegalMoves(state, _botId);
                if (targets.Count == 0)
                {
                    state = state.With(clearPending: true);
                    break;
                }
                state = engine.ExecuteCommand(targets[0]).NewState;
            }

            return state;
        }
        #endregion

        #region Description Generation
        private string GenerateDeepReasoning(GameState start, GameState end)
        {
            var playerStart = start.GetPlayer(_botId);
            var playerEnd = end.GetPlayer(_botId);
            var enemyStart = start.GetOpponent(_botId);
            var enemyEnd = end.GetOpponent(_botId);

            if (enemyEnd.Health <= 0) return "!!! LETHAL IN SIGHT !!!";

            var reasons = new List<string>(5);

            int cardDelta = enemyEnd.Hand.Count - playerStart.Hand.Count;
            if (cardDelta != 0) reasons.Add($"Cards:{(cardDelta > 0 ? "+" : "")}{cardDelta}");

            int healthDelta = enemyStart.Health - enemyEnd.Health;
            if (healthDelta > 0) reasons.Add($"EnemyDmg:{healthDelta}");

            if (playerEnd.CurrentBlood > 0 && start.TurnNumber == end.TurnNumber)
                reasons.Add($"Save:{playerEnd.CurrentBlood}B");

            int kills = start.Board.GetAllUnits().Count(unit => unit.OwnerPlayerId != _botId) -
                       end.Board.GetAllUnits().Count(unit => unit.OwnerPlayerId != _botId);
            if (kills > 0) reasons.Add($"Kills:{kills}");

            return reasons.Count == 0 ? "Hold Position" : "VISION: " + string.Join(", ", reasons);
        }

        private string GenerateDescription(IGameCommand move, GameState state)
        {
            if (move is PlayUnitCommand playUnitCommand)
            {
                var hand = state.GetPlayer(_botId).Hand;
                for (int i = 0; i < hand.Count; i++)
                {
                    var card = hand[i];
                    if (card.InstanceId == playUnitCommand.CardInstanceId)
                        return $"Play {card.Definition.Name ?? "Unit"}";
                }
                return "Play Unit";
            }

            if (move is PlaySpellCommand playSpellCommand)
            {
                var hand = state.GetPlayer(_botId).Hand;
                for (int i = 0; i < hand.Count; i++)
                {
                    var card = hand[i];
                    if (card.InstanceId == playSpellCommand.CardInstanceId)
                        return $"Cast {card.Definition.Name ?? "Spell"}";
                }
                return "Cast Spell";
            }

            return move is EndPhaseCommand ? "End Turn" : move.GetType().Name;
        }
        #endregion

        #region Node Pool Management
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
        #endregion

        #region Optimization Methods
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
        #endregion
    }
}