using System;
using System.Collections.Generic;
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
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine("=== TEST WALKI (IMMUTABLE) ===");

            // 1. DEFINICJE KART
            // Wilk: 2 Atak / 2 HP
            var wolfStats = new CardStats(2, 2, 1);
            var wolfDef = new CardDefinition("wolf", "Krwawy Wilk", wolfStats);

            // Goblin: 1 Atak / 1 HP
            var goblinStats = new CardStats(1, 1, 1);
            var goblinDef = new CardDefinition("goblin", "Słaby Goblin", goblinStats);

            // 2. INSTANCJE
            var p1Wolf = new CardInstance(100, 1, wolfDef);   // Gracz 1
            var p2Goblin = new CardInstance(200, 2, goblinDef); // Gracz 2

            // 3. SETUP PLANSZY "NA SZTYWNO"
            // Ustawiamy je naprzeciwko siebie na Linii 0
            var line0 = new Line(0, p1Wolf, p2Goblin);

            var lines = new List<Line> {
                line0,
                Line.Empty(1), Line.Empty(2), Line.Empty(3)
            };
            var initialBoard = new BoardState(lines);

            // 4. SETUP STANU GRY (Faza Combat)
            // Ustawiamy od razu fazę COMBAT, żeby nie klikać "End Phase" 5 razy
            var initialState = new GameState(
                turnNumber: 1,
                currentPhase: GamePhase.Combat, // <--- JESTEŚMY TUŻ PRZED WALKĄ
                activePlayerId: 1,
                board: initialBoard,
                playerA: PlayerState.Initial(1, new List<CardInstance>()),
                playerB: PlayerState.Initial(2, new List<CardInstance>())
            );

            var engine = new GameEngine(initialState);

            Console.WriteLine("\n--- SYTUACJA PRZED WALKĄ ---");
            PrintBoard(engine.CurrentState);

            // 5. WYKONANIE WALKI
            // W fazie Combat jedyna komenda to EndPhase, która odpala logikę walki
            Console.WriteLine("\n[AKCJA] Rozpoczynamy walkę (EndPhaseCommand)...");

            var combatCmd = new EndPhaseCommand(1);
            engine.ExecuteCommand(combatCmd);

            Console.WriteLine("\n--- SYTUACJA PO WALCE ---");
            PrintBoard(engine.CurrentState);

            // Weryfikacja
            var l0 = engine.CurrentState.Board.Lines[0];

            if (l0.Player1Unit != null && l0.Player1Unit.CurrentStats.Health == 1)
                Console.WriteLine("\n[OK] Wilk przeżył i ma 1 HP.");
            else
                Console.WriteLine("\n[BŁĄD] Stan Wilka jest niepoprawny!");

            if (l0.Player2Unit == null)
                Console.WriteLine("[OK] Goblin zginął (zniknął z planszy).");
            else
                Console.WriteLine($"[BŁĄD] Goblin nadal żyje! HP: {l0.Player2Unit.CurrentStats.Health}");

            Console.ReadKey();
        }

        static void PrintBoard(GameState state)
        {
            var line = state.Board.Lines[0];
            string p1Txt = line.Player1Unit != null
                ? $"{line.Player1Unit.Definition.Name} ({line.Player1Unit.CurrentStats.Attack}/{line.Player1Unit.CurrentStats.Health})"
                : "[TRUP/PUSTO]";

            string p2Txt = line.Player2Unit != null
                ? $"{line.Player2Unit.Definition.Name} ({line.Player2Unit.CurrentStats.Attack}/{line.Player2Unit.CurrentStats.Health})"
                : "[TRUP/PUSTO]";

            Console.WriteLine($"Linia 0: {p1Txt}  VS  {p2Txt}");
        }
    }
}