using System.Collections.Generic;
using System.Linq;
using CardGame.Core.Application;
using CardGame.Core.Cards.Data;
using CardGame.Core.Commands.Implementations;
using CardGame.Core.State.Enums;
using CardGame.Core.State.Models;
using Xunit;

namespace CardGame.Tests
{
    public class NewAddedCardsTests
    {
        private GameEngine CreateEngine()
        {
            // Pełny JSON zawierający nowe karty i niezbędne karty pomocnicze do przeprowadzenia testów
            string json = @"[
                { ""Id"": 8, ""Name"": ""Crusader"", ""Type"": ""Unit"", ""Subtypes"": [ ""Human"" ], ""Cost"": 2, ""Attack"": 2, ""Health"": 3, ""Keywords"": [ ""Armored"" ] },
                { ""Id"": 42, ""Name"": ""Soldat"", ""Type"": ""Unit"", ""Subtypes"": [""Mercenary""], ""Cost"": 1, ""Attack"": 2, ""Health"": 2 },
                { ""Id"": 44, ""Name"": ""Machine Bomber"", ""Type"": ""Unit"", ""Subtypes"": [""Machine""], ""Cost"": 1, ""Attack"": 1, ""Health"": 2, ""Keywords"": [""SplashDamage""], ""KeywordParams"": { ""SplashDamage"": 1 } },
                { ""Id"": 51, ""Name"": ""The Trapper"", ""Type"": ""Unit"", ""Subtypes"": [""Monster"", ""Human""], ""Cost"": 2, ""Attack"": 2, ""Health"": 2, ""Effects"": [ { ""Trigger"": ""OnPlayed"", ""Actions"": [ { ""Type"": ""AddCardToHand"", ""Target"": ""FriendlyHero"", ""ValueParam"": 903 } ] } ] },
                { ""Id"": 52, ""Name"": ""Little Bob"", ""Type"": ""Unit"", ""Subtypes"": [""Machine""], ""Cost"": 1, ""Attack"": 1, ""Health"": 1, ""Effects"": [ { ""Trigger"": ""OnPlayed"", ""Condition"": { ""Condition"": ""And"", ""SubConditions"": [ { ""Condition"": ""IsSubtype"", ""TargetParam"": ""Machine"" }, { ""Condition"": ""Not"", ""SubConditions"": [{ ""Condition"": ""IsSelf"" }] } ] }, ""Zone"": ""Board"", ""Actions"": [ { ""Type"": ""BuffStats"", ""Target"": ""Self"", ""BuffAtk"": 1, ""BuffHp"": 1 } ] } ] },
                { ""Id"": 53, ""Name"": ""Finale"", ""Type"": ""Spell"", ""Cost"": 7, ""Effects"": [ { ""Trigger"": ""OnPlayed"", ""Actions"": [ { ""Type"": ""ApplyStatus"", ""Target"": ""AllEnemyUnits"", ""StatusKeyword"": ""Marked"" }, { ""Type"": ""DestroyUnit"", ""Target"": ""AllEnemyUnits"" } ] } ] },
                { ""Id"": 54, ""Name"": ""Thor"", ""Type"": ""Unit"", ""Subtypes"": [""Machine"", ""Mercenary""], ""Cost"": 4, ""Attack"": 0, ""Health"": 4, ""Effects"": [ { ""Trigger"": ""OnFriendlyUnitDied"", ""Zone"": ""Hand"", ""Condition"": { ""Condition"": ""IsSubtype"", ""TargetParam"": ""Machine"" }, ""Actions"": [ { ""Type"": ""BuffStats"", ""Target"": ""Self"", ""BuffAtk"": 2 } ] } ] },
                { ""Id"": 55, ""Name"": ""ZaUm"", ""Type"": ""Unit"", ""Subtypes"": [""Machine""], ""Cost"": 4, ""Attack"": 4, ""Health"": 5 },
                { ""Id"": 56, ""Name"": ""Collector"", ""Type"": ""Unit"", ""Subtypes"": [""Monster""], ""Cost"": 7, ""Attack"": 2, ""Health"": 6, ""Effects"": [ { ""Trigger"": ""OnPlayed"", ""Actions"": [ { ""Type"": ""MakeAUnit"", ""ValueParam"": 904 }, { ""Type"": ""MakeAUnit"", ""ValueParam"": 905 }, { ""Type"": ""MakeAUnit"", ""ValueParam"": 906 } ] } ] },
                { ""Id"": 57, ""Name"": ""White Mourning"", ""Type"": ""Spell"", ""Cost"": 3, ""Effects"": [ { ""Trigger"": ""OnPlayed"", ""Actions"": [ { ""Type"": ""Silence"", ""Target"": ""AllUnitsOnBoard"" } ] } ] },
                { ""Id"": 58, ""Name"": ""Excision Conjucture"", ""Type"": ""Spell"", ""Cost"": 2, ""Effects"": [ { ""Trigger"": ""OnPlayed"", ""Targeting"": ""TargetEnemyUnit"", ""Actions"": [ { ""Type"": ""BuffStats"", ""Target"": ""SelectedTarget"", ""BuffAtk"": -2, ""BuffHp"": -2 } ] } ] },
                { ""Id"": 903, ""Name"": ""Trap"", ""Type"": ""Unit"", ""Subtypes"": [""Machine"", ""Token""], ""Cost"": 1, ""Attack"": 0, ""Health"": 1, ""Effects"": [ { ""Trigger"": ""OnDeath"", ""Actions"": [ { ""Type"": ""ApplyStatus"", ""Target"": ""TargetEnemyUnit"", ""StatusKeyword"": ""Stunned"" } ] } ] },
                { ""Id"": 904, ""Name"": ""Traces of Sylvian"", ""Type"": ""Unit"", ""Subtypes"": [""Monster"", ""Collection"", ""Token""], ""Cost"": 3, ""Attack"": 2, ""Health"": 2 },
                { ""Id"": 905, ""Name"": ""Traces of Almer"", ""Type"": ""Unit"", ""Subtypes"": [""Monster"", ""Collection"", ""Token""], ""Cost"": 3, ""Attack"": 1, ""Health"": 3 },
                { ""Id"": 906, ""Name"": ""Traces of Grogorth"", ""Type"": ""Unit"", ""Subtypes"": [""Monster"", ""Collection"", ""Token""], ""Cost"": 3, ""Attack"": 3, ""Health"": 1 }
            ]";
            return TestHelpers.CreateEngineWithCards(json);
        }

