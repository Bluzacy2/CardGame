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
            Console.WriteLine("=== MASTER TEST: WERYFIKACJA REFAKTORYZACJI ===");

            try
            {
                // 1. GENEROWANIE KOMPLETNEGO JSONA
                CreateMasterJson();
                LoadGameData();

                // 2. SETUP SILNIKA
                var rng = new DeterministicRng(777);
                var factory = new CardFactory(CardLibrary.Instance, rng);

                // Tworzymy karty
                var crusader = factory.CreateCard(20, 1);
                var polarBear = factory.CreateCard(24, 1);
                var moder = factory.CreateCard(25, 1);
                var corpseEater = factory.CreateCard(21, 1);
                var blackCat = factory.CreateCard(23, 1);
                var criticalThinking = factory.CreateCard(26, 1);
                var mokke = factory.CreateCard(1, 1); // Do talii

                // TWORZENIE STANU
                // Stół: Crusader
                var board = BoardState.Empty().WithUnitPlacedAt(0, 1, crusader);

                // Ręka: Polar Bear, Moder, Corpse Eater, Black Cat, Critical Thinking
                var hand = new List<CardInstance>();
                var pA = PlayerState.Initial(1, new List<CardInstance> { mokke }) // Mokke w talii
                    .With(maxBlood: 100, currentBlood: 100) // Infinite Mana
                    .WithCardAddedToHand(polarBear)
                    .WithCardAddedToHand(moder)
                    .WithCardAddedToHand(corpseEater)
                    .WithCardAddedToHand(blackCat)
                    .WithCardAddedToHand(criticalThinking);

                var state = new GameState(1, GamePhase.UnitAndAction, 1, board, pA, PlayerState.Initial(2, new List<CardInstance>()));
                var engine = new GameEngine(state, seed: 777);

                Console.WriteLine("\n--- START TESTÓW ---\n");

                // =======================================================================
                // SCENARIUSZ 1: COMBO BESTII (Sacrifice -> Hand Trigger -> Death -> Summon)
                // =======================================================================
                Console.WriteLine("[SCENARIUSZ 1] Polar Bear zjada Crusadera...");

                var cmd1 = new PlayUnitCommand(1, polarBear.InstanceId, 1, selectedTargetId: crusader.InstanceId);
                engine.ExecuteCommand(cmd1);

                // --- POPRAWIONA WERYFIKACJA ---

                // 1. Sprawdzamy, czy Crusader zniknął (nie sprawdzamy czy null, tylko czy ID się zmieniło)
                var currentUnitL0 = engine.CurrentState.Board.Lines[0].Player1Unit;
                Assert(currentUnitL0 == null || currentUnitL0.InstanceId != crusader.InstanceId,
                       "Crusader powinien zniknąć!");

                // 2. Sprawdzamy, czy Corpse Eater wskoczył na jego miejsce
                Assert(currentUnitL0 != null && currentUnitL0.Definition.Name == "Corpse Eater",
                       $"Na Linii 0 powinien być Corpse Eater, a jest: {currentUnitL0?.Definition.Name ?? "PUSTO"}");

                // 3. Sprawdzamy Polar Beara
                var unitL1 = engine.CurrentState.Board.Lines[1].Player1Unit;
                Assert(unitL1 != null && unitL1.Definition.Name == "Polar Bear",
                       "Polar Bear powinien być na Linii 1");

                // 4. Sprawdzamy The Moder (powinien być w ręce)
                Assert(HasCardInHand(engine, "The Moder"), "The Moder powinien zostać w ręce");

                Console.WriteLine(">>> SCENARIUSZ 1: SUKCES\n");

                // =======================================================================
                // SCENARIUSZ 2: BLACK CAT (Return To Hand)
                // Testuje: UnitSacrificedEvent, ReturnToHand, Interaction with Sacrifice
                // =======================================================================
                Console.WriteLine("[SCENARIUSZ 2] Zagrywam Kota, potem kolejny Polar Bear (cheat) zjada Kota...");

                // Hack: Dodajmy drugiego niedźwiedzia do ręki (bo pierwszego już zagraliśmy)
                var bear2 = factory.CreateCard(24, 1);
                engine.CurrentState = engine.CurrentState.UpdatePlayer(engine.CurrentState.PlayerA.WithCardAddedToHand(bear2));

                // 1. Zagraj Kota na Linię 2
                engine.ExecuteCommand(new PlayUnitCommand(1, blackCat.InstanceId, 2));
                Assert(IsUnitOnBoard(engine, 2, "Black Cat"), "Kot powinien wejść na stół");

                // 2. Zagraj Niedźwiedzia 2 na Linię 3, celując w Kota
                engine.ExecuteCommand(new PlayUnitCommand(1, bear2.InstanceId, 3, selectedTargetId: blackCat.InstanceId));

                // Weryfikacja:
                // Kot powinien zniknąć ze stołu
                Assert(!IsUnitOnBoard(engine, 2, "Black Cat"), "Kota nie powinno być na stole");
                // Ale Kot powinien być w RĘCE (ReturnToHand)
                // Uwaga: ID instancji Kota powinno zostać zachowane (chyba że fabryka tworzy nowego, ale użyliśmy WithStats)
                Assert(HasCardInHand(engine, "Black Cat"), "Kot powinien wrócić do ręki!");

                // Cmentarz nie powinien zawierać Kota
                var discard = engine.CurrentState.PlayerA.DiscardPile;
                Assert(!discard.Any(c => c.Definition.Name == "Black Cat"), "Kota nie powinno być na cmentarzu");

                Console.WriteLine(">>> SCENARIUSZ 2: SUKCES\n");

                // =======================================================================
                // SCENARIUSZ 3: CRITICAL THINKING (Tutor)
                // Testuje: TargetType.Self (w kontekście Spell), TutorCard Action
                // =======================================================================
                Console.WriteLine("[SCENARIUSZ 3] Critical Thinking wyciąga Mokke z talii...");

                // W talii mamy Mokke.
                // Critical Thinking (ID 26) jest w ręce.
                // Zagrywamy Critical Thinking celując w ID Mokke (musimy je znać)
                var mokkeInDeck = engine.CurrentState.PlayerA.DrawPile.First(c => c.Definition.Name == "Mokke");

                Console.WriteLine($"Celuję w kartę w talii: {mokkeInDeck.Definition.Name} (ID: {mokkeInDeck.InstanceId})");

                var cmdSpell = new PlaySpellCommand(1, criticalThinking.InstanceId, selectedTargetId: mokkeInDeck.InstanceId);
                engine.ExecuteCommand(cmdSpell);

                // Weryfikacja
                Assert(HasCardInHand(engine, "Mokke"), "Mokke powinien trafić do ręki");
                Assert(!engine.CurrentState.PlayerA.DrawPile.Any(c => c.InstanceId == mokkeInDeck.InstanceId), "Mokke zniknął z talii");

                Console.WriteLine(">>> SCENARIUSZ 3: SUKCES\n");

                // =======================================================================
                // SCENARIUSZ 4: DOUBLE SURVIVALIST (Aura Loop Prevention)
                // Testuje: AuraSystem, SoulGuardPrevention, Depleted Status, TargetType.OtherFriendlyUnits
                // =======================================================================
                Console.WriteLine("[SCENARIUSZ 4] Dwa Survivalisty (czy są nieśmiertelne?)...");

                // Tworzymy dwóch Survivalistów
                var s1 = factory.CreateCard(22, 1);
                var s2 = factory.CreateCard(22, 1);

                // Wstawiamy ich "na siłę" na planszę (na linie 2 i 3, bo 0 i 1 są zajęte)
                // Musimy zaktualizować stan ręcznie, bo nie chcemy ich zagrywać z ręki (kosztują manę)
                var boardWithS = engine.CurrentState.Board
                    .WithUnitPlacedAt(2, 1, s1)
                    .WithUnitPlacedAt(3, 1, s2);
                engine.CurrentState = engine.CurrentState.UpdateBoard(boardWithS);

                // Wymuszamy cykl silnika, żeby Aury się nałożyły
                // Używamy EndPhase w UnitOnly, co jest bezpieczne (przełączy na UnitSpell, ale przeliczy aury)
                engine.ExecuteCommand(new EndPhaseCommand(1));

                // Weryfikacja początkowa: Obaj powinni mieć SoulGuard
                Assert(HasKeyword(engine, 2, Keyword.SoulGuard), "S1 powinien mieć SoulGuard");
                Assert(HasKeyword(engine, 3, Keyword.SoulGuard), "S2 powinien mieć SoulGuard");

                // 1. ZABIJAMY S1 (Pierwszy raz)
                Console.WriteLine("Zadaję śmiertelne obrażenia S1...");
                var targetS1 = engine.CurrentState.Board.Lines[2].Player1Unit;
                var deadS1 = targetS1.TakeDamage(10);
                engine.CurrentState = engine.CurrentState.UpdateBoard(engine.CurrentState.Board.UpdateUnit(deadS1));

                // Odpalamy Resolve loop
                engine.ExecuteCommand(new EndPhaseCommand(2)); // Przełączamy dalej, żeby wymusić update

                // Weryfikacja po 1. śmierci:
                // - Powinien żyć
                // - Powinien mieć Depleted
                // - NIE powinien mieć SoulGuard (Aura zablokowana)
                Assert(IsUnitOnBoard(engine, 2, "Survivalist"), "S1 powinien przeżyć");
                Assert(HasKeyword(engine, 2, Keyword.SoulGuardDepleted), "S1 powinien być Depleted");
                Assert(!HasKeyword(engine, 2, Keyword.SoulGuard), "S1 NIE powinien odzyskać aury");

                // 2. ZABIJAMY S1 (Drugi raz)
                Console.WriteLine("Zadaję śmiertelne obrażenia S1 (drugi raz)...");
                targetS1 = engine.CurrentState.Board.Lines[2].Player1Unit; // Pobieramy aktualny stan
                deadS1 = targetS1.TakeDamage(10);
                engine.CurrentState = engine.CurrentState.UpdateBoard(engine.CurrentState.Board.UpdateUnit(deadS1));

                engine.ExecuteCommand(new EndPhaseCommand(1)); // Resolve

                // Weryfikacja ostateczna:
                // - S1 powinien zginąć (zniknąć z planszy)
                Assert(!IsUnitOnBoard(engine, 2, "Survivalist"), "S1 powinien zginąć definitywnie");
                Assert(IsUnitOnBoard(engine, 3, "Survivalist"), "S2 powinien nadal żyć");

                Console.WriteLine(">>> SCENARIUSZ 4: SUKCES\n");

                // --- FINISH ---
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("=============================================");
                Console.WriteLine("WSZYSTKIE SYSTEMY DZIAŁAJĄ POPRAWNIE!");
                Console.WriteLine("Refaktoryzacja zakończona sukcesem.");
                Console.WriteLine("=============================================");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n[BŁĄD KRYTYCZNY] Test niezaliczony: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                Console.ResetColor();
            }

            // Sprzątanie
            File.Delete("master_cards.json");
            Console.ReadKey();
        }

        // --- POMOCNICY TESTOWI ---

        static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }

        static bool HasCardInHand(GameEngine engine, string name)
        {
            return engine.CurrentState.PlayerA.Hand.Any(c => c.Definition.Name == name);
        }

        static bool IsUnitOnBoard(GameEngine engine, int lineIndex, string name)
        {
            var u = engine.CurrentState.Board.Lines[lineIndex].Player1Unit;
            return u != null && u.Definition.Name == name;
        }

        private static void LoadGameData() { CardLibrary.Instance.LoadFromJson("master_cards.json"); }

        private static void CreateMasterJson()
        {
            string json = @"
            [
              { ""Id"": 1, ""Name"": ""Mokke"", ""Type"": ""Unit"", ""Cost"": 1, ""Attack"": 1, ""Health"": 2 },
              { ""Id"": 20, ""Name"": ""Crusader"", ""Type"": ""Unit"", ""Cost"": 2, ""Attack"": 2, ""Health"": 3, ""Keywords"": [""Armored""] },
              { ""Id"": 21, ""Name"": ""Corpse Eater"", ""Type"": ""Unit"", ""Subtypes"": [""Monster""], ""Cost"": 3, ""Attack"": 3, ""Health"": 1,
                ""Effects"": [ { ""Trigger"": ""OnFriendlyUnitDied"", ""Zone"": ""Hand"", ""Actions"": [ { ""Type"": ""SummonUnit"", ""Target"": ""Self"" } ] } ] },
              { ""Id"": 23, ""Name"": ""Black Cat"", ""Type"": ""Unit"", ""Subtypes"": [""Animal""], ""Cost"": 0, ""Attack"": 1, ""Health"": 1,
                ""Effects"": [ { ""Trigger"": ""OnSacrificed"", ""Zone"": ""Board"", ""Actions"": [ { ""Type"": ""ReturnToHand"", ""Target"": ""Self"" } ] } ] },
              { ""Id"": 24, ""Name"": ""Polar Bear"", ""Type"": ""Unit"", ""Subtypes"": [""Animal""], ""Cost"": 2, ""Attack"": 2, ""Health"": 3,
                ""Effects"": [ { ""Trigger"": ""OnPlayed"", ""Targeting"": ""TargetFriendlyUnit"", 
                                 ""Actions"": [ { ""Type"": ""AbsorbStats"", ""Target"": ""SelectedTarget"" }, { ""Type"": ""SacrificeUnit"", ""Target"": ""SelectedTarget"" } ] } ] },
              { ""Id"": 25, ""Name"": ""The Moder"", ""Type"": ""Unit"", ""Subtypes"": [""Monster""], ""Cost"": 7, ""Attack"": 7, ""Health"": 7,
                ""Effects"": [ { ""Trigger"": ""OnSacrificed"", ""Zone"": ""Hand"", ""Actions"": [ { ""Type"": ""BuffStats"", ""Target"": ""Self"", ""Amount"": -1 } ] } ] },
              { ""Id"": 26, ""Name"": ""Critical Thinking"", ""Type"": ""Spell"", ""Cost"": 1,
                ""Effects"": [ { ""Trigger"": ""OnPlayed"", ""Actions"": [ { ""Type"": ""TutorCard"", ""Target"": ""Self"" }, { ""Type"": ""ShuffleDeck"", ""Target"": ""Self"" } ] } ] },
              
              { ""Id"": 22, ""Name"": ""Survivalist"", ""Type"": ""Unit"", ""Cost"": 4, ""Attack"": 2, ""Health"": 3,
                ""Effects"": [ { ""Trigger"": ""Passive"", ""Zone"": ""Board"", ""Actions"": [ { ""Type"": ""ApplyStatus"", ""Target"": ""OtherFriendlyUnits"", ""StringParam"": ""SoulGuard"" } ] } ] }
            ]";
            File.WriteAllText("master_cards.json", json);
        }
        static bool HasKeyword(GameEngine engine, int lineIndex, Keyword keyword)
        {
            var u = engine.CurrentState.Board.Lines[lineIndex].Player1Unit;
            return u != null && u.CurrentStats.Keywords.Contains(keyword);
        }

    }
}