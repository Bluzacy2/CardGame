using CardGame.Core.AI.Interfaces;
using CardGame.Core.AI.Logic;
using CardGame.Core.AI.Logic.Mcts;
using CardGame.Core.AI.Strategies;
using CardGame.Core.Application;
using CardGame.Core.Commands.Implementations;
using CardGame.Core.Commands.Interfaces;
using CardGame.Core.State.Enums;
using CardGame.Core.State.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace CardGame.Core.AI
{
    #region Enums

    /// <summary>
    /// Specifies the AI solver algorithm to use for decision making.
    /// </summary>
    public enum AISolverType
    {
        /// <summary>
        /// Fast heuristic solver using beam search.
        /// </summary>
        BeamSearch,

        /// <summary>
        /// Advanced Monte Carlo Tree Search solver.
        /// </summary>
        MCTS
    }

    #endregion

    #region AI Controller

    /// <summary>
    /// Controls AI player behavior and decision making during gameplay.
    /// </summary>
    public class AIPlayerController
    {
        /// <summary>
        /// Gets the player ID controlled by this AI.
        /// </summary>
        public int BotPlayerId { get; }

        /// <summary>
        /// Gets the beam search solver for heuristic-based decisions.
        /// </summary>
        public readonly BotSolver BeamSolver;

        private readonly GameEngine _gameEngine;
        private readonly MctsSolver _mctsSolver;
        private readonly AISolverType _solverType;
        private bool _isRunning = false;

        /// <summary>
        /// Initializes a new instance of the AIPlayerController class.
        /// </summary>
        /// <param name="gameEngine">The game engine managing the current game.</param>
        /// <param name="botPlayerId">The player ID this AI controls.</param>
        /// <param name="strategy">The strategy used for AI decision evaluation.</param>
        /// <param name="solverType">The type of solver algorithm to use.</param>
        public AIPlayerController(
            GameEngine gameEngine,
            int botPlayerId,
            IAIStrategy strategy,
            AISolverType solverType)
        {
            _gameEngine = gameEngine;
            BotPlayerId = botPlayerId;
            _solverType = solverType;

            // Initialize both solvers for flexibility
            // Beam Search: Width 4, Depth 5 (standard settings)
            BeamSolver = new BotSolver(_gameEngine, BotPlayerId, strategy, beamWidth: 4, maxDepth: 5);

            // MCTS uses the same strategy for leaf node evaluation
            _mctsSolver = new MctsSolver(_gameEngine, BotPlayerId, strategy);
        }

        #region Public Methods

        /// <summary>
        /// Starts automatic gameplay for the AI player.
        /// </summary>
        public async void StartAutoPlay()
        {
            if (_isRunning)
            {
                return;
            }

            _isRunning = true;

            while (!_gameEngine.IsGameOver)
            {
                var state = _gameEngine.CurrentState;

                // 1. MULLIGAN PHASE (Card exchange)
                if (state.CurrentPhase == GamePhase.Mulligan)
                {
                    if (!state.PlayersReady.Contains(BotPlayerId))
                    {
                        // Future enhancement: Add intelligent mulligan logic here
                        _gameEngine.ExecuteCommand(new ConfirmMulliganCommand(BotPlayerId, new System.Collections.Generic.List<int>()));
                    }

                    await Task.Delay(100);
                    continue;
                }

                // 2. BOT'S TURN
                if (state.ActivePlayerId == BotPlayerId)
                {
                    // Simulate thinking time (important for UX)
                    await Task.Delay(500);

                    IGameCommand bestCommand = null;
                    string moveLog = string.Empty;

                    // --- ALGORITHM SELECTION ---
                    if (_solverType == AISolverType.MCTS)
                    {
                        // MCTS: Thinks for 1.5 seconds
                        var bestMove = _mctsSolver.FindBestMove(state, thinkingTimeMs: 1500);
                        bestCommand = bestMove.Command;
                        moveLog = $"[MCTS] Score: {bestMove.Score:F2} | {bestMove.DeepReasoning}";
                    }
                    else
                    {
                        // BEAM SEARCH (Old): Works immediately
                        var evaluatedMoves = BeamSolver.FindBestMoves(state);

                        if (evaluatedMoves.Any())
                        {
                            var best = evaluatedMoves.First();
                            bestCommand = best.Command;
                            moveLog = $"[BEAM] Score: {best.Score:F0} | {best.Description}";
                        }
                        else
                        {
                            bestCommand = new EndPhaseCommand(BotPlayerId);
                            moveLog = "[BEAM] No moves -> EndPhase";
                        }
                    }

                    // --- MOVE EXECUTION ---
                    Console.WriteLine($"[P{BotPlayerId}] {moveLog}"); // Log to console (optional)

                    var result = _gameEngine.ExecuteCommand(bestCommand);

                    // Safety: If the move changed nothing (illegal/error) and it's not EndPhase, force end of turn
                    // Prevents infinite loops when the bot tries to play a card it cannot.
                    if (result.NewState == state && !(bestCommand is EndPhaseCommand))
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"[CRITICAL] Bot P{BotPlayerId} stuck! Forcing end of turn.");
                        Console.ResetColor();
                        _gameEngine.ExecuteCommand(new EndPhaseCommand(BotPlayerId));
                    }
                }
                else
                {
                    // Opponent's turn - wait
                    await Task.Delay(100);
                }
            }

            _isRunning = false;
        }

        #endregion
    }

    #endregion
}