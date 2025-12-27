using System.Collections.Generic;
using System.Linq;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Models;
using CardGame.Core.Commands.Implementations;
using Xunit;

namespace CardGame.Tests
{
    public class MechanicsTests
    {
        [Fact]
        public void BuffSystem_ShouldBeAdditive_And_HealingShouldNotExceedMaxHp()
        {
            string json = @"[
                { ""Id"": 1, ""Name"": ""Tank"", ""Type"": ""Unit"", ""Cost"": 1, ""Attack"": 1, ""Health"": 5 },
                { ""Id"": 2, ""Name"": ""Buffer"", ""Type"": ""Unit"", ""Cost"": 1, ""Attack"": 1, ""Health"": 1,
                  ""Effects"": [ { ""Trigger"": ""OnPlayed"", ""Targeting"": ""AllFriendlyUnits"", ""Actions"": [ { ""Type"": ""BuffStats"", ""BuffAtk"": 2, ""BuffHp"": 2 } ] } ] },
                { ""Id"": 3, ""Name"": ""Healer"", ""Type"": ""Unit"", ""Cost"": 1, ""Attack"": 1, ""Health"": 1,
                  ""Effects"": [ { ""Trigger"": ""OnPlayed"", ""Targeting"": ""AllFriendlyUnits"", ""Actions"": [ { ""Type"": ""Heal"", ""Amount"": 10 } ] } ] },
                { ""Id"": 4, ""Name"": ""Damager"", ""Type"": ""Unit"", ""Cost"": 1, ""Attack"": 1, ""Health"": 1,
                  ""Effects"": [ { ""Trigger"": ""OnPlayed"", ""Targeting"": ""AllFriendlyUnits"", ""Actions"": [ { ""Type"": ""DealDamage"", ""Amount"": 4 } ] } ] }
            ]";

            var engine = TestHelpers.CreateEngineWithCards(json);
            var factory = engine.Factory;

            var tank = factory.CreateCard(1, 1);
            engine.CurrentState = engine.CurrentState.UpdateBoard(engine.CurrentState.Board.WithUnitPlacedAt(0, 1, tank));

            var pA = engine.CurrentState.PlayerA
                .WithCardAddedToHand(factory.CreateCard(2, 1))
                .WithCardAddedToHand(factory.CreateCard(3, 1))
                .WithCardAddedToHand(factory.CreateCard(4, 1))
                .WithResourceChanged(ResourceType.Blood, 10, 10);

            engine.CurrentState = engine.CurrentState.UpdatePlayer(pA);

            engine.ExecuteCommand(new PlayUnitCommand(1, pA.Hand[0].InstanceId, 1));
            var tankAfterBuff = engine.CurrentState.Board.Lines[0].Player1Unit;
            Assert.Equal(3, tankAfterBuff.CurrentStats.Attack);
            Assert.Equal(7, tankAfterBuff.CurrentStats.Health);

            engine.ExecuteCommand(new PlayUnitCommand(1, pA.Hand[2].InstanceId, 2));
            var tankAfterDmg = engine.CurrentState.Board.Lines[0].Player1Unit;
            Assert.Equal(3, tankAfterDmg.CurrentStats.Health);

            engine.ExecuteCommand(new PlayUnitCommand(1, pA.Hand[1].InstanceId, 3));
            var tankAfterHeal = engine.CurrentState.Board.Lines[0].Player1Unit;
            Assert.Equal(7, tankAfterHeal.CurrentStats.Health);
        }

        [Fact]
        public void ApplyStatus_ShouldAddKeyword_WithoutResettingStats()
        {
            string json = @"[
                { ""Id"": 1, ""Name"": ""Knight"", ""Type"": ""Unit"", ""Cost"": 1, ""Attack"": 2, ""Health"": 2 },
                { ""Id"": 2, ""Name"": ""Armorer"", ""Type"": ""Unit"", ""Cost"": 1, ""Attack"": 1, ""Health"": 1,
                  ""Effects"": [ { ""Trigger"": ""OnPlayed"", ""Targeting"": ""AllFriendlyUnits"", ""Actions"": [ { ""Type"": ""ApplyStatus"", ""StatusKeyword"": ""Armored"" } ] } ] }
            ]";

            var engine = TestHelpers.CreateEngineWithCards(json);
            var knight = engine.Factory.CreateCard(1, 1);
            engine.CurrentState = engine.CurrentState.UpdateBoard(engine.CurrentState.Board.WithUnitPlacedAt(0, 1, knight));

            var armorer = engine.Factory.CreateCard(2, 1);
            engine.CurrentState = engine.CurrentState.UpdatePlayer(engine.CurrentState.PlayerA
                .WithResourceChanged(ResourceType.Blood, 10, 10)
                .WithCardAddedToHand(armorer));

