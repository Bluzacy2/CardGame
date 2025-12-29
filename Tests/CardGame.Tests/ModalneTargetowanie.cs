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
            // ARRANGE
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

            // Przygotowanie cmentarza (2 tokeny)
            var card1 = factory.CreateCard(1, 1);
            var card2 = factory.CreateCard(1, 1);
            engine.CurrentState = engine.CurrentState.UpdatePlayer(engine.CurrentState.PlayerA.With(discardPile: new[] { card1, card2 }));

            // Przygotowanie ręki (czar Expectancy)
            var expectancy = factory.CreateCard(15, 1);
            engine.CurrentState = engine.CurrentState.UpdatePlayer(engine.CurrentState.PlayerA.WithCardAddedToHand(expectancy));

            // Rygorystyczne ustawienie fazy akcji
            engine.CurrentState = engine.CurrentState.With(currentPhase: GamePhase.ActionOnly);

            // ACT
            // 1. Zagranie czaru
            engine.ExecuteCommand(new PlaySpellCommand(1, expectancy.InstanceId));

            // ASSERT: Silnik powinien prosić o wybór (Choice)
            Assert.NotNull(engine.CurrentState.PendingInteraction);
            Assert.Equal(TargetType.Choice, engine.CurrentState.PendingInteraction.RequiredTargetType);

            // ACT 2: Wybieramy opcję nr 1 ("Discard")
            engine.ExecuteCommand(new SelectTargetCommand(1, 1));

            // ASSERT FINAL
            // Czar powinien być na cmentarzu
            Assert.Contains(engine.CurrentState.PlayerA.DiscardPile, c => c.InstanceId == expectancy.InstanceId);
            // Gracz powinien mieć 2 tokeny w ręce (pobrane z discardu)
            Assert.Equal(2, engine.CurrentState.PlayerA.Hand.Count(c => c.Definition.Name == "Token"));
            // Stos czarów powinien być pusty
            Assert.Empty(engine.CurrentState.SpellStack);
        }

        [Fact]
        public void Advanced_Modal_Targeting_Test()
        {
            // ARRANGE
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
            var factory = engine.Factory;

            // Ustawienie wroga na planszy
            var victim = factory.CreateCard(2, 2);
            engine.CurrentState = engine.CurrentState.UpdateBoard(engine.CurrentState.Board.WithUnitPlacedAt(0, 2, victim));

            // Przygotowanie czaru w ręce
            var spell = factory.CreateCard(99, 1);
            engine.CurrentState = engine.CurrentState.UpdatePlayer(engine.CurrentState.PlayerA.WithCardAddedToHand(spell));
            engine.CurrentState = engine.CurrentState.With(currentPhase: GamePhase.ActionOnly);

            // ACT
            // 1. Zagranie czaru
            engine.ExecuteCommand(new PlaySpellCommand(1, spell.InstanceId));

            // 2. Wybieramy opcję nr 0 ("Kill")
            engine.ExecuteCommand(new SelectTargetCommand(1, 0));

            // ASSERT: Silnik powinien teraz prosić o cel jednostki (TargetEnemyUnit) dla wybranej akcji
            Assert.NotNull(engine.CurrentState.PendingInteraction);
            Assert.Equal(TargetType.TargetEnemyUnit, engine.CurrentState.PendingInteraction.RequiredTargetType);

            // ACT 3: Wybieramy ofiarę (Victim)
            engine.ExecuteCommand(new SelectTargetCommand(1, victim.InstanceId));

            // ASSERT FINAL
            // Jednostka wroga powinna zginąć (zniknąć z planszy)
            Assert.Null(engine.CurrentState.Board.Lines[0].Player2Unit);
            // Brak oczekujących interakcji
            Assert.Null(engine.CurrentState.PendingInteraction);
            // Stos czarów wyczyszczony
            Assert.Empty(engine.CurrentState.SpellStack);
        }
    }
}