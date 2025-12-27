using System.Collections.Generic;
using System.Linq;
using CardGame.Core.Application;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Models;
using CardGame.Core.Commands.Implementations;
using CardGame.Core.State.Enums;
using CardGame.Core.GameRules.Battle;
using Xunit;

namespace CardGame.Tests
{
    public class WomboComboTests
    {
        [Fact]
        public void Ultimate_WomboCombo_Test()
        {
            string json = @"[
                { ""Id"": 12, ""Name"": ""Black Cat"", ""Type"": ""Unit"", ""Cost"": 0, ""Attack"": 1, ""Health"": 1,
                  ""Effects"": [ { ""Trigger"": ""OnSacrificed"", ""Zone"": ""Board"", ""Actions"": [ { ""Type"": ""ReturnToHand"", ""Target"": ""Self"" } ] } ] },
                { ""Id"": 6, ""Name"": ""Radio Demon"", ""Type"": ""Unit"", ""Cost"": 5, ""Attack"": 2, ""Health"": 2,
                  ""Effects"": [ { ""Trigger"": ""OnPlayed"", ""Actions"": [ { ""Type"": ""ModifyGlobalBuff"", ""BuffAtk"": 1, ""BuffHp"": 1 } ] } ] },
                { ""Id"": 10, ""Name"": ""Polar Bear"", ""Type"": ""Unit"", ""Cost"": 2, ""Attack"": 2, ""Health"": 3,
                  ""Effects"": [ { ""Trigger"": ""OnPlayed"", ""Targeting"": ""TargetFriendlyUnit"", ""Actions"": [ { ""Type"": ""SacrificeUnit"", ""Target"": ""SelectedTarget"" } ] } ] },
                { ""Id"": 9, ""Name"": ""Corpse Eater"", ""Type"": ""Unit"", ""Cost"": 3, ""Attack"": 3, ""Health"": 1,
                  ""Effects"": [ { ""Trigger"": ""OnFriendlyUnitDied"", ""Zone"": ""Hand"", ""Actions"": [ { ""Type"": ""SummonUnit"", ""Target"": ""Self"" } ] } ] },
                { ""Id"": 11, ""Name"": ""The Creature"", ""Type"": ""Unit"", ""Cost"": 7, ""Attack"": 7, ""Health"": 7,
                  ""Effects"": [ { ""Trigger"": ""OnOtherUnitSacrificed"", ""Zone"": ""Hand"", ""Actions"": [ { ""Type"": ""BuffStats"", ""Target"": ""Self"", ""Amount"": -1 } ] } ] },
                { ""Id"": 17, ""Name"": ""Megumin"", ""Type"": ""Unit"", ""Cost"": 4, ""Attack"": 3, ""Health"": 1, 
                  ""Keywords"": [""SplashDamage""], ""KeywordParams"": {""SplashDamage"": 3} }
            ]";

            var engine = TestHelpers.CreateEngineWithCards(json);
            var factory = engine.Factory;

            var cat = factory.CreateCard(12, 1);
            var radio = factory.CreateCard(6, 1);
            var bear = factory.CreateCard(10, 1);
            var eater = factory.CreateCard(9, 1);
            var creature = factory.CreateCard(11, 1);
            var megumin = factory.CreateCard(17, 2);

            engine.CurrentState = engine.CurrentState.UpdatePlayer(engine.CurrentState.PlayerA.With(
                hand: new[] { cat, radio, bear, eater, creature },
                health: 30).WithResourceChanged(ResourceType.Blood, 20, 20));

            engine.CurrentState = engine.CurrentState.UpdateBoard(engine.CurrentState.Board
                .WithUnitPlacedAt(1, 2, megumin));

            engine.ExecuteCommand(new PlayUnitCommand(1, cat.InstanceId, 3));
            engine.ExecuteCommand(new PlayUnitCommand(1, radio.InstanceId, 0));
            engine.ExecuteCommand(new PlayUnitCommand(1, bear.InstanceId, 2, selectedTargetId: cat.InstanceId));

            Assert.Contains(engine.CurrentState.PlayerA.Hand, c => c.Definition.Name == "Black Cat");
            Assert.NotNull(engine.CurrentState.Board.Lines[3].Player1Unit);
            Assert.Equal("Corpse Eater", engine.CurrentState.Board.Lines[3].Player1Unit!.Definition.Name);

            engine.CurrentState = engine.CurrentState.With(currentPhase: GamePhase.Combat);
            engine.ExecuteCommand(new EndPhaseCommand(1));

            Assert.Equal(27, engine.CurrentState.PlayerA.Health);
            Assert.Null(engine.CurrentState.Board.Lines[0].Player1Unit);
            Assert.Equal(1, engine.CurrentState.Board.Lines[2].Player1Unit!.CurrentStats.Health);
        }
    }
}