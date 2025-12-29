using System.Collections.Generic;
using System.Linq;
using CardGame.Core.Application;
using CardGame.Core.Cards.Data;
using CardGame.Core.Commands.Implementations;
using CardGame.Core.State.Enums;
using Xunit;

namespace CardGame.Tests
{
    public class NewMechanicsPt3Tests
    {
        private string GetJson() => @"[
            { ""Id"": 31, ""Name"": ""Silence"", ""Type"": ""Spell"", ""Cost"": 2,
              ""Effects"": [ { ""Trigger"": ""OnPlayed"", ""Actions"": [ { ""Type"": ""Silence"", ""Target"": ""TargetEnemyUnit"" } ] } ] },
            { ""Id"": 33, ""Name"": ""Mouse"", ""Type"": ""Unit"", ""Attack"": 5, ""Health"": 10, ""Keywords"": [""Stunned""] },
            { ""Id"": 34, ""Name"": ""MaidMask"", ""Type"": ""Unit"", ""Attack"": 3, ""Health"": 1, ""Keywords"": [""SoulGuard""] },
            { ""Id"": 36, ""Name"": ""Death"", ""Type"": ""Unit"", ""Attack"": 3, ""Health"": 3,
              ""Effects"": [ { ""Trigger"": ""OnFriendlyUnitDied"", ""Actions"": [ { ""Type"": ""DealDamage"", ""Target"": ""EnemyHero"", ""Amount"": 3 } ] } ] },
            { ""Id"": 99, ""Name"": ""Victim"", ""Type"": ""Unit"", ""Attack"": 1, ""Health"": 1 }
        ]";

        [Fact]
        public void Silence_ShouldRemoveSoulGuard_AndBonusStats()
        {
            var engine = TestHelpers.CreateEngineWithCards(GetJson());
            var victim = engine.Factory.CreateCard(34, 2);
            var buffedVictim = victim.AddPermanentBuff(new CardGame.Core.Cards.Models.CardStats(2, 2, 0));
            engine.CurrentState = engine.CurrentState.UpdateBoard(engine.CurrentState.Board.WithUnitPlacedAt(0, 2, buffedVictim));

            var silence = engine.Factory.CreateCard(31, 1);
            engine.CurrentState = engine.CurrentState.UpdatePlayer(engine.CurrentState.PlayerA.WithCardAddedToHand(silence));

            // --- KLUCZOWA POPRAWKA: Zmiana fazy na taką, która pozwala na czary ---
            engine.CurrentState = engine.CurrentState.With(currentPhase: GamePhase.ActionOnly);

            engine.ExecuteCommand(new PlaySpellCommand(1, silence.InstanceId, selectedTargetId: victim.InstanceId));

            var silencedUnit = engine.CurrentState.Board.Lines[0].Player2Unit!;

            // Jeśli faza jest poprawna, te asercje sprawdzą realne działanie uciszenia
            Assert.True(silencedUnit.IsSilenced);
            Assert.Equal(3, silencedUnit.CurrentStats.Attack);
            Assert.DoesNotContain(Keyword.SoulGuard, silencedUnit.CurrentStats.Keywords);
        }

        [Fact]
        public void Death_ShouldPingHero_WhenFriendlyUnitDies()
        {
            var engine = TestHelpers.CreateEngineWithCards(GetJson());
            var death = engine.Factory.CreateCard(36, 1);
            var ally = engine.Factory.CreateCard(99, 1);
            var enemy = engine.Factory.CreateCard(99, 2);
            var blocker = engine.Factory.CreateCard(99, 2); // NOWE: Blokuje atak Death

            engine.CurrentState = engine.CurrentState.UpdateBoard(engine.CurrentState.Board
                .WithUnitPlacedAt(0, 1, death)
                .WithUnitPlacedAt(0, 2, blocker) // Death bije w blokera, nie w Hero
                .WithUnitPlacedAt(1, 1, ally)
                .WithUnitPlacedAt(1, 2, enemy));

            engine.CurrentState = engine.CurrentState.With(currentPhase: GamePhase.Combat);
            engine.ExecuteCommand(new EndPhaseCommand(1));

            // Ally (P1) ginie -> Death (P1) zadaje 3 dmg w P2. Expected: 30 - 3 = 27.
            Assert.Equal(27, engine.CurrentState.PlayerB.Health);
        }

        [Fact]
        public void StartingStun_ShouldWorkOnMouse()
        {
            var engine = TestHelpers.CreateEngineWithCards(GetJson());
            var mouse = engine.Factory.CreateCard(33, 1);
            var victim = engine.Factory.CreateCard(99, 2);
            engine.CurrentState = engine.CurrentState.UpdateBoard(engine.CurrentState.Board.WithUnitPlacedAt(0, 1, mouse).WithUnitPlacedAt(0, 2, victim));
            engine.CurrentState = engine.CurrentState.With(currentPhase: GamePhase.Combat);

            // ACT: Rozliczamy walkę
            engine.ExecuteCommand(new EndPhaseCommand(1));

            Assert.NotNull(engine.CurrentState.Board.Lines[0].Player2Unit);
            var mouseAfter = engine.CurrentState.Board.Lines[0].Player1Unit!;
            Assert.DoesNotContain(Keyword.Stunned, mouseAfter.CurrentStats.Keywords);
        }
    }
}