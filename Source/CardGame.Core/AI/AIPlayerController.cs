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
using CardGame.Core.AI.Logic.Mcts; // Upewnij siê, ¿e masz ten namespace

namespace CardGame.Core.AI
{
    public enum AISolverType
    {
        BeamSearch, // Stary, szybki solver heurystyczny
        MCTS        // Nowy, zaawansowany solver Monte Carlo
    }

    public class AIPlayerController
    {
        public int BotPlayerId { get; }
        private readonly GameEngine _gameEngine;

        // Dwa solvery - u¿ywamy jednego w zale¿noœci od konfiguracji
        public readonly BotSolver BeamSolver;
        private readonly MctsSolver _mctsSolver;

        private readonly AISolverType _solverType;
        private bool _isRunning = false;

        public AIPlayerController(GameEngine gameEngine, int botPlayerId, IAIStrategy strategy, AISolverType solverType)
        {
            _gameEngine = gameEngine;
            BotPlayerId = botPlayerId;
            _solverType = solverType;

            // Inicjalizacja obu (lub leniwa inicjalizacja, ale tu dla uproszczenia oba)
            // Beam Search: Width 4, Depth 5 (standardowe ustawienia)
            BeamSolver = new BotSolver(_gameEngine, BotPlayerId, strategy, beamWidth: 4, maxDepth: 5);

            // MCTS u¿ywa tej samej strategii do oceny liœci
            _mctsSolver = new MctsSolver(_gameEngine, BotPlayerId, strategy);
        }

        public async void StartAutoPlay()
        {
            if (_isRunning) return;
            _isRunning = true;

            while (!_gameEngine.IsGameOver)
            {
                var state = _gameEngine.CurrentState;

                // 1. FAZA MULLIGAN (Wymiana kart)
                if (state.CurrentPhase == GamePhase.Mulligan)
                {
                    if (!state.PlayersReady.Contains(BotPlayerId))
                    {
                        // Tu mo¿na dodaæ logikê inteligentnego mulliganu w przysz³oœci
                        _gameEngine.ExecuteCommand(new ConfirmMulliganCommand(BotPlayerId, new System.Collections.Generic.List<int>()));
                    }
                    await Task.Delay(100);
                    continue;
                }

                // 2. TURA BOTA
                if (state.ActivePlayerId == BotPlayerId)
                {
                    // Symulacja czasu myœlenia (wa¿ne dla UX)
                    await Task.Delay(500);

                    IGameCommand bestCommand = null;
                    string moveLog = "";

                    // --- WYBÓR ALGORYTMU ---
                    if (_solverType == AISolverType.MCTS)
                    {
                        // MCTS: Myœli przez 1.5 sekundy
                        var bestMove = _mctsSolver.FindBestMove(state, thinkingTimeMs: 1500);
                        bestCommand = bestMove.Command;
                        moveLog = $"[MCTS] Score: {bestMove.Score:F2} | {bestMove.DeepReasoning}";
                    }
                    else
                    {
                        // BEAM SEARCH (Stary): Dzia³a natychmiastowo
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
                            moveLog = "[BEAM] Brak ruchów -> EndPhase";
                        }
                    }

                    // --- WYKONANIE RUCHU ---
                    Console.WriteLine($"[P{BotPlayerId}] {moveLog}"); // Logowanie do konsoli (opcjonalne)

                    var result = _gameEngine.ExecuteCommand(bestCommand);

                    // Zabezpieczenie: Jeœli ruch nic nie zmieni³ (nielegalny/b³¹d) i nie jest to EndPhase, wymuœ koniec tury
                    // Zapobiega nieskoñczonym pêtlom, gdy bot próbuje zagraæ kartê, której nie mo¿e.
                    if (result.NewState == state && !(bestCommand is EndPhaseCommand))
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"[CRITICAL] Bot P{BotPlayerId} utkn¹³! Wymuszam koniec tury.");
                        Console.ResetColor();
                        _gameEngine.ExecuteCommand(new EndPhaseCommand(BotPlayerId));
                    }
                }
                else
                {
                    // Tura przeciwnika - czekaj
                    await Task.Delay(100);
                }
            }
            _isRunning = false;
        }
    }
}