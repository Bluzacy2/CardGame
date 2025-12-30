using System.Collections.Generic;
using System.Linq;
using CardGame.Core.Application;
using CardGame.Core.Cards.Data;
using CardGame.Core.Commands.Implementations;
using CardGame.Core.State.Enums;
using Xunit;

namespace CardGame.Tests
{
    public class SacrificeInteractionTests
    {
        private string GetSacrificeTestJson() => @"[
          { 
            ""Id"": 1, ""Name"": ""Shield Guard"", ""Type"": ""Unit"", ""Cost"": 1, ""Attack"": 1, ""Health"": 5, 
            ""Keywords"": [""SoulGuard""] 
          },
          { 
            ""Id"": 2, ""Name"": ""Eternal Phoenix"", ""Type"": ""Unit"", ""Cost"": 1, ""Attack"": 1, ""Health"": 1, 
            ""Keywords"": [""Unkillable""] 
          },
          {
            ""Id"": 100, ""Name"": ""Dark Pact"", ""Type"": ""Spell"", ""Cost"": 0,
            ""Effects"": [ {
                ""Trigger"": ""OnPlayed"", 
                ""Actions"": [ { ""Type"": ""SacrificeUnit"", ""Target"": ""TargetFriendlyUnit"" } ]
            } ]
          }
        ]";

        [Fact]
        public void Sacrifice_ShouldBypassSoulGuard_AndKillUnit()
        {
            // ARRANGE
            var engine = TestHelpers.CreateEngineWithCards(GetSacrificeTestJson());
            var sgUnit = engine.Factory.CreateCard(1, 1); // Jednostka z SoulGuard
            var spell = engine.Factory.CreateCard(100, 1); // Czar poświęcenia

            engine.CurrentState = engine.CurrentState.UpdateBoard(engine.CurrentState.Board.WithUnitPlacedAt(0, 1, sgUnit));
            engine.CurrentState = engine.CurrentState.UpdatePlayer(engine.CurrentState.PlayerA.WithCardAddedToHand(spell));
            engine.CurrentState = engine.CurrentState.With(currentPhase: GamePhase.ActionOnly, activePlayerId: 1);

            // ACT
            // Poświęcamy jednostkę z SoulGuardem
            engine.ExecuteCommand(new PlaySpellCommand(1, spell.InstanceId, selectedTargetId: sgUnit.InstanceId));

            // ASSERT
            // Jednostka powinna zniknąć z planszy (SoulGuard nie powinien jej uratować przed poświęceniem)
            Assert.Null(engine.CurrentState.Board.Lines[0].Player1Unit);

            // Powinna trafić na cmentarz (DiscardPile)
            Assert.Contains(engine.CurrentState.PlayerA.DiscardPile, c => c.InstanceId == sgUnit.InstanceId);
        }

        [Fact]
        public void Sacrifice_ShouldTriggerUnkillable_AndReturnUnitToHand()
        {
            // ARRANGE
            var engine = TestHelpers.CreateEngineWithCards(GetSacrificeTestJson());
            var ukUnit = engine.Factory.CreateCard(2, 1); // Jednostka z Unkillable
            var spell = engine.Factory.CreateCard(100, 1);

            engine.CurrentState = engine.CurrentState.UpdateBoard(engine.CurrentState.Board.WithUnitPlacedAt(0, 1, ukUnit));
            engine.CurrentState = engine.CurrentState.UpdatePlayer(engine.CurrentState.PlayerA.WithCardAddedToHand(spell));
            engine.CurrentState = engine.CurrentState.With(currentPhase: GamePhase.ActionOnly, activePlayerId: 1);

            // ACT
            // Poświęcamy jednostkę z Unkillable
            engine.ExecuteCommand(new PlaySpellCommand(1, spell.InstanceId, selectedTargetId: ukUnit.InstanceId));

            // ASSERT
            // Jednostka nie powinna być na planszy
            Assert.Null(engine.CurrentState.Board.Lines[0].Player1Unit);

            // Powinna wrócić do ręki dzięki Unkillable (mimo że to było poświęcenie)
            Assert.Contains(engine.CurrentState.PlayerA.Hand, c => c.InstanceId == ukUnit.InstanceId);

            // Nie powinna być na cmentarzu
            Assert.DoesNotContain(engine.CurrentState.PlayerA.DiscardPile, c => c.InstanceId == ukUnit.InstanceId);
        }

        [Fact]
        public void NormalDamage_ShouldStillTriggerSoulGuard()
        {
            // Test sprawdzający, czy nie zepsuliśmy normalnego działania SoulGuarda
            // ARRANGE
            var engine = TestHelpers.CreateEngineWithCards(GetSacrificeTestJson());
            var sgUnit = engine.Factory.CreateCard(1, 1);
            engine.CurrentState = engine.CurrentState.UpdateBoard(engine.CurrentState.Board.WithUnitPlacedAt(0, 1, sgUnit));

            // ACT
            // Zadajemy 10 obrażeń (jednostka ma 5 HP) - to nie jest Sacrifice
            var damagedUnit = sgUnit.TakeDamage(10);
            engine.CurrentState = engine.CurrentState.UpdateBoard(engine.CurrentState.Board.UpdateUnit(damagedUnit));

            // Wywołujemy resolver (np. przez EndPhase)
            engine.ExecuteCommand(new EndPhaseCommand(1));

            // ASSERT
            // Jednostka powinna przeżyć na 1 HP dzięki SoulGuard
            var unitOnBoard = engine.CurrentState.Board.Lines[0].Player1Unit;
            Assert.NotNull(unitOnBoard);
            Assert.Equal(1, unitOnBoard.CurrentStats.Health);
            Assert.Contains(Keyword.SoulGuardDepleted, unitOnBoard.CurrentStats.Keywords);
        }
    }
}