            engine.ExecuteCommand(new PlayUnitCommand(1, armorer.InstanceId, 1));

            var knightOnBoard = engine.CurrentState.Board.Lines[0].Player1Unit;
            Assert.Equal(2, knightOnBoard.CurrentStats.Attack);
            Assert.Contains(Keyword.Armored, knightOnBoard.CurrentStats.Keywords);
        }

        [Fact]
        public void SoulGuard_ShouldPreventDeathOnce_AndSetDepleted()
        {
            string json = @"[
                { ""Id"": 1, ""Name"": ""Guardian"", ""Type"": ""Unit"", ""Cost"": 1, ""Attack"": 1, ""Health"": 3, ""Keywords"": [""SoulGuard""] },
                { ""Id"": 2, ""Name"": ""Killer"", ""Type"": ""Unit"", ""Cost"": 1, ""Attack"": 1, ""Health"": 1,
                  ""Effects"": [ { ""Trigger"": ""OnPlayed"", ""Targeting"": ""AllFriendlyUnits"", ""Actions"": [ { ""Type"": ""DealDamage"", ""Amount"": 10 } ] } ] }
            ]";

            var engine = TestHelpers.CreateEngineWithCards(json);
            var guardian = engine.Factory.CreateCard(1, 1);
            engine.CurrentState = engine.CurrentState.UpdateBoard(engine.CurrentState.Board.WithUnitPlacedAt(0, 1, guardian));

            var killer = engine.Factory.CreateCard(2, 1);
            engine.CurrentState = engine.CurrentState.UpdatePlayer(engine.CurrentState.PlayerA
                .WithResourceChanged(ResourceType.Blood, 10, 10)
                .WithCardAddedToHand(killer));

            engine.ExecuteCommand(new PlayUnitCommand(1, killer.InstanceId, 1));
            var guardianSurvivor = engine.CurrentState.Board.Lines[0].Player1Unit;
            Assert.NotNull(guardianSurvivor);
            Assert.Equal(1, guardianSurvivor.CurrentStats.Health);
            Assert.Contains(Keyword.SoulGuardDepleted, guardianSurvivor.CurrentStats.Keywords);

            var killer2 = engine.Factory.CreateCard(2, 1);
            engine.CurrentState = engine.CurrentState.UpdatePlayer(engine.CurrentState.PlayerA.WithCardAddedToHand(killer2));
            engine.ExecuteCommand(new PlayUnitCommand(1, killer2.InstanceId, 2));

            Assert.Null(engine.CurrentState.Board.Lines[0].Player1Unit);
        }

        [Fact]
        public void Unkillable_ShouldReturnToHand_ResetStats()
        {
            string json = @"[
                { ""Id"": 1, ""Name"": ""Phoenix"", ""Type"": ""Unit"", ""Cost"": 1, ""Attack"": 1, ""Health"": 1, ""Keywords"": [""Unkillable""] },
                { ""Id"": 2, ""Name"": ""Killer"", ""Type"": ""Unit"", ""Cost"": 1, ""Attack"": 1, ""Health"": 1,
                  ""Effects"": [ { ""Trigger"": ""OnPlayed"", ""Targeting"": ""AllFriendlyUnits"", ""Actions"": [ { ""Type"": ""DealDamage"", ""Amount"": 10 } ] } ] }
            ]";

            var engine = TestHelpers.CreateEngineWithCards(json);
            var phoenix = engine.Factory.CreateCard(1, 1);
            var buffedPhoenix = phoenix.AddPermanentBuff(new CardStats(5, 5, 0));

            engine.CurrentState = engine.CurrentState.UpdateBoard(engine.CurrentState.Board.WithUnitPlacedAt(0, 1, buffedPhoenix));

            var killer = engine.Factory.CreateCard(2, 1);
            engine.CurrentState = engine.CurrentState.UpdatePlayer(engine.CurrentState.PlayerA
                .WithResourceChanged(ResourceType.Blood, 10, 10)
                .WithCardAddedToHand(killer));

            engine.ExecuteCommand(new PlayUnitCommand(1, killer.InstanceId, 1));

            Assert.Null(engine.CurrentState.Board.Lines[0].Player1Unit);
            var phoenixInHand = engine.CurrentState.PlayerA.Hand.Last();
            Assert.Equal("Phoenix", phoenixInHand.Definition.Name);
            Assert.Equal(1, phoenixInHand.CurrentStats.Attack);
            Assert.Equal(1, phoenixInHand.CurrentStats.Health);
        }
    }
}