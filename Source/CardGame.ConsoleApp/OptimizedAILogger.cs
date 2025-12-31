using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CardGame.Core.AI;
using CardGame.Core.AI.Logic;
using CardGame.Core.AI.Strategies;
using CardGame.Core.Application;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Factories;
using CardGame.Core.Cards.Models;
using CardGame.Core.State.Models;
using CardGame.Core.State.Enums;
using CardGame.Core.Events;
using CardGame.Core.Events.Interfaces;
using CardGame.Core.Commands.Interfaces;
using CardGame.Core.Commands.Implementations;

namespace CardGame.ConsoleApp
{
    public static class OptimizedAILogger
    {
        public static async Task RunAsync()
        {
            Console.Clear();
            Console.WriteLine("=== OPTIMIZED AI BATTLE LOGGER (WITH DEBUG PANEL) ===");

            // 1. SETUP
            CardLibrary.Instance.LoadFromJson("Data/Cards/cards.json");
            var rng = new DeterministicRng(new Random().Next());
            var factory = new CardFactory(CardLibrary.Instance, rng);
            var deckA = CreateHighlanderDeck(factory, 1, 30);
            var deckB = CreateHighlanderDeck(factory, 2, 30);
            var state = GameState.Initial(1, deckA, deckB, rng);
            var engine = new GameEngine(state, rng.Seed);
            var controller1 = new AIPlayerController(engine, 1, new StandardStrategy(), AISolverType.BeamSearch);
            var controller2 = new AIPlayerController(engine, 2, new StandardStrategy(), AISolverType.BeamSearch);
            int eventsSeenSoFar = 0;

            // 2. MAIN GAME LOOP
            while (!engine.IsGameOver)
            {
                var currentState = engine.CurrentState;
                int roundNum = (currentState.TurnNumber + 1) / 2;

                if (currentState.CurrentPhase == GamePhase.Mulligan)
                {
                    if (!currentState.PlayersReady.Contains(1)) engine.ExecuteCommand(new ConfirmMulliganCommand(1, new List<int>()));
                    if (!currentState.PlayersReady.Contains(2)) engine.ExecuteCommand(new ConfirmMulliganCommand(2, new List<int>()));
                    await Task.Delay(10);
                    continue;
                }

                Console.WriteLine($"\n--- ROUND {roundNum} | PHASE: {currentState.CurrentPhase} | ACTIVE: P{currentState.ActivePlayerId} | P1 HP: {currentState.PlayerA.Health}, P2 HP: {currentState.PlayerB.Health} ---");
                
                var activeController = currentState.ActivePlayerId == 1 ? controller1 : controller2;

                var evaluatedMoves = activeController.BeamSolver.FindBestMoves(currentState);

                if (!evaluatedMoves.Any())
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("[CRITICAL] AI could not find any possible moves. Forcing EndPhase.");
                    Console.ResetColor();
                    engine.ExecuteCommand(new EndPhaseCommand(currentState.ActivePlayerId));
                    continue;
                }

                var bestMove = evaluatedMoves.First();

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[BOT P{currentState.ActivePlayerId} THEORY] => {FormatCommand(bestMove.Command, currentState)} (Potential: {bestMove.Score:F2})");
                Console.ResetColor();

                Console.ForegroundColor = ConsoleColor.DarkGray;
                foreach (var consideredMove in evaluatedMoves.Skip(1).Take(4))
                {
                    Console.WriteLine($"    -> Considered: {FormatCommand(consideredMove.Command, currentState)} (Potential: {consideredMove.Score:F2})");
                }
                Console.ResetColor();
                
                var result = engine.ExecuteCommand(bestMove.Command);
                var allEvents = engine.Events.GetGlobalHistory().ToList();

                if (allEvents.Count > eventsSeenSoFar)
                {
                    for (int i = eventsSeenSoFar; i < allEvents.Count; i++)
                    {
                        string? logText = ParseEventToText(allEvents[i], result.NewState);
                        if(logText != null)
                        {
                            Console.ForegroundColor = ConsoleColor.Cyan;
                            Console.WriteLine($"  [FACT] {logText}");
                            Console.ResetColor();
                        }
                    }
                    eventsSeenSoFar = allEvents.Count;
                }

                await Task.Delay(200);
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n  === GAME OVER: WINNER IS PLAYER {engine.WinnerId ?? 0} ===\n");
            Console.ResetColor();
        }

        private static string FormatCommand(IGameCommand command, GameState state)
        {
            if (command is PlayUnitCommand puc)
            {
                var card = state.GetPlayer(puc.PlayerId).Hand.FirstOrDefault(c => c.InstanceId == puc.CardInstanceId);
                return $"PlayUnit '{card?.Definition.Name ?? "Unknown"}' in Lane {puc.TargetLineIndex}";
            }
            if (command is PlaySpellCommand psc)
            {
                var card = state.GetPlayer(psc.PlayerId).Hand.FirstOrDefault(c => c.InstanceId == psc.CardInstanceId);
                return $"PlaySpell '{card?.Definition.Name ?? "Unknown"}'";
            }
            if (command is EndPhaseCommand)
            {
                return "EndPhase";
            }
            if (command is SelectTargetCommand stc)
            {
                var target = state.Board.GetAllUnits().FirstOrDefault(u => u.InstanceId == stc.TargetId);
                return $"SelectTarget (Target: {target?.Definition.Name ?? "Invalid"})";
            }
            return command.GetType().Name;
        }

        private static List<CardInstance> CreateHighlanderDeck(CardFactory factory, int ownerId, int size)
        {
            var rand = new Random();
            var availableIds = Enumerable.Range(1, 36).OrderBy(x => rand.Next()).Take(size);
            return availableIds.Select(id => factory.CreateCard(id, ownerId)).ToList();
        }

        // Defensive Event Parser
        private static string? ParseEventToText(IGameEvent evt, GameState state)
        {
            if (evt is CardPlayedEvent cpe && cpe.Card?.Definition != null)
            {
                if(cpe.Card.Definition.Type == CardType.Unit)
                    return $"Player {cpe.PlayerId} summons {cpe.Card.Definition.Name}.";
            }
            if (evt is UnitDiedEvent ude && ude.Unit?.Definition != null)
            {
                return $"{ude.Unit.Definition.Name} was destroyed.";
            }
            if (evt is UnitDamagedEvent udde)
            {
                string sourceName = udde.Source?.Definition.Name ?? "an unknown source";
                if (udde.Unit?.Definition != null)
                {
                    return $"{udde.Unit.Definition.Name} takes {udde.Amount} damage from {sourceName}.";
                }
                else
                {
                    // This is likely direct player damage
                    int opponentId = (udde.Source?.OwnerPlayerId ?? 0) == 1 ? 2 : 1;
                    if(opponentId != 0) return $"Player {opponentId} takes {udde.Amount} damage from {sourceName}.";
                }
            }
            return null; // Return null for any other event type or if data is missing
        }
    }
}
