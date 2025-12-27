using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CardGame.Core.AI;
using CardGame.Core.AI.Strategies;
using CardGame.Core.Application;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Factories;
using CardGame.Core.Cards.Models;
using CardGame.Core.State.Enums;
using CardGame.Core.State.Models;

namespace CardGame.ConsoleApp
{
    public static class AIBattleRunner
    {
        public static async Task RunAsync()
        {
            Console.WriteLine("\n=== SYMULACJA AI VS AI ===");

            CreateAiDeckJson();
            CardLibrary.Instance.LoadFromJson("ai_deck.json");

            var rng = new DeterministicRng(new Random().Next());
            var factory = new CardFactory(CardLibrary.Instance, rng);

            var deckA = CreateRandomDeck(factory, 1, 15);
            var deckB = CreateRandomDeck(factory, 2, 15);

            // --- POPRAWKA TUTAJ ---
            // Inicjalizujemy stan i OD RAZU ustawiamy fazę na UnitOnly.
            // Pomijamy Mulligan, bo boty jeszcze nie umieją go obsługiwać, a chcemy zobaczyć walkę.
            var state = GameState.Initial(1, deckA, deckB, rng)
                                 .With(currentPhase: GamePhase.UnitOnly);

            var engine = new GameEngine(state, rng.Seed);

            var ai1 = new AIPlayerController(engine, 1, new StandardStrategy());
            // Zmieniam na RandomStrategy dla gracza 2, żeby było ciekawiej (AI vs Random)
            // lub zostaw StandardStrategy dla AI vs AI
            var ai2 = new AIPlayerController(engine, 2, new StandardStrategy());

            Console.WriteLine($"Seed Gry: {rng.Seed}");
            Console.WriteLine($"Start: Gracz 1 HP: {state.PlayerA.Health} vs Gracz 2 HP: {state.PlayerB.Health}");
            Console.WriteLine("Startuje symulację...\n");

            ai1.StartAutoPlay();
            ai2.StartAutoPlay();

            int lastTurn = 0;
            // Zmienna pomocnicza do śledzenia aktywnego gracza, żeby odświeżać widok
            int lastActivePlayer = 0;
            GamePhase lastPhase = GamePhase.None;

            while (!engine.IsGameOver)
            {
                // Odśwież widok jeśli zmieniła się tura, gracz LUB faza (np. po walce)
                if (engine.CurrentState.TurnNumber != lastTurn ||
                    engine.CurrentState.ActivePlayerId != lastActivePlayer ||
                    engine.CurrentState.CurrentPhase != lastPhase)
                {
                    lastTurn = engine.CurrentState.TurnNumber;
                    lastActivePlayer = engine.CurrentState.ActivePlayerId;
                    lastPhase = engine.CurrentState.CurrentPhase;

                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"\n--- TURA {lastTurn} | Faza: {lastPhase} | Aktywny: P{lastActivePlayer} ---");
                    PrintBoardState(engine.CurrentState);
                    Console.ResetColor();
                }

                await Task.Delay(200);
            }

            Console.WriteLine("\n=== KONIEC GRY ===");
            if (engine.WinnerId.HasValue)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"ZWYCIĘŻYŁ GRACZ: {engine.WinnerId.Value}!");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine("REMIS!");
            }
            Console.ResetColor();

            if (File.Exists("ai_deck.json")) File.Delete("ai_deck.json");
        }

        private static List<CardInstance> CreateRandomDeck(CardFactory factory, int ownerId, int count)
        {
            var deck = new List<CardInstance>();
            // ID kart zdefiniowanych w CreateAiDeckJson
            int[] availableIds = { 10, 11, 20, 21, 30, 99 };
            var rand = new Random();

            for (int i = 0; i < count; i++)
            {
                int randomId = availableIds[rand.Next(availableIds.Length)];
                deck.Add(factory.CreateCard(randomId, ownerId));
            }
            return deck;
        }

        private static void PrintBoardState(GameState state)
        {
            Console.WriteLine($"P1 HP: {state.PlayerA.Health} (Karty: {state.PlayerA.Hand.Count}) | P2 HP: {state.PlayerB.Health} (Karty: {state.PlayerB.Hand.Count})");
            Console.WriteLine("STÓŁ:");
            for (int i = 0; i < 4; i++)
            {
                var line = state.Board.Lines[i];
                string p1U = line.Player1Unit != null ? $"[{line.Player1Unit.Definition.Name} {line.Player1Unit.CurrentStats.Attack}/{line.Player1Unit.CurrentStats.Health}]" : "[ . ]";
                string p2U = line.Player2Unit != null ? $"[{line.Player2Unit.Definition.Name} {line.Player2Unit.CurrentStats.Attack}/{line.Player2Unit.CurrentStats.Health}]" : "[ . ]";
                Console.WriteLine($"  L{i}: {p1U}  vs  {p2U}");
            }
        }

        private static void CreateAiDeckJson()
        {
            string json = @"
            [
              { ""Id"": 10, ""Name"": ""Recruit"", ""Type"": ""Unit"", ""Cost"": 1, ""Attack"": 2, ""Health"": 2 },
              { ""Id"": 11, ""Name"": ""Guard"", ""Type"": ""Unit"", ""Cost"": 2, ""Attack"": 2, ""Health"": 4, ""Keywords"": [""Armored""] },
              { ""Id"": 20, ""Name"": ""Berserker"", ""Type"": ""Unit"", ""Cost"": 3, ""Attack"": 4, ""Health"": 3 },
              { ""Id"": 21, ""Name"": ""Cleric"", ""Type"": ""Unit"", ""Cost"": 2, ""Attack"": 1, ""Health"": 3,
                ""Effects"": [ { ""Trigger"": ""OnPlayed"", ""Targeting"": ""TargetFriendlyUnit"", ""Actions"": [ { ""Type"": ""Heal"", ""Amount"": 3 } ] } ] },
              { ""Id"": 30, ""Name"": ""Archer"", ""Type"": ""Unit"", ""Cost"": 2, ""Attack"": 2, ""Health"": 2,
                 ""Effects"": [ { ""Trigger"": ""OnPlayed"", ""Targeting"": ""TargetEnemyUnit"", ""Actions"": [ { ""Type"": ""DealDamage"", ""Amount"": 2 } ] } ] },
              { ""Id"": 99, ""Name"": ""Fireball"", ""Type"": ""Spell"", ""Cost"": 2,
                ""Effects"": [ { ""Trigger"": ""OnPlayed"", ""Targeting"": ""TargetEnemyUnit"", ""Actions"": [ { ""Type"": ""DealDamage"", ""Amount"": 4 } ] } ] }
            ]";
            File.WriteAllText("ai_deck.json", json);
        }
    }
}