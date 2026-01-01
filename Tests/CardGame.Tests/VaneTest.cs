using Xunit;
using CardGame.Core.State.Enums;
using CardGame.Core.Commands.Implementations;
using CardGame.Core.Cards.Data;
using System.Linq;

namespace CardGame.Tests
{
    public class VaneButcherTests
    {
        private string GetVaneJson() => @"[
            { ""Id"": 48, ""Name"": ""Vane"", ""Type"": ""Unit"", ""Attack"": 3, ""Health"": 3,
              ""Effects"": [
                { ""Trigger"": ""OnOtherUnitSacrificed"", ""Actions"": [ { ""Type"": ""BuffStats"", ""Target"": ""Self"", ""BuffAtk"": 1, ""BuffHp"": 1 } ] },
                { ""Trigger"": ""OnOtherUnitSacrificed"", ""Condition"": { ""Condition"": ""IsSubtype"", ""TargetParam"": ""Animal"" }, 
                  ""Actions"": [ { ""Type"": ""BuffStats"", ""Target"": ""Self"", ""BuffAtk"": 1, ""BuffHp"": 1 } ] }
              ] 
            },
            { ""Id"": 12, ""Name"": ""Black Cat"", ""Type"": ""Unit"", ""Subtypes"": [""Animal""], ""Attack"": 1, ""Health"": 1 },
            { ""Id"": 10, ""Name"": ""Human Victim"", ""Type"": ""Unit"", ""Subtypes"": [""Human""], ""Attack"": 1, ""Health"": 1 },
            { ""Id"": 100, ""Name"": ""Sacrifice Spell"", ""Type"": ""Spell"", ""Cost"": 0,
              ""Effects"": [ { ""Trigger"": ""OnPlayed"", ""Actions"": [ { ""Type"": ""SacrificeUnit"", ""Target"": ""TargetFriendlyUnit"" } ] } ] }
        ]";

        [Fact]
        public void Vane_ShouldGainDoubleBonus_FromHumanSacrifice()
        {
            var engine = TestHelpers.CreateEngineWithCards(@"[
        { ""Id"": 48, ""Name"": ""Vane"", ""Type"": ""Unit"", ""Subtypes"": [""Human""], ""Attack"": 3, ""Health"": 3,
          ""Effects"": [
            { ""Trigger"": ""OnOtherUnitSacrificed"", ""Actions"": [ { ""Type"": ""BuffStats"", ""Target"": ""Self"", ""BuffAtk"": 1, ""BuffHp"": 1 } ] },
            { ""Trigger"": ""OnOtherUnitSacrificed"", ""Condition"": { ""Condition"": ""IsSubtype"", ""TargetParam"": ""Human"" }, 
              ""Actions"": [ { ""Type"": ""BuffStats"", ""Target"": ""Self"", ""BuffAtk"": 1, ""BuffHp"": 1 } ] }
          ] 
        },
        { ""Id"": 4, ""Name"": ""Informer"", ""Type"": ""Unit"", ""Subtypes"": [""Human""], ""Attack"": 1, ""Health"": 1 },
        { ""Id"": 100, ""Name"": ""Sac"", ""Type"": ""Spell"", ""Cost"": 0, ""Effects"": [ { ""Trigger"": ""OnPlayed"", ""Actions"": [ { ""Type"": ""SacrificeUnit"", ""Target"": ""TargetFriendlyUnit"" } ] } ] }
    ]");

            var f = engine.Factory;
            var vane = f.CreateCard(48, 1);
            var human = f.CreateCard(4, 1);
            var spell = f.CreateCard(100, 1);

            engine.CurrentState = engine.CurrentState.UpdateBoard(engine.CurrentState.Board
                .WithUnitPlacedAt(0, 1, vane)
                .WithUnitPlacedAt(1, 1, human));
            engine.CurrentState = engine.CurrentState.UpdatePlayer(engine.CurrentState.PlayerA.WithCardAddedToHand(spell));
            engine.CurrentState = engine.CurrentState.With(currentPhase: GamePhase.ActionOnly);

            // ACT: Poświęcamy człowieka
            engine.ExecuteCommand(new PlaySpellCommand(1, spell.InstanceId, selectedTargetId: human.InstanceId));

            // ASSERT: 3/3 + 1/1 (any) + 1/1 (human) = 5/5
            var result = engine.CurrentState.Board.Lines[0].Player1Unit!;
            Assert.Equal(5, result.CurrentStats.Attack);
        }
    }
}