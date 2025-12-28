using System.Collections.Generic;
using System.Linq;
using CardGame.Core.Application;
using CardGame.Core.Cards.Data;
using CardGame.Core.Commands.Implementations;
using CardGame.Core.State.Enums;
using Xunit;

namespace CardGame.Tests
{
    public class NewCardsMechanicsTests
    {
        private string GetFullCardsJson() => @"[
            { ""Id"": 4, ""Name"": ""Informer"", ""Type"": ""Unit"", ""Cost"": 1, ""Attack"": 1, ""Health"": 2,
              ""Effects"": [ { ""Trigger"": ""OnPlayed"", ""Actions"": [ { ""Type"": ""ApplyStatus"", ""Target"": ""TargetEnemyUnit"", ""StatusKeyword"": ""Marked"" } ] } ] },
            { ""Id"": 18, ""Name"": ""Final Mission"", ""Type"": ""Spell"", ""Cost"": 1,
              ""Effects"": [ { ""Trigger"": ""OnPlayed"", ""Actions"": [ 
                  { ""Type"": ""SacrificeUnit"", ""Target"": ""TargetFriendlyUnit"" }, 
                  { ""Type"": ""DealDamage"", ""Target"": ""TargetEnemyUnit"", ""Amount"": 4 } 
              ] } ] },
            { ""Id"": 21, ""Name"": ""Juggernaut"", ""Type"": ""Unit"", ""Cost"": 4, ""Attack"": 4, ""Health"": 2, ""Keywords"": [""Armored""], ""KeywordParams"": { ""Armored"": 2 },
              ""Effects"": [ { ""Trigger"": ""OnDamagedEnemyUnit"", ""Actions"": [ { ""Type"": ""DealDamage"", ""Target"": ""EnemyHero"", ""Amount"": 2 } ] } ] },
            { ""Id"": 22, ""Name"": ""QuickDraw Gun Man"", ""Type"": ""Unit"", ""Cost"": 1, ""Attack"": 1, ""Health"": 3,
              ""Effects"": [ { ""Trigger"": ""OnOpponentCardDrawn"", ""Actions"": [ { ""Type"": ""DealDamage"", ""Target"": ""EnemyHero"", ""Amount"": 1 } ] } ] },
            { ""Id"": 23, ""Name"": ""FireAxe Man"", ""Type"": ""Unit"", ""Subtypes"": [ ""Monster"" ], ""Cost"": 5, ""Attack"": 4, ""Health"": 6,
              ""Effects"": [ { ""Trigger"": ""OnKill"", ""Actions"": [ { ""Type"": ""HealToFull"", ""Target"": ""Self"" }, { ""Type"": ""BuffStats"", ""Target"": ""Self"", ""BuffHp"": 2 } ] } ] },
            { ""Id"": 24, ""Name"": ""Gerard from Rivia"", ""Type"": ""Unit"", ""Cost"": 5, ""Attack"": 5, ""Health"": 5,
              ""Effects"": [ { ""Trigger"": ""OnStatusApplied"", ""Condition"": { ""Condition"": ""IsStatus"", ""TargetParam"": ""Marked"" }, ""Actions"": [ { ""Type"": ""BonusAttack"", ""Target"": ""Self"" } ] } ] },
            { ""Id"": 99, ""Name"": ""Victim"", ""Type"": ""Unit"", ""Attack"": 1, ""Health"": 1 }
        ]";

        [Fact]
        public void FinalMission_ShouldBlock_WhenNoFriendlyUnits()
        {
            var engine = TestHelpers.CreateEngineWithCards(GetFullCardsJson());
            var spell = engine.Factory.CreateCard(18, 1);
            engine.CurrentState = engine.CurrentState.UpdatePlayer(engine.CurrentState.PlayerA.WithCardAddedToHand(spell));

            engine.ExecuteCommand(new PlaySpellCommand(1, spell.InstanceId));

            Assert.Contains(engine.CurrentState.PlayerA.Hand, c => c.InstanceId == spell.InstanceId);
            Assert.Null(engine.CurrentState.PendingInteraction);
        }

