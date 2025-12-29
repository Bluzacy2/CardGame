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
                { ""Id"": 1, ""Name"": ""Token"", ""Type"": ""Unit"", ""Cost"": 0, ""Attack"": 1, ""Health"": 1 }
            ]";

            var engine = TestHelpers.CreateEngineWithCards(json);
            var factory = engine.Factory;

            var card1 = factory.CreateCard(1, 1);
            var card2 = factory.CreateCard(1, 1);
            engine.CurrentState = engine.CurrentState.UpdatePlayer(engine.CurrentState.PlayerA.With(discardPile: new[] { card1, card2 }));

            var expectancy = factory.CreateCard(15, 1);
            engine.CurrentState = engine.CurrentState.UpdatePlayer(engine.CurrentState.PlayerA.WithCardAddedToHand(expectancy));
            engine.CurrentState = engine.CurrentState.With(currentPhase: GamePhase.ActionOnly);

            // Wybór (Choice) zawsze pauzuje silnik
            engine.ExecuteCommand(new PlaySpellCommand(1, expectancy.InstanceId));

            Assert.NotNull(engine.CurrentState.PendingInteraction);
            Assert.Equal(TargetType.Choice, engine.CurrentState.PendingInteraction.RequiredTargetType);

            // Wybieramy opcję Discard
            engine.ExecuteCommand(new SelectTargetCommand(1, 1));

            Assert.Equal(2, engine.CurrentState.PlayerA.Hand.Count(c => c.Definition.Name == "Token"));
            Assert.Null(engine.CurrentState.PendingInteraction);
        }

        [Fact]
        public void Advanced_Modal_Targeting_ShouldAutoResolve_AfterChoice_IfOneTarget()
        {
            string json = @"[
                { ""Id"": 99, ""Name"": ""Modal Kill"", ""Type"": ""Spell"", ""Cost"": 0,
                  ""Effects"": [ { 
                      ""Trigger"": ""OnPlayed"", ""Targeting"": ""Choice"", ""ChoiceLabels"": [""Kill"", ""Draw""],
                      ""Actions"": [ 
                        { ""Type"": ""DealDamage"", ""Target"": ""TargetEnemyUnit"", ""Amount"": 5 }, 
                        { ""Type"": ""DrawCard"", ""Amount"": 1 } 
                      ]
                  } ] 
                },
                { ""Id"": 2, ""Name"": ""Victim"", ""Type"": ""Unit"", ""Attack"": 1, ""Health"": 5 }
            ]";

            var engine = TestHelpers.CreateEngineWithCards(json);
            var victim = engine.Factory.CreateCard(2, 2);
            engine.CurrentState = engine.CurrentState.UpdateBoard(engine.CurrentState.Board.WithUnitPlacedAt(0, 2, victim));

            var spell = engine.Factory.CreateCard(99, 1);
            engine.CurrentState = engine.CurrentState.UpdatePlayer(engine.CurrentState.PlayerA.WithCardAddedToHand(spell));
            engine.CurrentState = engine.CurrentState.With(currentPhase: GamePhase.ActionOnly);

            // 1. Pauza na Choice
            engine.ExecuteCommand(new PlaySpellCommand(1, spell.InstanceId));

            // 2. Wybieramy "Kill". Na stole jest tylko 1 Victim -> Silnik powinien go zabić AUTO bez drugiej pauzy.
            engine.ExecuteCommand(new SelectTargetCommand(1, 0));

            Assert.Null(engine.CurrentState.Board.Lines[0].Player2Unit);
            Assert.Null(engine.CurrentState.PendingInteraction);
        }
    }
}