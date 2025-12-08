using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CardGame.Core.Application;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Factories;
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
            Console.WriteLine("=== MASTER TEST MECHANIK (CONSOLE) ===\n");

            try
            {
                // 1. GENEROWANIE KART (Wszystkie przypadki testowe)
                CreateMechanicsJson();
                CardLibrary.Instance.LoadFromJson("mechanics_cards.json");

                // 2. SETUP SILNIKA
                var rng = new DeterministicRng(999);
                var factory = new CardFactory(CardLibrary.Instance, rng);

                // --- PRZYGOTOWANIE GRACZA I KART ---
                // Gracz ma nieskończoność many
                var pA = PlayerState.Initial(1, new List<CardInstance>())
                    .With(maxBlood: 100, currentBlood: 100);

                // Tworzymy karty testowe
                var tank = factory.CreateCard(1, 1);      // 1/5
                var buffer = factory.CreateCard(2, 1);    // +2/+2
                var healer = factory.CreateCard(3, 1);    // Heal 10
                var armorer = factory.CreateCard(4, 1);   // Give Armored
                var guardian = factory.CreateCard(5, 1);  // SoulGuard
                var phoenix = factory.CreateCard(6, 1);   // Unkillable
                var nuke = factory.CreateCard(99, 1);     // Deal 10 Dmg

                // Dodajemy karty akcyjne do ręki
                pA = pA.WithCardAddedToHand(buffer)
                       .WithCardAddedToHand(healer)
                       .WithCardAddedToHand(armorer)
                       .WithCardAddedToHand(nuke);

                // Rozstawiamy sytuację na stole (żeby nie tracić czasu na zagrywanie wszystkiego)
                var board = BoardState.Empty()
                    .WithUnitPlacedAt(0, 1, tank)      // L0: Tank (będzie buffowany i leczony)
                    .WithUnitPlacedAt(1, 1, guardian)  // L1: Guardian (test SoulGuard)
                    .WithUnitPlacedAt(2, 1, phoenix);  // L2: Phoenix (test Unkillable)

                // Tworzymy stan gry
                var state = new GameState(1, GamePhase.UnitAndAction, 1, board, pA, PlayerState.Initial(2, new List<CardInstance>()));
                var engine = new GameEngine(state, seed: 999);

                Console.WriteLine("--- START ---");

                // ====================================================================
                // TEST 1: BUFFOWANIE (+2/+2)
                // ====================================================================
                Console.WriteLine("\n[KROK 1] Zagrywam Buffera na Tanka (1/5)...");
                // Buffer celuje w SelectedTarget. Wybieramy Tanka (L0).
                engine.ExecuteCommand(new PlayUnitCommand(1, buffer.InstanceId, 3, selectedTargetId: tank.InstanceId));

                var tankCheck = engine.CurrentState.Board.Lines[0].Player1Unit;
                Console.WriteLine($"   Tank Stats: {tankCheck.CurrentStats.Attack}/{tankCheck.CurrentStats.Health}");

                Assert(tankCheck.CurrentStats.Attack == 3, "Atak powinien być 1+2=3");
                Assert(tankCheck.CurrentStats.Health == 7, "HP powinno być 5+2=7");
                Assert(tankCheck.MaxHealth == 7, "MaxHP powinno być 7");
                Console.WriteLine(">>> SUKCES: Buff zadziałał.");

                // ====================================================================
                // TEST 2: OBRAŻENIA I LECZENIE (MaxHP Cap)
                // ====================================================================
                Console.WriteLine("\n[KROK 2] Zadaję 4 dmg Tankowi, usuwam Buffera, potem leczę Tanka...");

                // 1. Zadajemy obrażenia (Cheat)
                var damagedTank = tankCheck.TakeDamage(4); // 7 - 4 = 3 HP
                Console.WriteLine($"   (Cheat) Tank oberwał za 4. Ma teraz: {damagedTank.CurrentStats.Health} HP.");

                // 2. REKONSTRUKCJA PLANSZY (PEWNE USUNIĘCIE BUFFERA)
                // Pobieramy aktualne wersje jednostek, które mają zostać
                var guardianRef = engine.CurrentState.Board.Lines[1].Player1Unit;
                var phoenixRef = engine.CurrentState.Board.Lines[2].Player1Unit;

                // Tworzymy nową, czystą planszę i stawiamy na niej tylko to, co chcemy
                var newBoard = BoardState.Empty()
                    .WithUnitPlacedAt(0, 1, damagedTank) // Nasz raniony tank
                    .WithUnitPlacedAt(1, 1, guardianRef)
                    .WithUnitPlacedAt(2, 1, phoenixRef);
                // Linia 3 jest teraz pusta (z definicji Empty)

                // Aktualizujemy silnik
                engine.CurrentState = engine.CurrentState.UpdateBoard(newBoard);

                // Sprawdzenie diagnostyczne
                if (engine.CurrentState.Board.Lines[3].Player1Unit != null)
                    Console.WriteLine("   [OSTRZEŻENIE] Linia 3 nadal zajęta! Coś jest nie tak z BoardState.Empty().");
                else
                    Console.WriteLine("   [INFO] Linia 3 wyczyszczona pomyślnie.");

                // 3. Zagrywamy Healera na Linię 3
                engine.ExecuteCommand(new PlayUnitCommand(1, healer.InstanceId, 3, selectedTargetId: tank.InstanceId));

                var healedTank = engine.CurrentState.Board.Lines[0].Player1Unit;
                Console.WriteLine($"   Tank po leczeniu: {healedTank.CurrentStats.Health}/{healedTank.MaxHealth}");

                Assert(healedTank.CurrentStats.Health == 7, "HP powinno wrócić do Max (7), a nie 13!");
                Console.WriteLine(">>> SUKCES: Leczenie ograniczone do MaxHP.");
                // ====================================================================
                // TEST 3: STATUSY (Armored)
                // ====================================================================
                Console.WriteLine("\n[KROK 3] Nadaję status 'Armored' Tankowi...");
                // Zwalniamy miejsce po Healerze
                engine.CurrentState = engine.CurrentState.UpdateBoard(engine.CurrentState.Board.WithUnitPlacedAt(3, 1, null));

                engine.ExecuteCommand(new PlayUnitCommand(1, armorer.InstanceId, 3, selectedTargetId: tank.InstanceId));

                var armoredTank = engine.CurrentState.Board.Lines[0].Player1Unit;
                bool hasArmor = armoredTank.CurrentStats.Keywords.Contains(Keyword.Armored);
                Console.WriteLine($"   Tank Keywords: {string.Join(", ", armoredTank.CurrentStats.Keywords)}");

                Assert(hasArmor, "Tank powinien mieć Armored.");
                Assert(armoredTank.CurrentStats.Attack == 3, "Statystyki nie powinny się zresetować.");
                Console.WriteLine(">>> SUKCES: Status dodany.");

                // ====================================================================
                // TEST 4: SOUL GUARD
                // ====================================================================
                Console.WriteLine("\n[KROK 4] Zabijam Guardiana (3 HP, SoulGuard) za pomocą Nuke (10 Dmg)...");
                // Potrzebujemy Nuke w ręce. Mamy go (ID 99).
                // Ale potrzebujemy Nuke x2 (do dobicia). Dodajmy drugiego.
                var nuke2 = factory.CreateCard(99, 1);
                engine.CurrentState = engine.CurrentState.UpdatePlayer(engine.CurrentState.PlayerA.WithCardAddedToHand(nuke2));

                // Strzał 1
                engine.ExecuteCommand(new PlaySpellCommand(1, nuke.InstanceId, selectedTargetId: guardian.InstanceId));

                var guardianSurvivor = engine.CurrentState.Board.Lines[1].Player1Unit;
                if (guardianSurvivor == null) throw new Exception("Guardian zginął, a powinien przeżyć!");

                Console.WriteLine($"   Guardian HP: {guardianSurvivor.CurrentStats.Health}");
                Console.WriteLine($"   Keywords: {string.Join(", ", guardianSurvivor.CurrentStats.Keywords)}");

                Assert(guardianSurvivor.CurrentStats.Health == 1, "Guardian powinien mieć 1 HP.");
                Assert(guardianSurvivor.CurrentStats.Keywords.Contains(Keyword.SoulGuardDepleted), "Powinien mieć status Depleted.");
                Console.WriteLine(">>> SUKCES: SoulGuard zadziałał.");

                Console.WriteLine("   -> Dobijam Guardiana...");
                engine.ExecuteCommand(new PlaySpellCommand(1, nuke2.InstanceId, selectedTargetId: guardian.InstanceId));

                var deadGuardian = engine.CurrentState.Board.Lines[1].Player1Unit;
                Assert(deadGuardian == null, "Guardian powinien zginąć za drugim razem.");
                Console.WriteLine(">>> SUKCES: Guardian zginął definitywnie.");

                // ====================================================================
                // TEST 5: UNKILLABLE
                // ====================================================================
                Console.WriteLine("\n[KROK 5] Zabijam Phoenixa (Unkillable)...");
                // Dodajmy Nuke 3
                var nuke3 = factory.CreateCard(99, 1);
                engine.CurrentState = engine.CurrentState.UpdatePlayer(engine.CurrentState.PlayerA.WithCardAddedToHand(nuke3));

                // Najpierw buffnijmy Phoenixa, żeby sprawdzić czy straci buffy po powrocie
                var buffedPhoenix = phoenix.AddPermanentBuff(new CardStats(5, 5, 0));
                engine.CurrentState = engine.CurrentState.UpdateBoard(engine.CurrentState.Board.WithUnitPlacedAt(2, 1, buffedPhoenix));
                Console.WriteLine($"   Phoenix przed śmiercią: {buffedPhoenix.CurrentStats.Attack}/{buffedPhoenix.CurrentStats.Health}");

                // Kill
                engine.ExecuteCommand(new PlaySpellCommand(1, nuke3.InstanceId, selectedTargetId: phoenix.InstanceId));

                // Sprawdź stół
                Assert(engine.CurrentState.Board.Lines[2].Player1Unit == null, "Phoenix powinien zniknąć ze stołu.");

                // Sprawdź rękę (powinien wrócić jako ostatnia karta)
                var returnedPhoenix = engine.CurrentState.PlayerA.Hand.Last();
                Console.WriteLine($"   Phoenix w ręce: {returnedPhoenix.Definition.Name}, Stats: {returnedPhoenix.CurrentStats.Attack}/{returnedPhoenix.CurrentStats.Health}");

                Assert(returnedPhoenix.Definition.Name == "Phoenix", "Karta w ręce to Phoenix.");
                Assert(returnedPhoenix.CurrentStats.Attack == 1, "Statystyki powinny się zresetować (1/1).");
                Console.WriteLine(">>> SUKCES: Unkillable działa poprawnie.");

                // --- KONIEC ---
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("\n=============================================");
                Console.WriteLine("WSZYSTKIE MECHANIKI DZIAŁAJĄ POPRAWNIE!");
                Console.WriteLine("=============================================");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n[BŁĄD] {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                Console.ResetColor();
            }

            if (File.Exists("mechanics_cards.json")) File.Delete("mechanics_cards.json");
            Console.ReadKey();
        }

        // --- DANE TESTOWE ---
        private static void CreateMechanicsJson()
        {
            string json = @"
            [
              { ""Id"": 1, ""Name"": ""Tank"", ""Type"": ""Unit"", ""Cost"": 1, ""Attack"": 1, ""Health"": 5 },
              
              { ""Id"": 2, ""Name"": ""Buffer"", ""Type"": ""Unit"", ""Cost"": 1, ""Attack"": 1, ""Health"": 1,
                ""Effects"": [ { ""Trigger"": ""OnPlayed"", ""Actions"": [ { ""Type"": ""BuffStats"", ""Target"": ""SelectedTarget"", ""BuffAtk"": 2, ""BuffHp"": 2 } ] } ] },
              
              { ""Id"": 3, ""Name"": ""Healer"", ""Type"": ""Unit"", ""Cost"": 1, ""Attack"": 1, ""Health"": 1,
                ""Effects"": [ { ""Trigger"": ""OnPlayed"", ""Actions"": [ { ""Type"": ""Heal"", ""Target"": ""SelectedTarget"", ""Amount"": 10 } ] } ] },
              
              { ""Id"": 4, ""Name"": ""Armorer"", ""Type"": ""Unit"", ""Cost"": 1, ""Attack"": 1, ""Health"": 1,
                ""Effects"": [ { ""Trigger"": ""OnPlayed"", ""Actions"": [ { ""Type"": ""ApplyStatus"", ""Target"": ""SelectedTarget"", ""StringParam"": ""Armored"" } ] } ] },
              
              { ""Id"": 5, ""Name"": ""Guardian"", ""Type"": ""Unit"", ""Cost"": 1, ""Attack"": 1, ""Health"": 3, ""Keywords"": [""SoulGuard""] },
              
              { ""Id"": 6, ""Name"": ""Phoenix"", ""Type"": ""Unit"", ""Cost"": 1, ""Attack"": 1, ""Health"": 1, ""Keywords"": [""Unkillable""] },
              
              { ""Id"": 99, ""Name"": ""Nuke"", ""Type"": ""Spell"", ""Cost"": 0,
                ""Effects"": [ { ""Trigger"": ""OnPlayed"", ""Actions"": [ { ""Type"": ""DealDamage"", ""Target"": ""SelectedTarget"", ""Amount"": 10 } ] } ] }
            ]";
            File.WriteAllText("mechanics_cards.json", json);
        }

        static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }
    }
}