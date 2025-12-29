using System.Collections.Generic;
using System.Linq;
using CardGame.Core.Application;
using CardGame.Core.Cards.Data;
using CardGame.Core.Commands.Implementations;
using CardGame.Core.State.Enums;
using Xunit;

namespace CardGame.Tests
{
    public class NewMechanicsPt2Tests
    {
        private string GetJson() => @"[
            { ""Id"": 25, ""Name"": ""Bounty Hunter"", ""Type"": ""Unit"", ""Attack"": 3, ""Health"": 3,
              ""Effects"": [ { ""Trigger"": ""OnStatusApplied"", ""Condition"": { ""Condition"": ""IsStatus"", ""TargetParam"": ""Marked"" }, ""Actions"": [ { ""Type"": ""ApplyStatus"", ""Target"": ""SelectedTarget"", ""StatusKeyword"": ""Stunned"" } ] } ] },
            { ""Id"": 28, ""Name"": ""Exploding Fruitcake"", ""Type"": ""Spell"", ""Cost"": 2,
              ""Effects"": [ { ""Trigger"": ""OnPlayed"", ""Actions"": [ { ""Type"": ""DealDamage"", ""Target"": ""TargetEnemyUnit"", ""Amount"": 6 }, { ""Type"": ""GiveToOpponent"", ""Target"": ""Self"" } ] } ] },
            { ""Id"": 29, ""Name"": ""Blighteinstein"", ""Type"": ""Unit"", ""Attack"": 5, ""Health"": 6,
              ""Effects"": [ { ""Trigger"": ""OnKill"", ""Actions"": [ { ""Type"": ""MoveRight"", ""Target"": ""Self"" } ] } ] },
            { ""Id"": 30, ""Name"": ""SharkCannon"", ""Type"": ""Unit"", ""Attack"": 0, ""Health"": 7,
              ""Effects"": [ { ""Trigger"": ""OnPreCombatLine"", ""Actions"": [ { ""Type"": ""DealDamage"", ""Target"": ""EnemyHero"", ""Amount"": 5 } ] } ] },
            { ""Id"": 4, ""Name"": ""Informer"", ""Type"": ""Unit"", ""Cost"": 1, ""Attack"": 1, ""Health"": 2,
              ""Effects"": [ { ""Trigger"": ""OnPlayed"", ""Targeting"": ""TargetEnemyUnit"", ""Actions"": [ { ""Type"": ""ApplyStatus"", ""Target"": ""SelectedTarget"", ""StatusKeyword"": ""Marked"" } ] } ] },
            { ""Id"": 99, ""Name"": ""Victim"", ""Type"": ""Unit"", ""Attack"": 2, ""Health"": 1 }
        ]";

        [Fact]
        public void Stun_ShouldPreventAttack_AndThenVanish()
        {
            var engine = TestHelpers.CreateEngineWithCards(GetJson());
            var hunter = engine.Factory.CreateCard(25, 1);
            var victim = engine.Factory.CreateCard(99, 2);
            var informer = engine.Factory.CreateCard(4, 1);
            engine.CurrentState = engine.CurrentState.UpdateBoard(engine.CurrentState.Board.WithUnitPlacedAt(0, 1, hunter).WithUnitPlacedAt(0, 2, victim));
            engine.CurrentState = engine.CurrentState.UpdatePlayer(engine.CurrentState.PlayerA.WithCardAddedToHand(informer));
            engine.ExecuteCommand(new PlayUnitCommand(1, informer.InstanceId, 3));
            var victimAfterStun = engine.CurrentState.Board.Lines[0].Player2Unit!;
            Assert.Contains(Keyword.Stunned, victimAfterStun.CurrentStats.Keywords);
            engine.CurrentState = engine.CurrentState.With(currentPhase: GamePhase.Combat);
            engine.ExecuteCommand(new EndPhaseCommand(1));
            var hunterAfter = engine.CurrentState.Board.Lines[0].Player1Unit!;
            Assert.Equal(3, hunterAfter.CurrentStats.Health);
        }

        [Fact]
        public void SharkCannon_ShouldShoot_BeforeCombat()
        {
            var engine = TestHelpers.CreateEngineWithCards(GetJson());
            var cannon = engine.Factory.CreateCard(30, 1);
            engine.CurrentState = engine.CurrentState.UpdateBoard(engine.CurrentState.Board.WithUnitPlacedAt(0, 1, cannon));
            engine.CurrentState = engine.CurrentState.With(currentPhase: GamePhase.Combat);
            engine.ExecuteCommand(new EndPhaseCommand(1));
            Assert.Equal(25, engine.CurrentState.PlayerB.Health);
        }

        [Fact]
        public void Blighteinstein_ShouldMoveRight_OnKill()
        {
            var engine = TestHelpers.CreateEngineWithCards(GetJson());
            var blight = engine.Factory.CreateCard(29, 1);
            var victim = engine.Factory.CreateCard(99, 2);
            engine.CurrentState = engine.CurrentState.UpdateBoard(engine.CurrentState.Board.WithUnitPlacedAt(1, 1, blight).WithUnitPlacedAt(1, 2, victim));
            engine.CurrentState = engine.CurrentState.With(currentPhase: GamePhase.Combat);
            engine.ExecuteCommand(new EndPhaseCommand(1));
            Assert.Null(engine.CurrentState.Board.Lines[1].Player1Unit);
            Assert.NotNull(engine.CurrentState.Board.Lines[2].Player1Unit);
        }

        [Fact]
        public void Fruitcake_ShouldGiveCopy_ToOpponent()
        {
            var engine = TestHelpers.CreateEngineWithCards(GetJson());
            var cake = engine.Factory.CreateCard(28, 1);
            var victim = engine.Factory.CreateCard(99, 2);
            engine.CurrentState = engine.CurrentState.UpdateBoard(engine.CurrentState.Board.WithUnitPlacedAt(0, 2, victim));
            engine.CurrentState = engine.CurrentState.UpdatePlayer(engine.CurrentState.PlayerA.WithCardAddedToHand(cake));
            engine.CurrentState = engine.CurrentState.With(currentPhase: GamePhase.ActionOnly);
            engine.ExecuteCommand(new PlaySpellCommand(1, cake.InstanceId));
            Assert.Contains(engine.CurrentState.PlayerB.Hand, c => c.Definition.Name == "Exploding Fruitcake");
        }
    }
}