        [Fact]
        public void TheTrapper_Id51_ShouldAddTrapToHand()
        {
            var engine = CreateEngine();
            var trapper = engine.Factory.CreateCard(51, 1);
            engine.CurrentState = engine.CurrentState.UpdatePlayer(engine.CurrentState.PlayerA.WithCardAddedToHand(trapper));

            engine.ExecuteCommand(new PlayUnitCommand(1, trapper.InstanceId, 0));

            Assert.Contains(engine.CurrentState.PlayerA.Hand, c => c.Definition.Id == "903");
        }

        [Fact]
        public void LittleBob_Id52_ShouldBuffWhenOtherMachinePlayed()
        {
            var engine = CreateEngine();
            var bob = engine.Factory.CreateCard(52, 1);
            var otherMachine = engine.Factory.CreateCard(55, 1); // ZaUm (Machine)

            engine.CurrentState = engine.CurrentState.UpdateBoard(engine.CurrentState.Board.WithUnitPlacedAt(0, 1, bob));
            engine.CurrentState = engine.CurrentState.UpdatePlayer(engine.CurrentState.PlayerA.WithCardAddedToHand(otherMachine));

            engine.ExecuteCommand(new PlayUnitCommand(1, otherMachine.InstanceId, 1));

            var bobOnBoard = engine.CurrentState.Board.Lines[0].Player1Unit;
            Assert.Equal(2, bobOnBoard.CurrentStats.Attack);
            Assert.Equal(2, bobOnBoard.CurrentStats.Health);
        }

        [Fact]
        public void Finale_Id53_ShouldMarkAndDestroyAllEnemies()
        {
            var engine = CreateEngine();
            var e1 = engine.Factory.CreateCard(42, 2); // Soldat
            var e2 = engine.Factory.CreateCard(42, 2);
            var finale = engine.Factory.CreateCard(53, 1);

            engine.CurrentState = engine.CurrentState.UpdateBoard(engine.CurrentState.Board
                .WithUnitPlacedAt(0, 2, e1)
                .WithUnitPlacedAt(1, 2, e2));
            engine.CurrentState = engine.CurrentState.UpdatePlayer(engine.CurrentState.PlayerA.WithCardAddedToHand(finale));
            engine.CurrentState = engine.CurrentState.With(currentPhase: GamePhase.ActionOnly);

            engine.ExecuteCommand(new PlaySpellCommand(1, finale.InstanceId));

            Assert.Empty(engine.CurrentState.Board.GetAllUnits().Where(u => u.OwnerPlayerId == 2));
        }

