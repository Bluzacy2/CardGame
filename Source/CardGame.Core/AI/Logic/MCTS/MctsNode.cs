using System;
using System.Collections.Generic;
using System.Linq;
using CardGame.Core.Commands.Interfaces;
using CardGame.Core.State.Models;

namespace CardGame.Core.AI.Logic.Mcts
{
    public class MctsNode
    {
        public GameState State { get; }
        public MctsNode? Parent { get; }
        public IGameCommand? MoveEntered { get; } // Ruch, który doprowadził do tego stanu
        public int PlayerIdJustMoved { get; } // Gracz, który wykonał ruch
        public List<MctsNode> Children { get; } = new();
        public List<IGameCommand> UntriedMoves { get; private set; }

        public int Visits { get; private set; }
        public double Score { get; private set; } // Skumulowany wynik (dla gracza z punktu widzenia korzenia)

        public MctsNode(GameState state, MctsNode? parent, IGameCommand? move, int playerIdJustMoved, MoveGenerator moveGenerator)
        {
            State = state;
            Parent = parent;
            MoveEntered = move;
            PlayerIdJustMoved = playerIdJustMoved;

            // Generujemy możliwe ruchy dla gracza, który MA TERAZ RUCH w stanie State
            UntriedMoves = moveGenerator.GenerateLegalMoves(state, state.ActivePlayerId);
        }

        public bool IsFullyExpanded => UntriedMoves.Count == 0;
        public bool IsTerminal => State.PlayerA.Health <= 0 || State.PlayerB.Health <= 0;

        public void Update(double result)
        {
            Visits++;
            Score += result;
        }

        // UCT (Upper Confidence Bound for Trees) - wzór na wybór najlepszego dziecka
        public MctsNode GetBestChild(double explorationParameter = 1.41)
        {
            // Sortujemy, żeby znaleźć ten z największą wartością UCT
            return Children.OrderByDescending(child =>
            {
                // Ważne: Jeśli to była tura przeciwnika, wynik jest odwrotny dla nas
                double exploitation = child.Score / child.Visits;
                double exploration = explorationParameter * Math.Sqrt(Math.Log(Visits) / child.Visits);
                return exploitation + exploration;
            }).First();
        }
    }
}