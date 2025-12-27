using CardGame.Core.AI.Interfaces;
using CardGame.Core.AI.Logic;
using CardGame.Core.Application;
using CardGame.Core.Commands.Interfaces;
using CardGame.Core.State.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CardGame.Core.AI
{
    internal class SimulationNode
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

    internal class BotSolver
    {
        private readonly GameEngine _engineTemplate;
        private readonly MoveGenerator _moveGenerator;
        private readonly IAIStrategy _strategy;
        private readonly VirtualOpponent _virtualOpponent;
        private readonly int _botId;

        private const int BeamWidth = 5;
        private const int MaxDepth = 3;

        public BotSolver(GameEngine engine, int botId, IAIStrategy strategy)
        {
            _engineTemplate = engine;
            _botId = botId;
            _strategy = strategy;
            _moveGenerator = new MoveGenerator();
            // Używamy fabryki z silnika, aby tworzyć poprawne definicje kart
            _virtualOpponent = new VirtualOpponent(engine.Factory);
        }

        public IGameCommand FindBestMove(GameState rootState)
        {
            var beam = new List<SimulationNode> { new SimulationNode(rootState, null, _strategy.Evaluate(rootState, _botId)) };

            for (int depth = 0; depth < MaxDepth; depth++)
            {
                var nextCandidates = new List<SimulationNode>();

                foreach (var node in beam)
                {
                    bool isBotTurn = node.State.ActivePlayerId == _botId;

                    // Tworzymy symulator raz na węzeł (optymalizacja), ale będziemy resetować jego stan
                    var simulator = new GameEngine(node.State, _engineTemplate.Rng.Seed);

                    if (isBotTurn)
                    {
                        // --- TURA BOTA ---
                        var moves = _moveGenerator.GenerateLegalMoves(node.State, _botId);

                        foreach (var move in moves)
                        {
                            // WAŻNE: Resetujemy stan symulatora do punktu wyjścia przed każdym ruchem!
                            simulator.CurrentState = node.State;

                            var result = simulator.ExecuteCommand(move);

                            // Jeśli ruch był błędny (stan się nie zmienił), pomijamy (chyba że to EndPhase)
                            if (result.NewState == node.State && !(move is CardGame.Core.Commands.Implementations.EndPhaseCommand))
                                continue;

                            float score = _strategy.Evaluate(result.NewState, _botId);
                            var firstMove = (depth == 0) ? move : node.InitialMove;

                            nextCandidates.Add(new SimulationNode(result.NewState, firstMove, score));
                        }
                    }
                    else
                    {
                        // --- TURA PRZECIWNIKA (Przewidywanie) ---
                        try
                        {
                            var stateWithPhantom = _virtualOpponent.InjectPhantomHand(node.State, node.State.ActivePlayerId);
                            var enemyMoves = _moveGenerator.GenerateLegalMoves(stateWithPhantom, node.State.ActivePlayerId);

                            float worstScoreForBot = float.MaxValue;
                            GameState worstStateForBot = node.State;

                            // Sprawdzamy kilka groźnych ruchów wroga
                            foreach (var emove in enemyMoves.Take(5))
                            {
                                // WAŻNE: Resetujemy stan do tego z Phantom Hand!
                                simulator.CurrentState = stateWithPhantom;

                                var res = simulator.ExecuteCommand(emove);

                                if (res.NewState == stateWithPhantom && !(emove is CardGame.Core.Commands.Implementations.EndPhaseCommand))
                                    continue;

                                float currentScore = _strategy.Evaluate(res.NewState, _botId);
                                if (currentScore < worstScoreForBot)
                                {
                                    worstScoreForBot = currentScore;
                                    worstStateForBot = res.NewState;
                                }
                            }
                            nextCandidates.Add(new SimulationNode(worstStateForBot, node.InitialMove, worstScoreForBot));
                        }
                        catch
                        {
                            // Jeśli symulacja wroga się wywali (np. błąd ID), ignorujemy tę gałąź
                        }
                    }
                }

                beam = nextCandidates
                    .OrderByDescending(n => n.Score)
                    .Take(BeamWidth)
                    .ToList();

                if (!beam.Any()) break;
            }

            var best = beam.OrderByDescending(n => n.Score).FirstOrDefault();
            return best?.InitialMove ?? new CardGame.Core.Commands.Implementations.EndPhaseCommand(_botId);
        }
    }
}