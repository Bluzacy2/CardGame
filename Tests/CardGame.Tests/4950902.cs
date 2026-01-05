using Xunit;
using CardGame.Core.State.Enums;
using CardGame.Core.Commands.Implementations;
using CardGame.Core.Cards.Data;
using System.Linq;

namespace CardGame.Tests
{
    public class CardValidationEdgeCaseTests
    {
        // Poprawiony JSON: Celowanie (Targeting/Target) musi być w akcji, 
        // aby PlayValidator.cs mógł je poprawnie zidentyfikować.
        private string GetValidationJson() => @"[
          { 
            ""Id"": 49, ""Name"": ""Rocket Ignorance"", ""Type"": ""Spell"", ""Cost"": 4, 
            ""Effects"": [ { 
                ""Trigger"": ""OnPlayed"", 
                ""Actions"": [ { ""Type"": ""DestroyUnit"", ""Target"": ""TargetEnemyUnit"" } ] 
            } ] 
          },
          { 
            ""Id"": 50, ""Name"": ""Diabetic Feast"", ""Type"": ""Spell"", ""Cost"": 1, 
            ""Effects"": [ { 
                ""Trigger"": ""OnPlayed"", 
                ""Actions"": [ { ""Type"": ""BuffStats"", ""Target"": ""TargetFriendlyUnit"", ""BuffAtk"": 1, ""BuffHp"": 3 } ] 
            } ] 
          },
          { 
            ""Id"": 902, ""Name"": ""The Appetizing Goblet"", ""Type"": ""Spell"", ""Cost"": 0, 
            ""Effects"": [ { 
                ""Trigger"": ""OnPlayed"", 
                ""Actions"": [ { ""Type"": ""SacrificeUnit"", ""Target"": ""TargetFriendlyUnit"" } ] 
            } ] 
          },
          { ""Id"": 100, ""Name"": ""TargetDummy"", ""Type"": ""Unit"", ""Attack"": 1, ""Health"": 5 }
        ]";

        [Fact]
        public void RocketIgnorance_ShouldNotBePlayable_WhenEnemyBoardIsEmpty()
        {
            // ARRANGE
            var engine = TestHelpers.CreateEngineWithCards(GetValidationJson());
            var spell = engine.Factory.CreateCard(49, 1);

            engine.CurrentState = engine.CurrentState.UpdatePlayer(engine.CurrentState.PlayerA.WithCardAddedToHand(spell));
            engine.CurrentState = engine.CurrentState.With(currentPhase: GamePhase.ActionOnly);

            int initialBlood = engine.CurrentState.PlayerA.CurrentBlood;

            // ACT
            // Próba zagrania - PlayValidator powinien zwrócić false, bo nie ma żadnego "TargetEnemyUnit"
            engine.ExecuteCommand(new PlaySpellCommand(1, spell.InstanceId));

            // ASSERT
            // Karta MUSI zostać w ręce. Jeśli ręka jest [], oznacza to, że walidacja przepuściła ruch!
            Assert.Contains(engine.CurrentState.PlayerA.Hand, c => c.InstanceId == spell.InstanceId);
            Assert.Equal(initialBlood, engine.CurrentState.PlayerA.CurrentBlood);
        }

        [Fact]
        public void DiabeticFeast_ShouldNotBePlayable_WhenPlayerHasNoUnits()
        {
            // ARRANGE
            var engine = TestHelpers.CreateEngineWithCards(GetValidationJson());
            var spell = engine.Factory.CreateCard(50, 1);

            engine.CurrentState = engine.CurrentState.UpdatePlayer(engine.CurrentState.PlayerA.WithCardAddedToHand(spell));
            engine.CurrentState = engine.CurrentState.With(currentPhase: GamePhase.ActionOnly);

            // ACT
            engine.ExecuteCommand(new PlaySpellCommand(1, spell.InstanceId));

            // ASSERT
            Assert.Contains(engine.CurrentState.PlayerA.Hand, c => c.InstanceId == spell.InstanceId);
            Assert.Empty(engine.CurrentState.SpellStack);
        }

        [Fact]
        public void AppetizingGoblet_ShouldNotBePlayable_WhenPlayerHasNoUnits()
        {
            // ARRANGE
            var engine = TestHelpers.CreateEngineWithCards(GetValidationJson());
            var goblet = engine.Factory.CreateCard(902, 1);

            engine.CurrentState = engine.CurrentState.UpdatePlayer(engine.CurrentState.PlayerA.WithCardAddedToHand(goblet));
            engine.CurrentState = engine.CurrentState.With(currentPhase: GamePhase.ActionOnly);

            // ACT
            engine.ExecuteCommand(new PlaySpellCommand(1, goblet.InstanceId));

            // ASSERT
            Assert.Contains(engine.CurrentState.PlayerA.Hand, c => c.InstanceId == goblet.InstanceId);
        }

        [Fact]
        public void Spells_ShouldRequireCorrectTargetSide()
        {
            // ARRANGE
            var engine = TestHelpers.CreateEngineWithCards(GetValidationJson());
            var spell = engine.Factory.CreateCard(49, 1); // Rocket Ignorance (szuka wroga)
            var ally = engine.Factory.CreateCard(100, 1); // Sojusznik

            // Na stole jest tylko sojusznik. Rocket Ignorance nie powinien go "widzieć" jako legalny cel.
            engine.CurrentState = engine.CurrentState.UpdateBoard(engine.CurrentState.Board.WithUnitPlacedAt(0, 1, ally));
            engine.CurrentState = engine.CurrentState.UpdatePlayer(engine.CurrentState.PlayerA.WithCardAddedToHand(spell));
            engine.CurrentState = engine.CurrentState.With(currentPhase: GamePhase.ActionOnly);

            // ACT
            engine.ExecuteCommand(new PlaySpellCommand(1, spell.InstanceId));

            // ASSERT
            // Czar nie został rzucony, bo jedyny cel na stole nie jest wrogiem
            Assert.Contains(engine.CurrentState.PlayerA.Hand, c => c.InstanceId == spell.InstanceId);
        }
    }
}