        [Fact]
        public void Thor_Id54_ShouldGainAttackInHand_WhenMachineDies()
        {
            var engine = CreateEngine();
            var thor = engine.Factory.CreateCard(54, 1);
            var machineFodder = engine.Factory.CreateCard(44, 1); // Machine Bomber

            engine.CurrentState = engine.CurrentState.UpdatePlayer(engine.CurrentState.PlayerA.WithCardAddedToHand(thor));
            engine.CurrentState = engine.CurrentState.UpdateBoard(engine.CurrentState.Board.WithUnitPlacedAt(0, 1, machineFodder));

            // ACT: Zabijamy maszynę
            var deadMachine = machineFodder.TakeDamage(10);
            engine.CurrentState = engine.CurrentState.UpdateBoard(engine.CurrentState.Board.UpdateUnit(deadMachine));
            engine.ExecuteCommand(new EndPhaseCommand(1));

            var thorInHand = engine.CurrentState.PlayerA.Hand.First(c => c.Definition.Id == "54");
            Assert.Equal(2, thorInHand.CurrentStats.Attack);
        }

        [Fact]
        public void Collector_Id56_ShouldSpawnAllThreeTraces()
        {
            var engine = CreateEngine();
            var collector = engine.Factory.CreateCard(56, 1);
            engine.CurrentState = engine.CurrentState.UpdatePlayer(engine.CurrentState.PlayerA.WithCardAddedToHand(collector));

            engine.ExecuteCommand(new PlayUnitCommand(1, collector.InstanceId, 0));

            var units = engine.CurrentState.Board.GetAllUnits().Where(u => u.OwnerPlayerId == 1).ToList();
            Assert.Contains(units, u => u.Definition.Id == "904");
            Assert.Contains(units, u => u.Definition.Id == "905");
            Assert.Contains(units, u => u.Definition.Id == "906");
            Assert.Equal(4, units.Count);
        }

        [Fact]
        public void WhiteMourning_Id57_ShouldSilenceEveryone()
        {
            var engine = CreateEngine();
            var unitWithKeywords = engine.Factory.CreateCard(8, 2); // Crusader (Armored)
            var mourning = engine.Factory.CreateCard(57, 1);

            engine.CurrentState = engine.CurrentState.UpdateBoard(engine.CurrentState.Board.WithUnitPlacedAt(0, 2, unitWithKeywords));
            engine.CurrentState = engine.CurrentState.UpdatePlayer(engine.CurrentState.PlayerA.WithCardAddedToHand(mourning));
            engine.CurrentState = engine.CurrentState.With(currentPhase: GamePhase.ActionOnly);

            engine.ExecuteCommand(new PlaySpellCommand(1, mourning.InstanceId));

            var target = engine.CurrentState.Board.Lines[0].Player2Unit;
            Assert.True(target.IsSilenced);
            Assert.DoesNotContain(Keyword.Armored, target.CurrentStats.Keywords);
        }

        [Fact]
        public void ExcisionConjucture_Id58_ShouldKillUnitWithTwoHp()
        {
            var engine = CreateEngine();
            var victim = engine.Factory.CreateCard(42, 2); // Soldat 2/2
            var spell = engine.Factory.CreateCard(58, 1);

            engine.CurrentState = engine.CurrentState.UpdateBoard(engine.CurrentState.Board.WithUnitPlacedAt(0, 2, victim));
            engine.CurrentState = engine.CurrentState.UpdatePlayer(engine.CurrentState.PlayerA.WithCardAddedToHand(spell));
            engine.CurrentState = engine.CurrentState.With(currentPhase: GamePhase.ActionOnly);

            engine.ExecuteCommand(new PlaySpellCommand(1, spell.InstanceId, selectedTargetId: victim.InstanceId));

            Assert.Null(engine.CurrentState.Board.Lines[0].Player2Unit);
            Assert.Contains(engine.CurrentState.PlayerB.DiscardPile, c => c.InstanceId == victim.InstanceId);
        }

        [Fact]
        public void Trap_Id903_ShouldStunOnDeath()
        {
            var engine = CreateEngine();
            var trap = engine.Factory.CreateCard(903, 1);
            var victim = engine.Factory.CreateCard(42, 2);

            engine.CurrentState = engine.CurrentState.UpdateBoard(engine.CurrentState.Board
                .WithUnitPlacedAt(0, 1, trap)
                .WithUnitPlacedAt(0, 2, victim));

            var deadTrap = trap.TakeDamage(5);
            engine.CurrentState = engine.CurrentState.UpdateBoard(engine.CurrentState.Board.UpdateUnit(deadTrap));
            engine.ExecuteCommand(new EndPhaseCommand(1));

            var stunnedUnit = engine.CurrentState.Board.Lines[0].Player2Unit;
            Assert.Contains(Keyword.Stunned, stunnedUnit.CurrentStats.Keywords);
        }
    }
}