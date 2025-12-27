using System;
using System.Linq;
using System.Threading.Tasks;
using CardGame.Core.AI.Interfaces;
using CardGame.Core.AI.Strategies;
using CardGame.Core.Application;
using CardGame.Core.Commands.Implementations;
using CardGame.Core.State.Enums; // Dodano namespace do Enumów

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
            var aiStrategy = strategy ?? new StandardStrategy();
            _solver = new BotSolver(_gameEngine, BotPlayerId, aiStrategy);
        }

        public async void StartAutoPlay()
        {
            if (_isRunning) return;
            _isRunning = true;

            while (!_gameEngine.IsGameOver)
            {
                var state = _gameEngine.CurrentState;

                // Sprawdzamy, czy ten Bot jest AKTYWNYM graczem
                if (state.ActivePlayerId == BotPlayerId)
                {
                    // SCENARIUSZ 1: Faza Walki (Combat)
                    // W tej fazie nie ma decyzji, ale musimy wysłać sygnał "Dalej", żeby silnik policzył obrażenia.
                    if (state.CurrentPhase == GamePhase.Combat)
                    {
                        Console.WriteLine($"[AI {BotPlayerId}] Faza Walki... Oczekiwanie na wynik.");
                        await Task.Delay(1500); // Czas dla widza na zobaczenie stołu przed walką

                        // Pychamy grę do przodu
                        _gameEngine.ExecuteCommand(new EndPhaseCommand(BotPlayerId));
                    }
                    // SCENARIUSZ 2: Normalna tura (Unit/Action)
                    else
                    {
                        await Task.Delay(1000); // Symulacja myślenia

                        try
                        {
                            var bestMove = await Task.Run(() => _solver.FindBestMove(state));
                            Console.WriteLine($"[AI {BotPlayerId}] Zagrywam: {bestMove.GetType().Name}");

                            var stateBefore = state;
                            var result = _gameEngine.ExecuteCommand(bestMove);

                            // Zabezpieczenie przed pętlą błędów
                            if (result.NewState == stateBefore && !(bestMove is EndPhaseCommand))
                            {
                                Console.WriteLine($"[AI {BotPlayerId} WARN] Ruch nielegalny/pusty. Pasuję.");
                                _gameEngine.ExecuteCommand(new EndPhaseCommand(BotPlayerId));
                                await Task.Delay(500);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"[AI ERROR] {ex.Message}");
                            Console.ResetColor();
                            // Ratunkowe zakończenie tury
                            try { _gameEngine.ExecuteCommand(new EndPhaseCommand(BotPlayerId)); } catch { }
                        }
                    }
                }
                else
                {
                    // Nie moja tura - czekam
                    await Task.Delay(500);
                }
            }

            _isRunning = false;
        }
    }
}