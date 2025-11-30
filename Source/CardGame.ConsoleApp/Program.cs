using System;
using System.Collections.Generic;
using System.Linq;
using CardGame.Core.Application;
using CardGame.Core.Cards.Models;
using CardGame.Core.Commands.Implementations;
using CardGame.Core.State.Enums;  
using CardGame.Core.State.Models;

namespace CardGame.ConsoleApp
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8; // Żeby ładnie wyświetlało znaki
            Console.WriteLine("=== TEST ZAGRYWANIA JEDNOSTKI ===");

            // 1. STWORZENIE KARTY
            // Definicja: Krwawy Wilk (Atak 2, Życie 2, Koszt 1 Krwi)
            var wolfStats = new CardStats(attack: 2, health: 2, bloodCost: 1);
            var wolfDef = new CardDefinition("wolf_01", "Krwawy Wilk", wolfStats);

            // Instancja: Konkretny egzemplarz tej karty (ID 100, Właściciel: Gracz 1)
            var wolfCard = new CardInstance(instanceId: 100, ownerPlayerId: 1, definition: wolfDef);

            // 2. PRZYGOTOWANIE STANU GRY (Manualne, żeby dać kartę do ręki)
            // Normalnie GameState.Initial daje puste ręce, więc robimy to "ręcznie"

            var handA = new List<CardInstance> { wolfCard }; // Gracz A ma Wilka w ręce
            var deckA = new List<CardInstance>();

            // Tworzymy Gracza A "na bogato" - ma kartę i 1 punkt Krwi
            var playerA = new PlayerState(1, 20, 1, 1, handA, deckA, new List<CardInstance>());
            var playerB = PlayerState.Initial(2, new List<CardInstance>()); // Gracz B pusty

            // Składamy GameState
            var initialState = new GameState(
                turnNumber: 1,
                currentPhase: GamePhase.UnitOnly,
                activePlayerId: 1,
                board: BoardState.Empty(),
                playerA: playerA,
                playerB: playerB
            );

            // 3. ODPALENIE SILNIKA
            var engine = new GameEngine(initialState);

            // Wyświetl stan PRZED zagraniem
            Console.WriteLine("\n--- STAN PRZED RUCHEM ---");
            PrintBoard(engine.CurrentState);
            PrintPlayer(engine.CurrentState.PlayerA, "Gracz A");

            // 4. WYKONANIE RUCHU (PlayUnitCommand)
            Console.WriteLine("\n[AKCJA] Gracz A zagrywa 'Krwawego Wilka' na Linię 0...");

            try
            {
                var playCommand = new PlayUnitCommand(
                    playerId: 1,
                    cardInstanceId: 100, // ID naszego wilka
                    targetLineIndex: 0   // Pierwsza linia
                );

                engine.ExecuteCommand(playCommand);

                // Wyświetl stan PO zagraniu
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("\n>>> SUKCES! KARTA ZAGRANA <<<");
                Console.ResetColor();

                Console.WriteLine("\n--- STAN PO RUCHU ---");
                PrintBoard(engine.CurrentState);
                PrintPlayer(engine.CurrentState.PlayerA, "Gracz A");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n[BŁĄD] Nie udało się zagrać karty: {ex.Message}");
                Console.ResetColor();
            }

            Console.ReadKey();
        }

        // --- Metody pomocnicze do wyświetlania w konsoli ---

        static void PrintPlayer(PlayerState p, string name)
        {
            Console.WriteLine($"{name} [HP: {p.Health} | Krew: {p.CurrentBlood}/{p.MaxBlood}]");
            Console.WriteLine($"  Ręka: {p.Hand.Count} kart");
            foreach (var c in p.Hand)
            {
                Console.WriteLine($"   - {c.Definition.Name} (Koszt: {c.CurrentStats.BloodCost})");
            }
        }

        static void PrintBoard(GameState state)
        {
            Console.WriteLine("PLANASZA:");
            for (int i = 0; i < 4; i++)
            {
                var line = state.Board.Lines[i];
                var p1Unit = line.Player1Unit != null ? $"[{line.Player1Unit.Definition.Name}]" : "[PUSTO]";
                var p2Unit = line.Player2Unit != null ? $"[{line.Player2Unit.Definition.Name}]" : "[PUSTO]";

                Console.WriteLine($"  Linia {i}: {p1Unit} vs {p2Unit}");
            }
        }
    }
}