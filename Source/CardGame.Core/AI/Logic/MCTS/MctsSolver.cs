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
    public class MctsSolver
    {
        private readonly GameEngine _engineTemplate;
        private readonly int _botId;
        private readonly MoveGenerator _moveGenerator;
        private readonly VirtualOpponent _virtualOpponent;
        private readonly IAIStrategy _heuristic;
        private readonly DeterministicRng _rng;

        // Głębokość symulacji - im głębiej, tym dokładniej, ale wolniej. 20 to solidny kompromis.
        private const int SIMULATION_DEPTH = 20;
        // Stała eksploracji - 1.41 (pierwiastek z 2) to teoretyczne optimum.
        private const double EXPLORATION_CONSTANT = 1.4142;

        public MctsSolver(GameEngine engine, int botId, IAIStrategy heuristic)
        {
            _engineTemplate = engine;
            _botId = botId;
            _heuristic = heuristic;
            _moveGenerator = new MoveGenerator();
            _virtualOpponent = new VirtualOpponent(engine.Factory);
            _rng = new DeterministicRng(new Random().Next());
        }

        public EvaluatedMove FindBestMove(GameState rootState, int thinkingTimeMs = 1500)
        {
            // 1. Determinization: Zakładamy konkretną (choć zgadywaną) rękę przeciwnika
            int oppId = rootState.ActivePlayerId == 1 ? 2 : 1;
            GameState determinizedState = _virtualOpponent.InjectRealisticPhantomHand(rootState, oppId);

            var rootNode = new MctsNode(determinizedState, null, null, _botId, _moveGenerator);
            var timer = Stopwatch.StartNew();
            int iterations = 0;

            // 2. Główna pętla MCTS
            while (timer.ElapsedMilliseconds < thinkingTimeMs)
            {
                // A. Selection: Zjazd w dół drzewa do liścia
                MctsNode node = Select(rootNode);

                // B. Expansion: Dodanie nowego węzła, jeśli gra się nie skończyła
                if (!node.IsTerminal && !node.IsFullyExpanded)
                {
                    node = Expand(node);
                }

                // C. Simulation (Rollout): Szybka, losowa gra do końca (lub do głębokości)
                double result = Simulate(node.State);

                // D. Backpropagation: Aktualizacja statystyk w górę drzewa
                Backpropagate(node, result);

                iterations++;
            }

            // 3. Wybór ruchu: Najczęściej odwiedzany węzeł jest najbardziej "zaufany"
            var bestChild = rootNode.Children.OrderByDescending(c => c.Visits).FirstOrDefault();

            if (bestChild == null || bestChild.MoveEntered == null)
            {
                return new EvaluatedMove(new EndPhaseCommand(_botId), -999, "Panic Skip");
            }

            // Statystyka dla człowieka
            float winProb = (float)(bestChild.Score / bestChild.Visits);
            string reasoning = $"MCTS: {iterations} iters | WinChance: {winProb:P1} (Visits: {bestChild.Visits})";

            return new EvaluatedMove(bestChild.MoveEntered, winProb, "MCTS Choice", reasoning);
        }

        private MctsNode Select(MctsNode node)
        {
            // Idziemy w głąb dopóki węzeł ma dzieci i jest w pełni rozwinięty (ma dzieci dla wszystkich ruchów)
            while (!node.IsTerminal && node.IsFullyExpanded && node.Children.Count > 0)
            {
                node = node.GetBestChild(EXPLORATION_CONSTANT);
            }
            return node;
        }

        private MctsNode Expand(MctsNode node)
        {
            // Wybieramy losowy ruch z listy nierozpatrzonych
            var move = node.UntriedMoves[_rng.Next(0, node.UntriedMoves.Count)];
            node.UntriedMoves.Remove(move);

            // Symulujemy ten ruch na kopii silnika
            var engine = new GameEngine(node.State, _engineTemplate.Rng.Seed);
            var result = engine.ExecuteCommand(move);
            var newState = result.NewState;

            // --- SAFETY BREAK: Obsługa PendingInteraction (np. wybór celu) ---
            int safety = 0;
            while (newState.PendingInteraction != null && safety++ < 15)
            {
                var legalTargets = _moveGenerator.GenerateLegalMoves(newState, newState.ActivePlayerId);
                if (!legalTargets.Any()) break;

                // Wybieramy losowy cel
                var randomTarget = legalTargets[_rng.Next(0, legalTargets.Count)];
                var subResult = engine.ExecuteCommand(randomTarget);

                // Jeśli stan się nie zmienił (zakleszczenie), przerywamy
                if (subResult.NewState == newState) break;
                newState = subResult.NewState;
            }

            // Tworzymy nowy węzeł. Ważne: zapisujemy KTO wykonał ten ruch.
            var childNode = new MctsNode(newState, node, move, node.State.ActivePlayerId, _moveGenerator);
            node.Children.Add(childNode);
            return childNode;
        }

        private double Simulate(GameState initialState)
        {
            var currentState = initialState;
            // Używamy losowego seeda w symulacji, żeby każda była inna
            var engine = new GameEngine(currentState, _rng.Next(0,int.MaxValue));
            int depth = 0;

            while (!IsGameOver(currentState) && depth < SIMULATION_DEPTH)
            {
                var moves = _moveGenerator.GenerateLegalMoves(currentState, currentState.ActivePlayerId);
                if (moves.Count == 0) break;

                // --- OPTYMALIZACJA "KILLER MOVE" ---
                // Jeśli mamy ruch wygrywający (Lethal), ZAWSZE go wybieramy.
                // To uczy bota wykańczania przeciwnika i unikania śmierci.
                var lethalMove = FindLethalMove(currentState, moves);

                IGameCommand selectedMove;
                if (lethalMove != null)
                {
                    selectedMove = lethalMove;
                }
                else
                {
                    // Heurystyka Epsilon-Greedy:
                    // 80% szans na zagranie czegoś sensownego (nie-EndPhase), 20% na cokolwiek.
                    // To zapobiega sytuacji, gdzie bot w symulacji ciągle pasuje.
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

                // --- SAFETY BREAK w Symulacji ---
                int safety = 0;
                while (currentState.PendingInteraction != null && safety++ < 15)
                {
                    var targets = _moveGenerator.GenerateLegalMoves(currentState, currentState.ActivePlayerId);
                    if (!targets.Any()) break;

                    var rndTarget = targets[_rng.Next(0, targets.Count)];
                    var subResult = engine.ExecuteCommand(rndTarget);

                    if (subResult.NewState == currentState) break;
                    currentState = subResult.NewState;
                }

                depth++;
            }

            // OCENA STANU KOŃCOWEGO
            // Zwracamy wynik z perspektywy BOTA (_botId)
            // 1.0 = Bot Wygrał / 0.0 = Bot Przegrał
            if (IsGameOver(currentState))
            {
                if (currentState.PlayerA.Health <= 0 && _botId == 2) return 1.0;
                if (currentState.PlayerB.Health <= 0 && _botId == 1) return 1.0;
                return 0.0; // Przegrana
            }

            // Jeśli gra się nie skończyła, używamy heurystyki z DNA
            float heuristicScore = _heuristic.Evaluate(currentState, _botId);

            // Sigmoida spłaszcza wynik (np. -5000 do +5000) do zakresu (0.0 do 1.0)
            // Dzielnik 4000.0f "rozciąga" czułość - bot rozróżni małą przewagę od dużej.
            return Sigmoid(heuristicScore / 4000.0f);
        }

        private void Backpropagate(MctsNode node, double result)
        {
            // result = wynik z perspektywy GŁÓWNEGO BOTA (_botId)
            // 1.0 = Bot wygrywa, 0.0 = Bot przegrywa

            MctsNode? temp = node;
            while (temp != null)
            {
                // LOGIKA NEGAMAX / MINMAX:
                // Każdy węzeł przechowuje statystykę "Jak dobry był ten ruch dla gracza, który go wykonał?"

                double valueForNodeOwner;

                if (temp.PlayerIdJustMoved == _botId)
                {
                    // Jeśli to był nasz ruch, i wynik jest dobry (1.0), to super.
                    valueForNodeOwner = result;
                }
                else
                {
                    // Jeśli to był ruch PRZECIWNIKA, i wynik jest dobry dla NAS (1.0),
                    // to znaczy, że ten ruch był tragiczny dla przeciwnika (0.0).
                    valueForNodeOwner = 1.0 - result;
                }

                temp.Update(valueForNodeOwner);
                temp = temp.Parent;
            }
        }

        // --- Helpery ---

        private IGameCommand? FindLethalMove(GameState s, List<IGameCommand> moves)
        {
            // Prosta heurystyka: Sprawdź, czy atakując Hero można wygrać w tym ruchu
            // Nie symulujemy tutaj całego silnika (za wolno), tylko szybkie sprawdzenie.

            // Jeśli to faza ataku
            if (s.CurrentPhase == GamePhase.Combat) return null;

            int enemyHp = s.GetOpponent(s.ActivePlayerId).Health;

            // Szukamy czaru zadającego obrażenia (bardzo uproszczone)
            foreach (var m in moves)
            {
                if (m is PlaySpellCommand psc)
                {
                    // Tutaj można dodać logikę sprawdzającą, czy karta to np. Fireball w Hero
                    // Wymagałoby dostępu do definicji karty.
                    // Dla wydajności w tym przykładzie pomijam głęboką analizę kart.
                }
            }
            return null;
        }

        private bool IsGameOver(GameState s) => s.PlayerA.Health <= 0 || s.PlayerB.Health <= 0;

        private double Sigmoid(double value) => 1.0 / (1.0 + Math.Exp(-value));
    }
}