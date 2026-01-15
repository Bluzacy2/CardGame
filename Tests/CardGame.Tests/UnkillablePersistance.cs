using CardGame.Core.Application;
using CardGame.Core.Cards.Data;
using CardGame.Core.Commands.Implementations;
using CardGame.Core.GameRules.Death;
using CardGame.Core.State.Enums;
using CardGame.Core.State.Models;
using System.Linq;
using Xunit;

namespace CardGame.Tests
{
    public class UnkillablePersistenceTests
    {
        [Fact]
        public void UnkillableStatus_ShouldPersist_AfterReturningToHandFromGoblet()
        {
            // 1. ARRANGE: Define a unit (BoomBot) and the Goblet
            var json = @"[
                { ""Id"": 2, ""Name"": ""BoomBot"", ""Type"": ""Unit"", ""Attack"": 1, ""Health"": 3 },
                { 
                  ""Id"": 900, ""Name"": ""The Alluring Goblet"", ""Type"": ""Spell"", ""Cost"": 0,
                  ""Effects"": [
                    {
                      ""Trigger"": ""OnPlayed"",
                      ""Targeting"": ""TargetFriendlyUnit"",
                      ""Actions"": [
                        { ""Type"": ""ApplyStatus"", ""Target"": ""SelectedTarget"", ""StatusKeyword"": ""Unkillable"" },
                        { ""Type"": ""DestroyUnit"", ""Target"": ""SelectedTarget"" }
                      ]
                    }
                  ]
                }
            ]";

            var engine = TestHelpers.CreateEngineWithCards(json);
            var f = engine.Factory;
            var bot = f.CreateCard(2, 1);
            var goblet = f.CreateCard(900, 1);

            // Set initial state: Bot on board, Goblet in hand
            engine.CurrentState = engine.CurrentState
                .UpdateBoard(engine.CurrentState.Board.WithUnitPlacedAt(0, 1, bot))
                .UpdatePlayer(engine.CurrentState.PlayerA.WithCardAddedToHand(goblet))
                .With(currentPhase: GamePhase.UnitAndAction);

            // 2. ACT: Play the Goblet on the BoomBot
            // This should grant Unkillable, then "Destroy" it, triggering UnkillableHandler
            engine.ExecuteCommand(new PlaySpellCommand(1, goblet.InstanceId, bot.InstanceId));

            // 3. ASSERT: Phase 1 - Card should be in hand
            var handAfterFirstDeath = engine.CurrentState.PlayerA.Hand;
            var botInHand = handAfterFirstDeath.FirstOrDefault(c => c.Definition.Id == "2");

            Assert.NotNull(botInHand);
            Assert.Contains(Keyword.Unkillable, botInHand.CurrentStats.Keywords);

            // 4. ACT: Play the bot again and kill it normally (e.g., via damage)
            engine.CurrentState = engine.CurrentState.With(currentPhase: GamePhase.UnitOnly);
            engine.ExecuteCommand(new PlayUnitCommand(1, botInHand.InstanceId, 0));

            var botOnBoard = engine.CurrentState.Board.Lines[0].Player1Unit;
            Assert.NotNull(botOnBoard);

            // Deal lethal damage
            var damagedBot = botOnBoard.TakeDamage(10);
            engine.CurrentState = engine.CurrentState.UpdateBoard(engine.CurrentState.Board.UpdateUnit(damagedBot));

            // Resolve deaths
            var context = new GameContext(f, engine.Rng, engine.Events);
            engine.CurrentState = new DeathResolver().ResolveDeaths(engine.CurrentState, engine.Events, context);

            // 5. ASSERT: Phase 2 - Bot should have returned to hand AGAIN
            var handAfterSecondDeath = engine.CurrentState.PlayerA.Hand;
            var botInHandAgain = handAfterSecondDeath.FirstOrDefault(c => c.Definition.Id == "2");

            Assert.NotNull(botInHandAgain); // THIS IS WHERE IT LIKELY FAILS
            Assert.Contains(Keyword.Unkillable, botInHandAgain.CurrentStats.Keywords);
        }
    }
}