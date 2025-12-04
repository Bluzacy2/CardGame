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
            Console.WriteLine("=== TEST INTEGRACYJNY: JSON + UNKILLABLE + DEATH ===");

            // 1. PRZYGOTOWANIE PLIKU JSON "W LOCIE"
            // Tworzymy tymczasowy plik z dwiema kartami:
            // - Feniks: Ma keyword Unkillable, 5 Ataku, 1 HP
            // - Zombie: Zwykły, 5 Ataku, 1 HP
            // Obydwa zginą w walce, ale zadzieje się co innego.

            string tempJsonPath = "test_cards_temp.json";
            string jsonContent = @"
            [
              {
                ""Id"": 888,
                ""Name"": ""Nieśmiertelny Feniks"",
                ""Type"": ""Unit"",
                ""Cost"": 1,
                ""Attack"": 5,
                ""Health"": 1,
                ""Keywords"": [""Unkillable""]
              },
              {
                ""Id"": 999,
                ""Name"": ""Zwykły Zombie"",
                ""Type"": ""Unit"",
                ""Cost"": 1,
                ""Attack"": 5,
                ""Health"": 1,
                ""Keywords"": []
              }
            ]";
            File.WriteAllText(tempJsonPath, jsonContent);

            // 2. ŁADOWANIE BIBLIOTEKI
            Console.WriteLine("\n[1] Ładowanie kart z JSON...");
            try
            {
                CardLibrary.Instance.LoadFromJson(tempJsonPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BŁĄD] Nie udało się załadować JSON: {ex.Message}");
                return;
            }

            // 3. TWORZENIE INSTANCJI KART
            var phoenixDef = CardLibrary.Instance.CreateDefinition(888);
            var zombieDef = CardLibrary.Instance.CreateDefinition(999);

            // Gracz 1 ma Feniksa (ID instancji 100)
            var p1Phoenix = new CardInstance(100, 1, phoenixDef);

            // Gracz 2 ma Zombie (ID instancji 200)
            var p2Zombie = new CardInstance(200, 2, zombieDef);

            // 4. USTAWIANIE PLANSZY (WALKA)
            // Stawiamy ich naprzeciwko siebie na Linii 0
            var line0 = new Line(0, p1Phoenix, p2Zombie);

            var board = new BoardState(new List<Line> {
                line0, Line.Empty(1), Line.Empty(2), Line.Empty(3)
            });

            // Gracz 1 ma pustą rękę (żebyśmy widzieli czy Feniks wróci)
            var playerA = PlayerState.Initial(1, new List<CardInstance>());
            // Gracz 2 ma pusty cmentarz (żebyśmy widzieli czy Zombie tam trafi)
            var playerB = PlayerState.Initial(2, new List<CardInstance>());

            var state = new GameState(1, GamePhase.Combat, 1, board, playerA, playerB);
            var engine = new GameEngine(state);

            Console.WriteLine("\n[2] Sytuacja Przed Walką:");
            PrintLine(engine.CurrentState.Board.Lines[0]);
            Console.WriteLine($"   Gracz 1 Ręka: {engine.CurrentState.PlayerA.Hand.Count}");
            Console.WriteLine($"   Gracz 2 Cmentarz: {engine.CurrentState.PlayerB.DiscardPile.Count}");

            // 5. EGZEKUCJA WALKI
            Console.WriteLine("\n[3] ROZPOCZĘCIE WALKI...");
            // Używamy EndPhase w fazie Combat, co odpala logikę walki i śmierci
            engine.ExecuteCommand(new EndPhaseCommand(1));

            // 6. WERYFIKACJA WYNIKÓW
            Console.WriteLine("\n[4] Sytuacja Po Walce:");
            var finalState = engine.CurrentState;
            var finalLine = finalState.Board.Lines[0];

            // A. Plansza
            PrintLine(finalLine);
            if (finalLine.Player1Unit == null && finalLine.Player2Unit == null)
                Console.WriteLine("   [OK] Plansza jest pusta (obydwa 'zginęły').");
            else
                Console.WriteLine("   [BŁĄD] Ktoś został na planszy!");

            // B. Unkillable (Gracz 1)
            var p1Hand = finalState.PlayerA.Hand;
            if (p1Hand.Count == 1 && p1Hand[0].Definition.Id == "888")
                Console.WriteLine($"   [OK] UNKILLABLE ZADZIAŁAŁ! Feniks wrócił do ręki Gracza 1.");
            else
                Console.WriteLine($"   [BŁĄD] Feniksa nie ma w ręce! Liczba kart: {p1Hand.Count}");

            // C. Normal Death (Gracz 2)
            var p2Discard = finalState.PlayerB.DiscardPile; // Upewnij się że to nazwałeś DiscardPile w PlayerState
            if (p2Discard.Count == 1 && p2Discard[0].Definition.Id == "999")
                Console.WriteLine($"   [OK] ŚMIERĆ ZADZIAŁAŁA! Zombie trafił na cmentarz Gracza 2.");
            else
                Console.WriteLine($"   [BŁĄD] Zombie nie ma na cmentarzu! Liczba kart: {p2Discard.Count}");

            // Sprzątanie pliku tymczasowego
            File.Delete(tempJsonPath);
            Console.ReadKey();
        }

        static void PrintLine(Line line)
        {
            string u1 = line.Player1Unit != null ? $"{line.Player1Unit.Definition.Name} (HP:{line.Player1Unit.CurrentStats.Health})" : "[PUSTO]";
            string u2 = line.Player2Unit != null ? $"{line.Player2Unit.Definition.Name} (HP:{line.Player2Unit.CurrentStats.Health})" : "[PUSTO]";
            Console.WriteLine($"   Linia 0: {u1}  VS  {u2}");
        }
    }
}