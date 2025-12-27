using System.Collections.Generic;
using System.Linq;
using CardGame.Core.Application;
using CardGame.Core.Cards.Data;
using CardGame.Core.Commands.Implementations;
using CardGame.Core.State.Enums;
using Xunit;

namespace CardGame.Tests
{
    public class TimeoutTests
    {
        [Fact]
        public void Expectancy_ShouldAutoResolve_WhenUpdateTimerReachesLimit()
        {
            string json = @"[
                { ""Id"": 15, ""Name"": ""Expectancy"", ""Type"": ""Spell"", ""Cost"": 2,
                  ""Effects"": [ { 
                      ""Trigger"": ""OnPlayed"", 
                      ""Targeting"": ""Choice"",
                      ""ChoiceLabels"": [""Deck"", ""Discard""],
                      ""Actions"": [
                          { ""Type"": ""DrawCard"", ""Amount"": 2 },
                          { ""Type"": ""DrawFromDiscard"", ""Amount"": 2 }
                      ]
                  } ] 
                },
                { ""Id"": 1, ""Name"": ""Token"", ""Type"": ""Unit"", ""Cost"": 0 }
            ]";

            var engine = TestHelpers.CreateEngineWithCards(json);
            var factory = engine.Factory;

            var p1 = engine.CurrentState.PlayerA.With(
                drawPile: new[] { factory.CreateCard(1, 1), factory.CreateCard(1, 1) }
            ).WithResourceChanged(ResourceType.Blood, 10, 10);

            var expectancy = factory.CreateCard(15, 1);
            p1 = p1.WithCardAddedToHand(expectancy);

            engine.CurrentState = engine.CurrentState.UpdatePlayer(p1);
            engine.CurrentState = engine.CurrentState.With(currentPhase: GamePhase.ActionOnly);

            engine.ExecuteCommand(new PlaySpellCommand(1, expectancy.InstanceId));
            Assert.NotNull(engine.CurrentState.PendingInteraction);

            engine.Update(5.1f);

            Assert.Null(engine.CurrentState.PendingInteraction);
            Assert.Equal(2, engine.CurrentState.PlayerA.Hand.Count);
        }

        [Fact]
        public void UnitTargeting_ShouldAutoResolve_OnTimeout()
        {
            string json = @"[
                { ""Id"": 4, ""Name"": ""Informer"", ""Type"": ""Unit"", ""Cost"": 1,
                  ""Effects"": [ { 
                      ""Trigger"": ""OnPlayed"", ""Targeting"": ""TargetEnemyUnit"",
                      ""Actions"": [ { ""Type"": ""ApplyStatus"", ""StatusKeyword"": ""Marked"" } ]
                  } ] 
                },
                { ""Id"": 2, ""Name"": ""Target"", ""Type"": ""Unit"", ""Health"": 5 }
            ]";

            var engine = TestHelpers.CreateEngineWithCards(json);
            var victim = engine.Factory.CreateCard(2, 2);
            engine.CurrentState = engine.CurrentState.UpdateBoard(
                engine.CurrentState.Board.WithUnitPlacedAt(0, 2, victim));

            var informer = engine.Factory.CreateCard(4, 1);
            engine.CurrentState = engine.CurrentState.UpdatePlayer(
                engine.CurrentState.PlayerA
                    .WithCardAddedToHand(informer)
                    .WithResourceChanged(ResourceType.Blood, 10, 10));

            engine.ExecuteCommand(new PlayUnitCommand(1, informer.InstanceId, 3));
            engine.Update(5.0f);

            Assert.Null(engine.CurrentState.PendingInteraction);
            var updatedVictim = engine.CurrentState.Board.Lines[0].Player2Unit;
            Assert.Contains(Keyword.Marked, updatedVictim!.CurrentStats.Keywords);
        }
    }
}