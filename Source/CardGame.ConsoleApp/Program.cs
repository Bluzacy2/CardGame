using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CardGame.Core.Application;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Factories;
using CardGame.Core.Cards.Models;
using CardGame.Core.Commands.Implementations;
using CardGame.Core.Decks;
using CardGame.Core.State.Enums;
using CardGame.Core.State.Models;

namespace CardGame.ConsoleApp
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine("=== TEST INTEGRACYJNY: POLAR BEAR ABSORPTION ===");

            // 1. WCZYTYWANIE DANYCH (Standardowa funkcja)
            LoadGameData();

            // 2. SETUP SILNIKA
            var rng = new DeterministicRng(123);
            var factory = new CardFactory(CardLibrary.Instance, rng);

            // Tworzymy karty z biblioteki (teraz wczytanej z cards.json)
            try
            {
                var crusader = factory.CreateCard(8, 1);
                var polarBear = factory.CreateCard(10, 1);
                var moder = factory.CreateCard(11, 1);
                var corpseEater = factory.CreateCard(9, 1);

                // Stan początkowy
                var board = BoardState.Empty().WithUnitPlacedAt(0, 1, crusader);
                var pA = PlayerState.Initial(1, new List<CardInstance>())
                    .With(maxBlood: 10, currentBlood: 10)
                    .WithCardAddedToHand(polarBear)
                    .WithCardAddedToHand(moder)
                    .WithCardAddedToHand(corpseEater);

                var state = new GameState(1, GamePhase.UnitOnly, 1, board, pA, PlayerState.Initial(2, new List<CardInstance>()));
                var engine = new GameEngine(state, seed: 123);

                Console.WriteLine($"\n--- START ---\nStół: {crusader.Definition.Name} (2/3)");

                // 3. ZRANIONY CRUSADER (Test Pancerza)
                var dmgCalc = new CardGame.Core.GameRules.Damage.DamageCalculator();
                var ctx = new CardGame.Core.GameRules.Damage.DamageContext(null, crusader, 2, CardGame.Core.GameRules.Damage.DamageType.Combat);
                int finalDmg = dmgCalc.CalculateFinalDamage(ctx);

                var damagedCrusader = crusader.TakeDamage(finalDmg);
                engine.CurrentState = engine.CurrentState.UpdateBoard(engine.CurrentState.Board.UpdateUnit(damagedCrusader));
                Console.WriteLine($"[INFO] Crusader oberwał. Aktualne staty: {damagedCrusader.CurrentStats.Attack}/{damagedCrusader.CurrentStats.Health}");

                // 4. ZAGRANIE NIEDŹWIEDZIA
                Console.WriteLine("\n[AKCJA] Zagrywam Polar Beara na Crusadera...");

                // Polar Bear (2/3) zjada Crusadera (2/2) -> Powinien mieć 4/5
                var cmd = new PlayUnitCommand(1, polarBear.InstanceId, 1, selectedTargetId: crusader.InstanceId);
                engine.ExecuteCommand(cmd);

                // 5. WERYFIKACJA
                Console.WriteLine("\n--- WERYFIKACJA STATYSTYK ---");
                var bearOnBoard = engine.CurrentState.Board.Lines[1].Player1Unit;

                if (bearOnBoard != null)
                {
                    int atk = bearOnBoard.CurrentStats.Attack;
                    int hp = bearOnBoard.CurrentStats.Health;

                    Console.WriteLine($"Polar Bear: {atk}/{hp} (Oczekiwano: 4/5)");

                    if (atk == 4 && hp == 5)
                        Console.WriteLine("[SUKCES] AbsorbStats zadziałało poprawnie!");
                    else
                        Console.WriteLine("[BŁĄD] Złe statystyki.");
                }

                // Sprawdzenie czy reszta comba zadziałała (Corpse Eater)
                var line0 = engine.CurrentState.Board.Lines[0].Player1Unit;
                if (line0?.Definition.Name == "Corpse Eater")
                    Console.WriteLine("[SUKCES] Corpse Eater wskoczył na miejsce ofiary.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BŁĄD KRYTYCZNY] {ex.Message}");
                Console.WriteLine("Sprawdź czy plik cards.json ma ustawione 'Copy to Output Directory'!");
            }

            Console.ReadKey();
        }

        // --- STAŁA FUNKCJA DO ŁADOWANIA DANYCH ---
        private static void LoadGameData()
        {
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string cardsPath = Path.Combine(basePath, "Data", "Cards", "cards.json");
            string decksPath = Path.Combine(basePath, "Data", "Decks");

            Console.WriteLine($"[INIT] Szukam danych w: {basePath}");

            if (File.Exists(cardsPath))
            {
                CardLibrary.Instance.LoadFromJson(cardsPath);
                // DeckRepository.Instance.LoadDecksFromDirectory(decksPath); // Opcjonalnie, jeśli używamy talii
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[BŁĄD] Nie znaleziono pliku cards.json!");
                Console.WriteLine("Upewnij się, że w Visual Studio kliknąłeś na plik -> Properties -> Copy to Output Directory: Copy Always");
                Console.ResetColor();
                throw new FileNotFoundException("Brak pliku kart.");
            }
        }
    }
}