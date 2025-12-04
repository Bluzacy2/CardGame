using System;
using System.Collections.Generic;
using System.IO;
using CardGame.Core.Application;
using CardGame.Core.Cards.Data;
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
            Console.WriteLine("=== TEST INFORMER (MARKED) ===");

            // 1. JSON
            string jsonContent = @"
            [
              {
                ""Id"": 50, ""Name"": ""Informer"", ""Type"": ""Unit"", ""Cost"": 1, ""Attack"": 2, ""Health"": 2,
                ""Effects"": [ 
                    { 
                        ""Trigger"": ""OnPlayed"", 
                        ""Actions"": [ 
                            { ""Type"": ""ApplyStatus"", ""Target"": ""TargetEnemyUnit"", ""StringParam"": ""Marked"" } 
                        ] 
                    } 
                ]
              },
              {
                ""Id"": 60, ""Name"": ""Ofiara"", ""Type"": ""Unit"", ""Cost"": 1, ""Attack"": 0, ""Health"": 3
              }
            ]";
            File.WriteAllText("informer_test.json", jsonContent);
            CardLibrary.Instance.LoadFromJson("informer_test.json");

            // 2. SETUP
            var pA = PlayerState.Initial(1, new List<CardInstance>()).WithBloodSpent(0);
            var pB = PlayerState.Initial(2, new List<CardInstance>());

            // Stawiamy Ofiarę (3 HP) na planszy dla Gracza 2
            var victim = new CardInstance(200, 2, CardLibrary.Instance.CreateDefinition(60));
            var board = BoardState.Empty().WithUnitPlacedAt(0, 2, victim);

            var state = new GameState(1, GamePhase.UnitOnly, 1, board, pA, pB);
            var engine = new GameEngine(state);

            // Dajemy Informera do ręki
            var informer = new CardInstance(100, 1, CardLibrary.Instance.CreateDefinition(50));
            engine.CurrentState = engine.CurrentState.UpdatePlayer(
                engine.CurrentState.PlayerA.WithCardAddedToHand(informer)
            );

            // 3. ZAGRYWAMY INFORMERA (Powinien oznaczyć Ofiarę)
            Console.WriteLine("\n[AKCJA] Zagrywam Informera...");
            engine.ExecuteCommand(new PlayUnitCommand(1, 100, 0)); // Linia 0

            // Sprawdzenie czy Ofiara jest Marked
            var victimOnBoard = engine.CurrentState.Board.Lines[0].Player2Unit;
            if (victimOnBoard.CurrentStats.Keywords.Contains(Keyword.Marked))
                Console.WriteLine("[SUKCES] Ofiara ma status MARKED!");
            else
                Console.WriteLine("[BŁĄD] Ofiara NIE JEST oznaczona.");

            // 4. WALKA (Informer atakuje Ofiarę)
            // Informer ma 2 ataku. Ofiara ma 3 HP.
            // Bez Marked: 3 - 2 = 1 HP (Przeżywa).
            // Z Marked: 3 - (2*2) = -1 HP (Ginie).

            Console.WriteLine("\n[AKCJA] Faza Walki...");
            engine.ExecuteCommand(new EndPhaseCommand(1)); // UnitOnly -> UnitSpell
            engine.ExecuteCommand(new EndPhaseCommand(2)); // UnitSpell -> SpellOnly
            engine.ExecuteCommand(new EndPhaseCommand(1)); // SpellOnly -> Combat

            // --- BRAKOWAŁO TEJ LINII: ---
            Console.WriteLine("[AKCJA] Rozstrzyganie Walki (wewnątrz Combat Phase)...");
            engine.ExecuteCommand(new EndPhaseCommand(1)); // Combat -> Rozstrzygnij -> Nowa Tura)

            // 5. WERYFIKACJA
            var line = engine.CurrentState.Board.Lines[0];
            if (line.Player2Unit == null)
            {
                Console.WriteLine("[SUKCES] Ofiara zginęła (2 dmg * 2 = 4 dmg > 3 HP). Mechanika Marked działa!");
            }
            else
            {
                Console.WriteLine($"[BŁĄD] Ofiara przeżyła z {line.Player2Unit.CurrentStats.Health} HP.");
            }

            Console.ReadKey();
        }
    }
}