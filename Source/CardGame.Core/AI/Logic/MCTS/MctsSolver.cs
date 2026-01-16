using CardGame.Core.AI.Interfaces;
using CardGame.Core.Application;
using CardGame.Core.Commands.Implementations;
using CardGame.Core.Commands.Interfaces;
using CardGame.Core.State.Enums;
using CardGame.Core.State.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace CardGame.Core.AI.Logic.Mcts
{
    /// <summary>
    /// Monte Carlo Tree Search implementation for evaluating and selecting optimal game moves.
    /// </summary>
    public class MctsSolver
    {
        #region Constants
        /// <summary>
        /// Simulation depth - the deeper, the more accurate, but slower. 20 is a solid compromise.
        /// </summary>
        private const int SIMULATION_DEPTH = 20;

        /// <summary>
        /// Exploration constant - 1.41 (square root of 2) is the theoretical optimum.
        /// </summary>
        private const double EXPLORATION_CONSTANT = 1.4142;
        #endregion

        #region Private Fields
        private readonly GameEngine _engineTemplate;
        private readonly int _botId;
        private readonly MoveGenerator _moveGenerator;
        private readonly VirtualOpponent _virtualOpponent;
        private readonly IAIStrategy _heuristic;
        private readonly DeterministicRng _rng;
        #endregion

        #region Constructor
        /// <summary>
        /// Initializes a new instance of the Monte Carlo Tree Search solver.
        /// </summary>
        /// <param name="engine">The game engine template for simulations.</param>
        /// <param name="botId">The identifier of the bot player.</param>
        /// <param name="heuristic">The heuristic evaluation strategy.</param>
        public MctsSolver(GameEngine engine, int botId, IAIStrategy heuristic)
        {
            _engineTemplate = engine;
            _botId = botId;
            _heuristic = heuristic;
            _moveGenerator = new MoveGenerator();
            _virtualOpponent = new VirtualOpponent(engine.Factory);
            _rng = new DeterministicRng(new Random().Next());
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Analyzes the game state and returns the best move according to MCTS simulation.
        /// </summary>
        /// <param name="rootState">The current game state to analyze.</param>
        /// <param name="thinkingTimeMs">Maximum time to spend on analysis in milliseconds.</param>
        /// <returns>The best evaluated move with its probability of success.</returns>
        public EvaluatedMove FindBestMove(GameState rootState, int thinkingTimeMs = 1500)
        {
            // 1. Determinization: We assume a specific (though guessed) opponent hand
            int opponentId = rootState.ActivePlayerId == 1 ? 2 : 1;
            GameState determinizedState = _virtualOpponent.InjectRealisticPhantomHand(rootState, opponentId);

            var rootNode = new MctsNode(determinizedState, null, null, _botId, _moveGenerator);
            var timer = Stopwatch.StartNew();
            int iterations = 0;

            // 2. Main MCTS loop
            while (timer.ElapsedMilliseconds < thinkingTimeMs)
            {
                // A. Selection: Traverse down the tree to a leaf
                MctsNode node = Select(rootNode);

                // B. Expansion: Add a new node if the game is not over
                if (!node.IsTerminal && !node.IsFullyExpanded)
                {
                    node = Expand(node);
                }

                // C. Simulation (Rollout): Fast, random play until the end (or until depth)
                double result = Simulate(node.State);

                // D. Backpropagation: Update statistics up the tree
                Backpropagate(node, result);

                iterations++;
            }

            // 3. Move choice: The most visited node is the most "trusted"
            var bestChild = rootNode.Children.OrderByDescending(c => c.Visits).FirstOrDefault();

            if (bestChild == null || bestChild.MoveEntered == null)
            {
                return new EvaluatedMove(new EndPhaseCommand(_botId), -999, "Panic Skip");
            }

            float winProbability = (float)(bestChild.Score / bestChild.Visits);
            string reasoning = $"MCTS: {iterations} iterations | WinChance: {winProbability:P1} (Visits: {bestChild.Visits})";

            return new EvaluatedMove(bestChild.MoveEntered, winProbability, "MCTS Choice", reasoning);
        }
        #endregion

        #region MCTS Core Algorithms
        private MctsNode Select(MctsNode node)
        {
            while (!node.IsTerminal && node.IsFullyExpanded && node.Children.Count > 0)
            {
                node = node.GetBestChild(EXPLORATION_CONSTANT);
            }
            return node;
        }

        private MctsNode Expand(MctsNode node)
        {
            var move = node.UntriedMoves[_rng.Next(0, node.UntriedMoves.Count)];
            node.UntriedMoves.Remove(move);

            var engine = new GameEngine(node.State, _engineTemplate.Rng.Seed);
            var result = engine.ExecuteCommand(move);
            var newState = result.NewState;

            // Safety Break: Handling PendingInteraction (e.g., target selection)
            int safetyCounter = 0;
            while (newState.PendingInteraction != null && safetyCounter++ < 15)
            {
                var legalTargets = _moveGenerator.GenerateLegalMoves(newState, newState.ActivePlayerId);
                if (!legalTargets.Any()) break;

                var randomTarget = legalTargets[_rng.Next(0, legalTargets.Count)];
                var subResult = engine.ExecuteCommand(randomTarget);

                if (subResult.NewState == newState) break;
                newState = subResult.NewState;
            }

            var childNode = new MctsNode(newState, node, move, node.State.ActivePlayerId, _moveGenerator);
            node.Children.Add(childNode);
            return childNode;
        }

        private double Simulate(GameState initialState)
        {
            var currentState = initialState;
            var engine = new GameEngine(currentState, _rng.Next(0, int.MaxValue));
            int depth = 0;

            while (!IsGameOver(currentState) && depth < SIMULATION_DEPTH)
            {
                var moves = _moveGenerator.GenerateLegalMoves(currentState, currentState.ActivePlayerId);
                if (moves.Count == 0) break;

                // "Killer Move" Optimization
                var lethalMove = FindLethalMove(currentState, moves);

                IGameCommand selectedMove;
                if (lethalMove != null)
                {
                    selectedMove = lethalMove;
                }
                else
                {
                    // Epsilon-Greedy Heuristic:
                    // 80% chance to play something sensible (not EndPhase), 20% chance to play anything.
                    if (_rng.Next(0, 10) < 8 && moves.Any(m => m is not EndPhaseCommand))
                    {
                        var activeMoves = moves.Where(m => m is not EndPhaseCommand).ToList();
                        selectedMove = activeMoves[_rng.Next(0, activeMoves.Count)];
                    }
                    else
                    {
                        selectedMove = moves[_rng.Next(0, moves.Count)];
                    }
                }

                var result = engine.ExecuteCommand(selectedMove);
                currentState = result.NewState;

                // Safety Break in Simulation
                int safetyCounter = 0;
                while (currentState.PendingInteraction != null && safetyCounter++ < 15)
                {
                    var targets = _moveGenerator.GenerateLegalMoves(currentState, currentState.ActivePlayerId);
                    if (!targets.Any()) break;

                    var randomTarget = targets[_rng.Next(0, targets.Count)];
                    var subResult = engine.ExecuteCommand(randomTarget);

                    if (subResult.NewState == currentState) break;
                    currentState = subResult.NewState;
                }

                depth++;
            }

            // Final State Evaluation
            // We return the result from the perspective of the BOT (_botId)
            // 1.0 = Bot Won / 0.0 = Bot Lost
            if (IsGameOver(currentState))
            {
                if (currentState.PlayerA.Health <= 0 && _botId == 2) return 1.0;
                if (currentState.PlayerB.Health <= 0 && _botId == 1) return 1.0;
                return 0.0; // Loss
            }

            // If the game is not over, we use the heuristic evaluation
            float heuristicScore = _heuristic.Evaluate(currentState, _botId);

            // Sigmoid flattens the result (e.g., -5000 to +5000) to the range (0.0 to 1.0)
            // Divisor 4000.0f "stretches" the sensitivity
            return Sigmoid(heuristicScore / 4000.0f);
        }

        private void Backpropagate(MctsNode node, double result)
        {
            // result = result from the perspective of the MAIN BOT (_botId)
            // 1.0 = Bot wins, 0.0 = Bot loses

            MctsNode? currentNode = node;
            while (currentNode != null)
            {
                // NEGAMAX / MINMAX LOGIC:
                // Each node stores the statistic "How good was this move for the player who made it?"
                double valueForNodeOwner = currentNode.PlayerIdJustMoved == _botId
                    ? result
                    : 1.0 - result;

                currentNode.Update(valueForNodeOwner);
                currentNode = currentNode.Parent;
            }
        }
        #endregion

        #region Helper Methods
        private IGameCommand? FindLethalMove(GameState state, List<IGameCommand> moves)
        {
            // Simple heuristic: Check if attacking the Hero can win in this move
            // We don't simulate the entire engine here (too slow), just a quick check.

            // If it's the attack phase
            if (state.CurrentPhase == GamePhase.Combat) return null;

            int enemyHealth = state.GetOpponent(state.ActivePlayerId).Health;

            // Look for a spell that deals damage (very simplified)
            foreach (var move in moves)
            {
                if (move is PlaySpellCommand playSpellCommand)
                {
                    // Here you could add logic to check if the card is, for example, Fireball to the Hero
                    // Would require access to the card definition.
                    // For performance, in this example, we skip deep card analysis.
                }
            }
            return null;
        }

        private bool IsGameOver(GameState state) => state.PlayerA.Health <= 0 || state.PlayerB.Health <= 0;

        private double Sigmoid(double value) => 1.0 / (1.0 + Math.Exp(-value));
        #endregion
    }
}