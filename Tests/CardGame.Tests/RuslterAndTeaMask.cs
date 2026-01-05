using Xunit;
using CardGame.Core.State.Enums;
using CardGame.Core.Commands.Implementations;
using CardGame.Core.GameRules.Auras;
using CardGame.Core.Cards.Data; // Dodane dla TargetType i TriggerType
using System.Linq;

namespace CardGame.Tests
{
    public class ComboMechanicsTests
    {
        [Fact]
        public void TeaMaid_ShouldReduceSpellCost_WhileOnBoard()
        {
            // ARRANGE
            // Dodajemy pełną definicję efektu do JSONa testowego
            var json = @"[
                { 
                  ""Id"": 46, ""Name"": ""Tea Maid"", ""Type"": ""Unit"", ""Attack"": 2, ""Health"": 4,
                  ""Effects"": [
                    {
                      ""Trigger"": ""Passive"",
                      ""Zone"": ""Board"",
                      ""Actions"": [
                        { ""Type"": ""BuffStats"", ""Target"": ""FriendlySpellsInHand"", ""Amount"": -1 }
                      ]
                    }
                  ]
                },
                { ""Id"": 7, ""Name"": ""Glock-17"", ""Type"": ""Spell"", ""Cost"": 2 }
            ]";
            var engine = TestHelpers.CreateEngineWithCards(json);
            var f = engine.Factory;

            var maid = f.CreateCard(46, 1);
            var spell = f.CreateCard(7, 1);

            engine.CurrentState = engine.CurrentState.UpdatePlayer(engine.CurrentState.PlayerA.WithCardAddedToHand(spell));

            // Przed wystawieniem koszt czaru = 2
            Assert.Equal(2, engine.CurrentState.PlayerA.Hand.First().CurrentStats.BloodCost);

            // ACT
            engine.CurrentState = engine.CurrentState.UpdateBoard(engine.CurrentState.Board.WithUnitPlacedAt(0, 1, maid));
            engine.CurrentState = new AuraSystem().RecalculateAuras(engine.CurrentState);

            // ASSERT
            // Po wystawieniu Tea Maid koszt czaru = 1 (AuraSystem znalazł efekt Passive celujący w FriendlySpellsInHand)
            Assert.Equal(1, engine.CurrentState.PlayerA.Hand.First().CurrentStats.BloodCost);
        }

        [Fact]
        public void TeaMaid_ShouldStack_And_ResetOnDeath()
        {
            // ARRANGE
            var json = @"[
                { 
                  ""Id"": 46, ""Name"": ""Tea Maid"", ""Type"": ""Unit"", ""Attack"": 2, ""Health"": 2,
                  ""Effects"": [
                    {
                      ""Trigger"": ""Passive"",
                      ""Zone"": ""Board"",
                      ""Actions"": [
                        { ""Type"": ""BuffStats"", ""Target"": ""FriendlySpellsInHand"", ""Amount"": -1 }
                      ]
                    }
                  ]
                },
                { ""Id"": 7, ""Name"": ""BigSpell"", ""Type"": ""Spell"", ""Cost"": 3 }
            ]";
            var engine = TestHelpers.CreateEngineWithCards(json);
            var f = engine.Factory;
            var spell = f.CreateCard(7, 1);

            engine.CurrentState = engine.CurrentState.UpdatePlayer(engine.CurrentState.PlayerA.WithCardAddedToHand(spell));
            var auraSystem = new AuraSystem();
            var deathResolver = new CardGame.Core.GameRules.Death.DeathResolver();
            var context = new CardGame.Core.Application.GameContext(f, engine.Rng, engine.Events);

            // 1. POCZĄTEK: Koszt czaru = 3
            Assert.Equal(3, engine.CurrentState.PlayerA.Hand.First().CurrentStats.BloodCost);

            // 2. STACKOWANIE: Wystawiamy pierwszą Maid
            var maid1 = f.CreateCard(46, 1);
            engine.CurrentState = engine.CurrentState.UpdateBoard(engine.CurrentState.Board.WithUnitPlacedAt(0, 1, maid1));
            engine.CurrentState = auraSystem.RecalculateAuras(engine.CurrentState);
            Assert.Equal(2, engine.CurrentState.PlayerA.Hand.First().CurrentStats.BloodCost);

            // Wystawiamy drugą Maid
            var maid2 = f.CreateCard(46, 1);
            engine.CurrentState = engine.CurrentState.UpdateBoard(engine.CurrentState.Board.WithUnitPlacedAt(1, 1, maid2));
            engine.CurrentState = auraSystem.RecalculateAuras(engine.CurrentState);

            // Teraz koszt powinien wynosić 1 (3 - 1 - 1)
            Assert.Equal(1, engine.CurrentState.PlayerA.Hand.First().CurrentStats.BloodCost);

            // 3. POWRÓT: Zabijamy jedną Maid
            var damagedMaid = maid2.TakeDamage(10);
            engine.CurrentState = engine.CurrentState.UpdateBoard(engine.CurrentState.Board.UpdateUnit(damagedMaid));

            // Sprzątamy zwłoki i przeliczamy aury
            engine.CurrentState = deathResolver.ResolveDeaths(engine.CurrentState, engine.Events, context);
            engine.CurrentState = auraSystem.RecalculateAuras(engine.CurrentState);

            // ASSERT: Powinna zostać tylko jedna zniżka, koszt wraca do 2
            Assert.Equal(2, engine.CurrentState.PlayerA.Hand.First().CurrentStats.BloodCost);
        }

        [Fact]
        public void Rustler_ShouldReduceOwnCost_WhenFriendlySpellPlayed()
        {
            var json = @"[
                { ""Id"": 47, ""Name"": ""Rustler"", ""Type"": ""Unit"", ""Cost"": 10, ""Attack"": 5, ""Health"": 5,
                  ""Effects"": [ { ""Trigger"": ""OnFriendlyActionPlayed"", ""Zone"": ""Hand"", ""Actions"": [ { ""Type"": ""BuffStats"", ""Target"": ""Self"", ""Amount"": -1 } ] } ] },
                { ""Id"": 13, ""Name"": ""Cheap Spell"", ""Type"": ""Spell"", ""Cost"": 1 }
            ]";
            var engine = TestHelpers.CreateEngineWithCards(json);
            var f = engine.Factory;

            var rustler = f.CreateCard(47, 1);
            var spell = f.CreateCard(13, 1);

            engine.CurrentState = engine.CurrentState.UpdatePlayer(engine.CurrentState.PlayerA
                .WithCardAddedToHand(rustler)
                .WithCardAddedToHand(spell))
                .With(currentPhase: GamePhase.ActionOnly);

            Assert.Equal(10, engine.CurrentState.PlayerA.Hand.First(c => c.Definition.Id == "47").CurrentStats.BloodCost);

            // ACT
            engine.ExecuteCommand(new PlaySpellCommand(1, spell.InstanceId));

            // ASSERT
            var rustlerInHand = engine.CurrentState.PlayerA.Hand.First(c => c.Definition.Id == "47");
            Assert.Equal(9, rustlerInHand.CurrentStats.BloodCost);
        }
    }
}