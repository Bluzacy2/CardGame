using CardGame.Core.Application;
using CardGame.Core.Cards.Models;
using CardGame.Core.Commands.Implementations;
using CardGame.Core.State.Models;
using CardGame.Core.State.Models;
using System;
using System.Collections.Generic;

namespace CardGame.ConsoleApp
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== START SYMULACJI ===");

            // 1. Przygotowanie danych (puste talie na start)
            var deckA = new List<CardInstance>();
            var deckB = new List<CardInstance>();

            // 2. Tworzymy stan początkowy (Zaczyna Gracz 1)
            var initialState = GameState.Initial(1, deckA, deckB);

            // 3. Odpalamy Silnik
            var engine = new GameEngine(initialState);

            PrintState(engine.CurrentState);

            // --- SYMULACJA ROZGRYWKI ---

            // Próbujemy przejść przez kilka faz, klikając ciągle "End Phase"
            // Symulujemy 10 kroków (powinno to pokryć około 2 pełne tury)

            for (int i = 0; i < 10; i++)
            {
                Console.WriteLine("\n[AKCJA] Gracz klika 'End Phase'...");

                // Tworzymy komendę. 
                // WAŻNE: Musimy wiedzieć, KTÓRY gracz ma prawo kliknąć.
                // Silnik odrzuci komendę, jeśli kliknie zły gracz.
                int activePlayer = engine.CurrentState.ActivePlayerId;

                var command = new EndPhaseCommand(activePlayer);

                engine.ExecuteCommand(command);

                PrintState(engine.CurrentState);
            }

            Console.WriteLine("\n=== KONIEC SYMULACJI ===");
            Console.ReadKey();
        }

        static void PrintState(GameState state)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"STATUS: Tura {state.TurnNumber} | Faza: {state.CurrentPhase} | Aktywny: Gracz {state.ActivePlayerId}");
            Console.ResetColor();
        }
    }
}