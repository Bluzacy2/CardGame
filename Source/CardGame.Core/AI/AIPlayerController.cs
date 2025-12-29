using System;
using System.Linq;
using System.Threading.Tasks;
using CardGame.Core.AI.Interfaces;
using CardGame.Core.AI.Strategies;
using CardGame.Core.Application;
using CardGame.Core.Commands.Implementations;
using CardGame.Core.State.Enums;

namespace CardGame.Core.AI
{
    public class AIPlayerController
    {
        public int BotPlayerId { get; }
        private readonly GameEngine _gameEngine;
        private readonly BotSolver _solver;
        private bool _isRunning = false;

        public AIPlayerController(GameEngine gameEngine, int botPlayerId, IAIStrategy? strategy = null)
        {
            _gameEngine = gameEngine;
            BotPlayerId = botPlayerId;
            _solver = new BotSolver(_gameEngine, BotPlayerId, strategy ?? new StandardStrategy());
        }

        public async void StartAutoPlay()
        {
            if (_isRunning) return;
            _isRunning = true;

            while (!_gameEngine.IsGameOver)
            {
                var state = _gameEngine.CurrentState;

                // LOGIKA MULLIGANA: Obaj boty muszą wysłać komendę, niezależnie od ActivePlayerId
                if (state.CurrentPhase == GamePhase.Mulligan)
                {
                    if (!state.PlayersReady.Contains(BotPlayerId))
                    {
                        await Task.Delay(500);
                        _gameEngine.ExecuteCommand(new ConfirmMulliganCommand(BotPlayerId, new System.Collections.Generic.List<int>()));
                    }
                    await Task.Delay(200);
                    continue;
                }

                // NORMALNA TURA: Tylko jeśli bot jest aktywny
                if (state.ActivePlayerId == BotPlayerId)
                {
                    if (state.CurrentPhase == GamePhase.Combat)
                    {
                        return; 
                    }
                    else
                    {
                        await Task.Delay(800);
                        try
                        {
                            var bestMove = await Task.Run(() => _solver.FindBestMove(state));
                            var result = _gameEngine.ExecuteCommand(bestMove);

                            // Zabezpieczenie: jeśli ruch nic nie zmienił, a nie był to koniec fazy, wymuś koniec fazy
                            if (result.NewState == state && !(bestMove is EndPhaseCommand))
                            {
                                _gameEngine.ExecuteCommand(new EndPhaseCommand(BotPlayerId));
                            }
                        }
                        catch
                        {
                            _gameEngine.ExecuteCommand(new EndPhaseCommand(BotPlayerId));
                        }
                    }
                }
                else
                {
                    await Task.Delay(200); // Czekaj na swoją kolej
                }
            }
            _isRunning = false;
        }
    }
}