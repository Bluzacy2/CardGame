using System;
using System.Linq;
using System.Threading.Tasks;
using CardGame.Core.AI.Interfaces;
using CardGame.Core.AI.Strategies;
using CardGame.Core.Application;
using CardGame.Core.Commands.Interfaces;
using CardGame.Core.Commands.Implementations;
using CardGame.Core.State.Enums;
using CardGame.Core.State.Models;
using CardGame.Core.AI.Logic;

namespace CardGame.Core.AI
{
    public class AIPlayerController
    {
        public int BotPlayerId { get; }
        private readonly GameEngine _gameEngine;
        public readonly BotSolver Solver;
        private bool _isRunning = false;

        public AIPlayerController(GameEngine gameEngine, int botPlayerId, IAIStrategy? strategy = null, int beamWidth = 4, int maxDepth = 5)
        {
            _gameEngine = gameEngine;
            BotPlayerId = botPlayerId;
            Solver = new BotSolver(_gameEngine, BotPlayerId, strategy ?? new StandardStrategy(), beamWidth, maxDepth);
        }

        public async void StartAutoPlay()
        {
            if (_isRunning) return;
            _isRunning = true;

            while (!_gameEngine.IsGameOver)
            {
                var state = _gameEngine.CurrentState;

                if (state.CurrentPhase == GamePhase.Mulligan)
                {
                    if (!state.PlayersReady.Contains(BotPlayerId))
                    {
                        _gameEngine.ExecuteCommand(new ConfirmMulliganCommand(BotPlayerId, new System.Collections.Generic.List<int>()));
                    }
                    await Task.Delay(100);
                    continue;
                }

                if (state.ActivePlayerId == BotPlayerId)
                {
                    await Task.Delay(500); // Wait a bit before making a move
                    
                    var evaluatedMoves = Solver.FindBestMoves(state);

                    if (evaluatedMoves.Any())
                    {
                        var bestCommand = evaluatedMoves.First().Command;
                        var result = _gameEngine.ExecuteCommand(bestCommand);

                        // Safeguard: if the best move did nothing, end the phase.
                        if (result.NewState == state && !(bestCommand is EndPhaseCommand))
                        {
                            _gameEngine.ExecuteCommand(new EndPhaseCommand(BotPlayerId));
                        }
                    }
                    else
                    {
                        // If AI finds no valid moves, just end the phase.
                        _gameEngine.ExecuteCommand(new EndPhaseCommand(BotPlayerId));
                    }
                }
                else
                {
                    await Task.Delay(100);
                }
            }
            _isRunning = false;
        }
    }
}