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
    public static class MechanicsTester
    {
        public static void Run()
        {
            Console.WriteLine("\n=== UNIT TESTS START ===");

            try
            {
                CreateMechanicsJson();
                CardLibrary.Instance.LoadFromJson("mechanics_cards.json");

                var rng = new DeterministicRng(999);
                var factory = new CardFactory(CardLibrary.Instance, rng);

                var pA = PlayerState.Initial(1, new List<CardInstance>())
                    .With(maxBlood: 100, currentBlood: 100);

                var tank = factory.CreateCard(1, 1);
                var buffer = factory.CreateCard(2, 1);
                var healer = factory.CreateCard(3, 1);
                var armorer = factory.CreateCard(4, 1);
                var guardian = factory.CreateCard(5, 1);
                var phoenix = factory.CreateCard(6, 1);
                var nuke = factory.CreateCard(99, 1);

                pA = pA.WithCardAddedToHand(buffer)
                       .WithCardAddedToHand(healer)
                       .WithCardAddedToHand(armorer)
                       .WithCardAddedToHand(nuke);

                var board = BoardState.Empty()
                    .WithUnitPlacedAt(0, 1, tank)
                    .WithUnitPlacedAt(1, 1, guardian)
                    .WithUnitPlacedAt(2, 1, phoenix);

                var state = new GameState(1, GamePhase.UnitAndAction, 1, board, pA, PlayerState.Initial(2, new List<CardInstance>()));
                var engine = new GameEngine(state, seed: 999);

                // --- TESTY (Skrócone logi) ---
                Console.WriteLine("Test 1: Buffing...");
                engine.ExecuteCommand(new PlayUnitCommand(1, buffer.InstanceId, 3, selectedTargetId: tank.InstanceId));
                Assert(engine.CurrentState.Board.Lines[0].Player1Unit != null, "Buff test failed: Tank is null");
                Assert(engine.CurrentState.Board.Lines[0].Player1Unit.CurrentStats.Attack == 3, "Buff failed");

                Console.WriteLine("Test 2: Damage & Heal...");
                var tankCheck = engine.CurrentState.Board.Lines[0].Player1Unit;
                Assert(tankCheck != null, "Heal test failed: Tank is null before damage");
                var damagedTank = tankCheck.TakeDamage(4);
                
                // Hack planszy
                var guardianCheck = engine.CurrentState.Board.Lines[1].Player1Unit;
                var phoenixCheck = engine.CurrentState.Board.Lines[2].Player1Unit;
                Assert(guardianCheck != null && phoenixCheck != null, "Heal test failed: Guardian or Phoenix is null");
                var newBoard = BoardState.Empty()
                   .WithUnitPlacedAt(0, 1, damagedTank)
                   .WithUnitPlacedAt(1, 1, guardianCheck)
                   .WithUnitPlacedAt(2, 1, phoenixCheck);
                engine.CurrentState = engine.CurrentState.UpdateBoard(newBoard);

                engine.ExecuteCommand(new PlayUnitCommand(1, healer.InstanceId, 3, selectedTargetId: tank.InstanceId));
                Assert(engine.CurrentState.Board.Lines[0].Player1Unit != null, "Heal test failed: Tank is null after heal");
                Assert(engine.CurrentState.Board.Lines[0].Player1Unit.CurrentStats.Health == 7, "Heal failed");

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("\n[SUCCESS] Wszystkie testy mechaniczne zaliczone.");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[FAIL] {ex.Message}");
                Console.ResetColor();
            }

            if (File.Exists("mechanics_cards.json")) File.Delete("mechanics_cards.json");
        }

        private static void CreateMechanicsJson()
        {
            string json = @"
            [
              { ""Id"": 1, ""Name"": ""Tank"", ""Type"": ""Unit"", ""Cost"": 1, ""Attack"": 1, ""Health"": 5 },
              { ""Id"": 2, ""Name"": ""Buffer"", ""Type"": ""Unit"", ""Cost"": 1, ""Attack"": 1, ""Health"": 1, ""Effects"": [ { ""Trigger"": ""OnPlayed"", ""Actions"": [ { ""Type"": ""BuffStats"", ""Target"": ""SelectedTarget"", ""BuffAtk"": 2, ""BuffHp"": 2 } ] } ] },
              { ""Id"": 3, ""Name"": ""Healer"", ""Type"": ""Unit"", ""Cost"": 1, ""Attack"": 1, ""Health"": 1, ""Effects"": [ { ""Trigger"": ""OnPlayed"", ""Actions"": [ { ""Type"": ""Heal"", ""Target"": ""SelectedTarget"", ""Amount"": 10 } ] } ] },
              { ""Id"": 4, ""Name"": ""Armorer"", ""Type"": ""Unit"", ""Cost"": 1, ""Attack"": 1, ""Health"": 1, ""Effects"": [ { ""Trigger"": ""OnPlayed"", ""Actions"": [ { ""Type"": ""ApplyStatus"", ""Target"": ""SelectedTarget"", ""StringParam"": ""Armored"" } ] } ] },
              { ""Id"": 5, ""Name"": ""Guardian"", ""Type"": ""Unit"", ""Cost"": 1, ""Attack"": 1, ""Health"": 3, ""Keywords"": [""SoulGuard""] },
              { ""Id"": 6, ""Name"": ""Phoenix"", ""Type"": ""Unit"", ""Cost"": 1, ""Attack"": 1, ""Health"": 1, ""Keywords"": [""Unkillable""] },
              { ""Id"": 99, ""Name"": ""Nuke"", ""Type"": ""Spell"", ""Cost"": 0, ""Effects"": [ { ""Trigger"": ""OnPlayed"", ""Actions"": [ { ""Type"": ""DealDamage"", ""Target"": ""SelectedTarget"", ""Amount"": 10 } ] } ] }
            ]";
            File.WriteAllText("mechanics_cards.json", json);
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }
    }
}