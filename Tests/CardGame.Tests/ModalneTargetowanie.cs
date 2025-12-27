using System.Collections.Generic;
using System.Linq;
using CardGame.Core.Application;
using CardGame.Core.Cards.Data;
using CardGame.Core.Commands.Implementations;
using CardGame.Core.State.Enums;
using Xunit;

namespace CardGame.Tests
{
    public class TargetingAndChoiceTests
    {
        [Fact]
        public void Expectancy_Logic_Choice_Test()
        {
            string json = @"[
                { ""Id"": 15, ""Name"": ""Expectancy"", ""Type"": ""Spell"", ""Cost"": 2,
                  ""Effects"": [ { 
                      ""Trigger"": ""OnPlayed"", ""Targeting"": ""Choice"", ""ChoiceLabels"": [""Deck"", ""Discard""],
                      ""Actions"": [ { ""Type"": ""DrawCard"", ""Amount"": 2 }, { ""Type"": ""DrawFromDiscard"", ""Amount"": 2 } ]
                  } ] 
                },
                { ""Id"": 1, ""Name"": ""Token"", ""Type"": ""Unit"", ""Cost"": 0 }
            ]";

            var engine = TestHelpers.CreateEngineWithCards(json);
            var card1 = engine.Factory.CreateCard(1, 1);
            var card2 = engine.Factory.CreateCard(1, 1);

            // Setup: 2 karty na cmentarzu
            engine.CurrentState = engine.CurrentState.UpdatePlayer(engine.CurrentState.PlayerA.With(discardPile: new[] { card1, card2 }));
            var expectancy = engine.Factory.CreateCard(15, 1);
            engine.CurrentState = engine.CurrentState.UpdatePlayer(engine.CurrentState.PlayerA.WithCardAddedToHand(expectancy));
            engine.CurrentState = engine.CurrentState.With(currentPhase: GamePhase.ActionOnly);

            // Zagrywamy czar
            engine.ExecuteCommand(new PlaySpellCommand(1, expectancy.InstanceId));

            // ZMIANA: Sprawdzamy, czy stos nie jest pusty (czar jest w Limbo/SpellStack)
            Assert.NotEmpty(engine.CurrentState.SpellStack);
            // Sprawdzamy, czy konkretnie nasz czar tam jest
            Assert.Contains(engine.CurrentState.SpellStack, s => s.InstanceId == expectancy.InstanceId);

            // Upewniamy się, że nie ma go jeszcze na cmentarzu
            Assert.DoesNotContain(engine.CurrentState.PlayerA.DiscardPile, c => c.InstanceId == expectancy.InstanceId);

            // Wybieramy opcję 1 (Draw from Discard)
            engine.ExecuteCommand(new SelectTargetCommand(1, 1));

            // ZMIANA: Po zakończeniu akcji stos powinien być pusty
            Assert.Empty(engine.CurrentState.SpellStack);

            // Czar trafia na cmentarz
            Assert.Contains(engine.CurrentState.PlayerA.DiscardPile, c => c.InstanceId == expectancy.InstanceId);
            // Karty zostały dobrane z discardu
            Assert.Equal(2, engine.CurrentState.PlayerA.Hand.Count(c => c.Definition.Name == "Token"));
        }

        [Fact]
        public void Advanced_Modal_Targeting_Test()
        {
            string json = @"[
                { ""Id"": 99, ""Name"": ""Modal Kill"", ""Type"": ""Spell"", ""Cost"": 0,
                  ""Effects"": [ { 
                      ""Trigger"": ""OnPlayed"", ""Targeting"": ""Choice"", ""ChoiceLabels"": [""Kill"", ""Draw""],
                      ""Actions"": [ { ""Type"": ""DealDamage"", ""Target"": ""TargetEnemyUnit"", ""Amount"": 5 }, { ""Type"": ""DrawCard"", ""Amount"": 1 } ]
                  } ] 
                },
                { ""Id"": 2, ""Name"": ""Victim"", ""Type"": ""Unit"", ""Health"": 5 }
            ]";

            var engine = TestHelpers.CreateEngineWithCards(json);
            var victim = engine.Factory.CreateCard(2, 2);
            engine.CurrentState = engine.CurrentState.UpdateBoard(engine.CurrentState.Board.WithUnitPlacedAt(0, 2, victim));
            var spell = engine.Factory.CreateCard(99, 1);
            engine.CurrentState = engine.CurrentState.UpdatePlayer(engine.CurrentState.PlayerA.WithCardAddedToHand(spell));
            engine.CurrentState = engine.CurrentState.With(currentPhase: GamePhase.ActionOnly);

            engine.ExecuteCommand(new PlaySpellCommand(1, spell.InstanceId));
            engine.ExecuteCommand(new SelectTargetCommand(1, 0)); // Wybór "Kill"
            engine.ExecuteCommand(new SelectTargetCommand(1, victim.InstanceId));

            // Jednostka zginęła
            Assert.Null(engine.CurrentState.Board.Lines[0].Player2Unit);
            // Czar na cmentarzu (zdjęty ze stosu)
            Assert.Contains(engine.CurrentState.PlayerA.DiscardPile, c => c.InstanceId == spell.InstanceId);
            // Stos pusty
            Assert.Empty(engine.CurrentState.SpellStack);
        }
    }
}