using System.Collections.Generic;
using System.Linq;
using CardGame.Core.Application;
using CardGame.Core.Cards.Data;
using CardGame.Core.Commands.Implementations;
using CardGame.Core.State.Enums;
using CardGame.Core.Cards.Models;
using Xunit;

namespace CardGame.Tests
{
    public class CriticalThinkingTests
    {
        private string GetTutorJson() => @"[
            { ""Id"": 13, ""Name"": ""Critical Thinking"", ""Type"": ""Spell"", ""Cost"": 2,
              ""Effects"": [ 
                  { ""Trigger"": ""OnPlayed"", ""Actions"": [ { ""Type"": ""TutorCard"", ""Target"": ""Self"" } ] } 
              ] 
            },
            { ""Id"": 1, ""Name"": ""Filler"", ""Type"": ""Unit"", ""Cost"": 1, ""Attack"": 1, ""Health"": 1 }
        ]";

        [Fact]
        public void CriticalThinking_ShouldCreatePendingInteraction_WithDeckOptions()
        {
            // 1. SETUP
            var engine = TestHelpers.CreateEngineWithCards(GetTutorJson());
            var factory = engine.Factory;

            // Tworzymy kartę Critical Thinking
            var spell = factory.CreateCard(13, 1);

            // Wypełniamy talię gracza kartami, żeby Tutor miał z czego wybierać
            var deck = new List<CardInstance>();
            for (int i = 0; i < 10; i++) deck.Add(factory.CreateCard(1, 1));

            // Ustawiamy stan: Karta w ręce, talia pełna, mana 10
            engine.CurrentState = engine.CurrentState.UpdatePlayer(engine.CurrentState.PlayerA
                .WithCardAddedToHand(spell)
                .With(drawPile: deck)
                .WithResourceChanged(ResourceType.Blood, 10, 10));

            // Ustawiamy odpowiednią fazę
            engine.CurrentState = engine.CurrentState.With(currentPhase: GamePhase.ActionOnly);

            // 2. ACT - Zagrywamy kartę
            var result = engine.ExecuteCommand(new PlaySpellCommand(1, spell.InstanceId));

            // 3. ASSERT - Weryfikacja

            // A. Czy karta zeszła z ręki? (Czy walidacja przeszła)
            Assert.DoesNotContain(engine.CurrentState.PlayerA.Hand, c => c.InstanceId == spell.InstanceId);

            // B. Czy mamy PendingInteraction? (To jest kluczowe!)
            Assert.NotNull(engine.CurrentState.PendingInteraction);

            // C. Czy typ to Choice?
            Assert.Equal(TargetType.Choice, engine.CurrentState.PendingInteraction.RequiredTargetType);

            // D. Czy liczba opcji zgadza się z liczbą kart w talii?
            Assert.Equal(10, engine.CurrentState.PendingInteraction.Options.Count);
        }
    }
}