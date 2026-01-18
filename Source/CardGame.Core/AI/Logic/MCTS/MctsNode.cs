using CardGame.Core.Commands.Interfaces;
using CardGame.Core.State.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CardGame.Core.AI.Logic.Mcts
{
    /// <summary>
    /// Represents a node in the Monte Carlo Tree Search algorithm.
    /// </summary>
    public class MctsNode
    {
        #region Properties

        /// <summary>
        /// Gets the game state at this node.
        /// </summary>
        public GameState State { get; }

        /// <summary>
        /// Gets the parent node of this node.
        /// </summary>
        public MctsNode? Parent { get; }

        /// <summary>
        /// Gets the move that led to this state.
        /// </summary>
        public IGameCommand? MoveEntered { get; }

        /// <summary>
        /// Gets the ID of the player who just moved to reach this state.
        /// </summary>
        public int PlayerIdJustMoved { get; }

        /// <summary>
        /// Gets the child nodes of this node.
        /// </summary>
        public List<MctsNode> Children { get; } = new List<MctsNode>();

        /// <summary>
        /// Gets the list of moves that have not been tried from this node.
        /// </summary>
        public List<IGameCommand> UntriedMoves { get; private set; }

        /// <summary>
        /// Gets the number of times this node has been visited during simulation.
        /// </summary>
        public int Visits { get; private set; }

        /// <summary>
        /// Gets the cumulative score from simulations (from the perspective of the root player).
        /// </summary>
        public double Score { get; private set; }

        /// <summary>
        /// Gets a value indicating whether all possible moves have been expanded from this node.
        /// </summary>
        public bool IsFullyExpanded => UntriedMoves.Count == 0;

        /// <summary>
        /// Gets a value indicating whether this node represents a terminal game state (game over).
        /// </summary>
        public bool IsTerminal => State.PlayerA.Health <= 0 || State.PlayerB.Health <= 0;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the MctsNode class.
        /// </summary>
        /// <param name="state">The game state at this node.</param>
        /// <param name="parent">The parent node, or null if this is the root.</param>
        /// <param name="move">The move that led to this state.</param>
        /// <param name="playerIdJustMoved">The ID of the player who just moved.</param>
        /// <param name="moveGenerator">The move generator for creating untried moves.</param>
        public MctsNode(
            GameState state,
            MctsNode? parent,
            IGameCommand? move,
            int playerIdJustMoved,
            MoveGenerator moveGenerator)
        {
            State = state;
            Parent = parent;
            MoveEntered = move;
            PlayerIdJustMoved = playerIdJustMoved;

            // Generate possible moves for the player whose turn it is in this state
            UntriedMoves = moveGenerator.GenerateLegalMoves(state, state.ActivePlayerId);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Updates the node's statistics with the result of a simulation.
        /// </summary>
        /// <param name="result">The result value to add (from root player's perspective).</param>
        public void Update(double result)
        {
            Visits++;
            Score += result;
        }

        /// <summary>
        /// Selects the best child node using the UCT (Upper Confidence Bound for Trees) formula.
        /// </summary>
        /// <param name="explorationParameter">The exploration constant (default is √2).</param>
        /// <returns>The child node with the highest UCT value.</returns>
        public MctsNode GetBestChild(double explorationParameter = 1.41)
        {
            // Sort to find the child with the highest UCT value
            return Children.OrderByDescending(child =>
            {
                // Important: If it was the opponent's turn, the result is inverted for us
                double exploitation = child.Score / child.Visits;
                double exploration = explorationParameter * Math.Sqrt(Math.Log(Visits) / child.Visits);
                return exploitation + exploration;
            }).First();
        }

        #endregion
    }
}