        [Fact]
        public void FinalMission_ShouldSacrificeAndSkipDamage_WhenNoEnemyUnits()
        {
            var engine = TestHelpers.CreateEngineWithCards(GetFullCardsJson());
            var spell = engine.Factory.CreateCard(18, 1);
            var myUnit = engine.Factory.CreateCard(99, 1);

            engine.CurrentState = engine.CurrentState.UpdateBoard(engine.CurrentState.Board.WithUnitPlacedAt(0, 1, myUnit));
            engine.CurrentState = engine.CurrentState.UpdatePlayer(engine.CurrentState.PlayerA.WithCardAddedToHand(spell));
            engine.CurrentState = engine.CurrentState.With(currentPhase: GamePhase.ActionOnly);

            engine.ExecuteCommand(new PlaySpellCommand(1, spell.InstanceId));
            Assert.NotNull(engine.CurrentState.PendingInteraction);

            engine.ExecuteCommand(new SelectTargetCommand(1, myUnit.InstanceId));

            Assert.Null(engine.CurrentState.Board.Lines[0].Player1Unit);
            Assert.Null(engine.CurrentState.PendingInteraction);
        }

        [Fact]
        public void Juggernaut_ShouldReduceDamageByTwo_AndPingHero()
        {
            var engine = TestHelpers.CreateEngineWithCards(GetFullCardsJson());
            var jugg = engine.Factory.CreateCard(21, 1);
            var victim = engine.Factory.CreateCard(99, 2);

            engine.CurrentState = engine.CurrentState.UpdateBoard(engine.CurrentState.Board
                .WithUnitPlacedAt(0, 1, jugg)
                .WithUnitPlacedAt(0, 2, victim));

            engine.CurrentState = engine.CurrentState.With(currentPhase: GamePhase.Combat);
            engine.ExecuteCommand(new EndPhaseCommand(1));

            Assert.Equal(28, engine.CurrentState.PlayerB.Health);
            Assert.Equal(2, engine.CurrentState.Board.Lines[0].Player1Unit!.CurrentStats.Health);
        }

        [Fact]
        public void Gerard_ShouldBonusAttack_WhenEnemyIsMarked()
        {
            var engine = TestHelpers.CreateEngineWithCards(GetFullCardsJson());
            var gerard = engine.Factory.CreateCard(24, 1);
            var victim = engine.Factory.CreateCard(99, 2);
            var informer = engine.Factory.CreateCard(4, 1);

            engine.CurrentState = engine.CurrentState.UpdateBoard(engine.CurrentState.Board
                .WithUnitPlacedAt(0, 1, gerard)
                .WithUnitPlacedAt(0, 2, victim));

            engine.CurrentState = engine.CurrentState.UpdatePlayer(engine.CurrentState.PlayerA.WithCardAddedToHand(informer));
            engine.ExecuteCommand(new PlayUnitCommand(1, informer.InstanceId, 3, selectedTargetId: victim.InstanceId));

            Assert.Null(engine.CurrentState.Board.Lines[0].Player2Unit);
        }

        [Fact]
        public void FireAxeMan_ShouldHealAndBuff_OnKill()
        {
            var engine = TestHelpers.CreateEngineWithCards(GetFullCardsJson());
            var axeMan = engine.Factory.CreateCard(23, 1);
            var victim = engine.Factory.CreateCard(99, 2);
            axeMan = axeMan.TakeDamage(4);

            engine.CurrentState = engine.CurrentState.UpdateBoard(engine.CurrentState.Board
                .WithUnitPlacedAt(0, 1, axeMan)
                .WithUnitPlacedAt(0, 2, victim));

            engine.CurrentState = engine.CurrentState.With(currentPhase: GamePhase.Combat);
            engine.ExecuteCommand(new EndPhaseCommand(1));

            var axeManAfter = engine.CurrentState.Board.Lines[0].Player1Unit!;
            Assert.Equal(8, axeManAfter.CurrentStats.Health);
            Assert.Equal(8, axeManAfter.MaxHealth);
        }

        [Fact]
        public void QuickDraw_ShouldPunishOpponentDrawing()
        {
            var engine = TestHelpers.CreateEngineWithCards(GetFullCardsJson());
            var gunMan = engine.Factory.CreateCard(22, 1);
            engine.CurrentState = engine.CurrentState.UpdateBoard(engine.CurrentState.Board.WithUnitPlacedAt(0, 1, gunMan));

            var token = engine.Factory.CreateCard(99, 2);
            var playerBWithDeck = engine.CurrentState.PlayerB.With(drawPile: new List<CardGame.Core.Cards.Models.CardInstance> { token });
            engine.CurrentState = engine.CurrentState.UpdatePlayer(playerBWithDeck);

            engine.CurrentState = engine.CurrentState.UpdatePlayer(engine.CurrentState.PlayerB.WithCardDrawn(engine.Events));
            engine.ExecuteCommand(new EndPhaseCommand(1));

            Assert.Equal(29, engine.CurrentState.PlayerB.Health);
        }
    }
}