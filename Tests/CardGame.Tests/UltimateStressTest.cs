using System.Collections.Generic;
using System.Linq;
using CardGame.Core.Application;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Models;
using CardGame.Core.Commands.Implementations;
using CardGame.Core.State.Enums;
using Xunit;

namespace CardGame.Tests
{
    public class NuclearChaosTests
    {
        private string GetFinalChaosJson() => @"[
            { ""Id"": 12, ""Name"": ""Black Cat"", ""Type"": ""Unit"", ""Cost"": 0, ""Attack"": 1, ""Health"": 1,
              ""Effects"": [ { ""Trigger"": ""OnSacrificed"", ""Zone"": ""Board"", ""Actions"": [ { ""Type"": ""ReturnToHand"", ""Target"": ""Self"" } ] } ] },
            { ""Id"": 36, ""Name"": ""Death"", ""Type"": ""Unit"", ""Subtypes"": [ ""Monster"" ], ""Cost"": 5, ""Attack"": 3, ""Health"": 3,
              ""Effects"": [ { ""Trigger"": ""OnFriendlyUnitDied"", ""Actions"": [ { ""Type"": ""DealDamage"", ""Target"": ""EnemyHero"", ""Amount"": 3 } ] } ] },
            { ""Id"": 35, ""Name"": ""Cathulli Captain"", ""Type"": ""Unit"", ""Cost"": 2, ""Attack"": 2, ""Health"": 2,
              ""Effects"": [ { 
                  ""Trigger"": ""OnDamagedEnemyHero"", 
                  ""Condition"": { ""Condition"": ""IsSubtype"", ""TargetParam"": ""Monster"" }, 
                  ""Actions"": [ { ""Type"": ""BuffStats"", ""Target"": ""SelectedTarget"", ""BuffAtk"": 1, ""BuffHp"": 1 } ] 
              } ] },
            { ""Id"": 27, ""Name"": ""Raiding Raptor"", ""Type"": ""Unit"", ""Attack"": 1, ""Health"": 4,
              ""Effects"": [ 
                { ""Trigger"": ""OnDamagedEnemyHero"", ""Condition"": { ""Condition"": ""IsSelf"" }, ""Actions"": [ { ""Type"": ""DrawCard"", ""Target"": ""FriendlyHero"" } ] },
                { ""Trigger"": ""OnFriendlyCardDrawn"", ""Actions"": [ { ""Type"": ""BuffStats"", ""Target"": ""Self"", ""BuffAtk"": 1 } ] } 
              ] },
            { ""Id"": 900, ""Name"": ""The Alluring Goblet"", ""Type"": ""Spell"", ""Cost"": 1,
              ""Effects"": [ { ""Trigger"": ""OnPlayed"", ""Targeting"": ""TargetFriendlyUnit"", ""Actions"": [ { ""Type"": ""DestroyUnit"" } ] } ] }
        ]";

        [Fact]
        public void Absolute_Nuclear_Butterfly_Effect_Test()
        {
            var engine = TestHelpers.CreateEngineWithCards(GetFinalChaosJson());
            var factory = engine.Factory;

            // Gracz 1 Board: Death (Monster), Cathulli Captain, Black Cat
            var death = factory.CreateCard(36, 1);
            var cap = factory.CreateCard(35, 1);
            var cat = factory.CreateCard(12, 1);
            var raptor = factory.CreateCard(27, 1); // Raptor też u nas, dla testu IsSelf

            engine.CurrentState = engine.CurrentState.UpdateBoard(engine.CurrentState.Board
                .WithUnitPlacedAt(0, 1, death)
                .WithUnitPlacedAt(1, 1, cap)
                .WithUnitPlacedAt(2, 1, cat)
                .WithUnitPlacedAt(3, 1, raptor));

            var goblet = factory.CreateCard(900, 1);
            engine.CurrentState = engine.CurrentState.UpdatePlayer(engine.CurrentState.PlayerA
                .With(hand: new List<CardInstance> { goblet }, drawPile: new List<CardInstance> { factory.CreateCard(12, 1) })
                .WithResourceChanged(ResourceType.Blood, 10, 10));

            engine.CurrentState = engine.CurrentState.With(currentPhase: GamePhase.ActionOnly);

            // ACT: Poświęcamy kota
            engine.ExecuteCommand(new PlaySpellCommand(1, goblet.InstanceId, selectedTargetId: cat.InstanceId));

            // ASSERTIONS
            var p1Board = engine.CurrentState.Board.GetAllUnits().Where(u => u.OwnerPlayerId == 1).ToList();
            var deathUnit = p1Board.First(u => u.Definition.Name == "Death");
            var raptorUnit = p1Board.First(u => u.Definition.Name == "Raiding Raptor");

            // 1. Death zadał 3 DMG bohaterowi wroga -> Captain to widział -> Death powinien mieć buffa +1/+1 (3/3 -> 4/4)
            Assert.Equal(4, deathUnit.CurrentStats.Attack);
            Assert.Equal(4, deathUnit.CurrentStats.Health);

            // 2. Raptor NIE powinien dobrać karty, mimo że bohater wroga dostał DMG (bo to nie Raptor uderzył - IsSelf zadziałało)
            // Ręka powinna zawierać tylko czarny kot, który wrócił. (Gdyby raptor dobrał, byłyby 2 karty).
            Assert.Single(engine.CurrentState.PlayerA.Hand);
            Assert.Contains(engine.CurrentState.PlayerA.Hand, c => c.Definition.Name == "Black Cat");

            // 3. Raptor NIE powinien mieć buffa do ataku (bo nie dobrał karty)
            Assert.Equal(1, raptorUnit.CurrentStats.Attack);
        }
    }
}