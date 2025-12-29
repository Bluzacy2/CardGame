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
        public void Choice_ShouldAutoResolve_OnTimeout()
        {
            string json = @"[
                { ""Id"": 15, ""Name"": ""Expectancy"", ""Type"": ""Spell"", ""Cost"": 2,
                  ""Effects"": [ { 
                      ""Trigger"": ""OnPlayed"", ""Targeting"": ""Choice"", ""ChoiceLabels"": [""A"", ""B""],
                      ""Actions"": [ { ""Type"": ""DrawCard"", ""Amount"": 1 }, { ""Type"": ""Heal"", ""Amount"": 1, ""Target"": ""FriendlyHero"" } ]
                  } ] 
                }
            ]";
            var engine = TestHelpers.CreateEngineWithCards(json);
            var spell = engine.Factory.CreateCard(15, 1);
            engine.CurrentState = engine.CurrentState.UpdatePlayer(engine.CurrentState.PlayerA.WithCardAddedToHand(spell));
            engine.CurrentState = engine.CurrentState.With(currentPhase: GamePhase.ActionOnly);
            engine.ExecuteCommand(new PlaySpellCommand(1, spell.InstanceId));
            Assert.NotNull(engine.CurrentState.PendingInteraction);
            engine.Update(5.1f);
            Assert.Null(engine.CurrentState.PendingInteraction);
        }

        [Fact]
        public void UnitTargeting_ShouldAutoResolve_OnTimeout_WhenMultipleTargetsExist()
        {
            string json = @"[
                { ""Id"": 4, ""Name"": ""Informer"", ""Type"": ""Unit"", ""Cost"": 1, ""Attack"": 1, ""Health"": 2,
                  ""Effects"": [ { ""Trigger"": ""OnPlayed"", ""Targeting"": ""TargetEnemyUnit"", ""Actions"": [ { ""Type"": ""ApplyStatus"", ""StatusKeyword"": ""Marked"" } ] } ] },
                { ""Id"": 2, ""Name"": ""Target"", ""Type"": ""Unit"", ""Attack"": 1, ""Health"": 5 }
            ]";
            var engine = TestHelpers.CreateEngineWithCards(json);
            engine.CurrentState = engine.CurrentState.UpdateBoard(engine.CurrentState.Board
                .WithUnitPlacedAt(0, 2, engine.Factory.CreateCard(2, 2))
                .WithUnitPlacedAt(1, 2, engine.Factory.CreateCard(2, 2)));
            var informer = engine.Factory.CreateCard(4, 1);
            engine.CurrentState = engine.CurrentState.UpdatePlayer(engine.CurrentState.PlayerA.WithCardAddedToHand(informer));
            engine.ExecuteCommand(new PlayUnitCommand(1, informer.InstanceId, 3));
            Assert.NotNull(engine.CurrentState.PendingInteraction);
            engine.Update(5.0f);
            Assert.Null(engine.CurrentState.PendingInteraction);
        }
    }
}