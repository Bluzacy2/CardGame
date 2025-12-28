using System.Collections.Generic;
using System.Linq;
using CardGame.Core.Application;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Models;
using CardGame.Core.Commands.Implementations;
using CardGame.Core.State.Enums;
using CardGame.Core.GameRules.Damage;
using CardGame.Core.GameRules.Battle;
using Xunit;

namespace CardGame.Tests
{
    public class ComplexMechanicsTests
    {
        [Fact]
        public void FullSystemTest_Combat_Splash_Burn_And_Tick()
        {
            // --- ARRANGE ---
            string json = @"[
                { 
                  ""Id"": 1, ""Name"": ""Megumin"", ""Type"": ""Unit"", ""Cost"": 4, ""Attack"": 3, ""Health"": 2, 
                  ""Keywords"": [""SplashDamage"", ""BurnSource""], 
                  ""KeywordParams"": {""SplashDamage"": 2} 
                },
                { ""Id"": 2, ""Name"": ""Recruit"", ""Type"": ""Unit"", ""Cost"": 1, ""Attack"": 2, ""Health"": 2 },
                { ""Id"": 3, ""Name"": ""BigVictim"", ""Type"": ""Unit"", ""Cost"": 1, ""Attack"": 0, ""Health"": 10 }
            ]";

            var engine = TestHelpers.CreateEngineWithCards(json);
            var factory = engine.Factory;

            var p1Recruit = factory.CreateCard(2, 1);
            var p2Recruit = factory.CreateCard(2, 2);
            var megumin = factory.CreateCard(1, 1);
            var mainVictim = factory.CreateCard(3, 2);
            var sideVictim = factory.CreateCard(3, 2);

            engine.CurrentState = engine.CurrentState.UpdateBoard(engine.CurrentState.Board
                .WithUnitPlacedAt(0, 1, p1Recruit)
                .WithUnitPlacedAt(0, 2, p2Recruit)
                .WithUnitPlacedAt(1, 1, megumin)
                .WithUnitPlacedAt(1, 2, mainVictim)
                .WithUnitPlacedAt(2, 2, sideVictim));

            engine.CurrentState = engine.CurrentState.With(currentPhase: GamePhase.Combat);

            // --- ACT ---
            engine.ExecuteCommand(new EndPhaseCommand(1));

            // --- ASSERT ---
            var board = engine.CurrentState.Board;

            Assert.Null(board.Lines[0].Player1Unit);
            Assert.Null(board.Lines[0].Player2Unit);

            var sideUnit = board.Lines[2].Player2Unit;
            Assert.NotNull(sideUnit);
            Assert.Equal(8, sideUnit.CurrentStats.Health);

            var updatedMainVictim = board.Lines[1].Player2Unit;
            Assert.NotNull(updatedMainVictim);
            Assert.Contains(Keyword.Burning, updatedMainVictim.CurrentStats.Keywords);

            Assert.Equal(6, updatedMainVictim.CurrentStats.Health);
        }

        [Fact]
        public void BonusStrike_ShouldBeOneSided_AndNotReceiveCounterDamage()
        {
            // --- ARRANGE ---
            string json = @"[
                { ""Id"": 1, ""Name"": ""Striker"", ""Type"": ""Unit"", ""Attack"": 3, ""Health"": 1 },
                { ""Id"": 2, ""Name"": ""Defender"", ""Type"": ""Unit"", ""Attack"": 10, ""Health"": 10 }
            ]";
            var engine = TestHelpers.CreateEngineWithCards(json);

            var attacker = engine.Factory.CreateCard(1, 1);
            var defender = engine.Factory.CreateCard(2, 2);

            engine.CurrentState = engine.CurrentState.UpdateBoard(engine.CurrentState.Board
                .WithUnitPlacedAt(0, 1, attacker)
                .WithUnitPlacedAt(0, 2, defender));

            // POPRAWKA TUTAJ: Używamy 3-argumentowego konstruktora. 
            // GameContext wewnętrznie sam stworzy DamageCalculatora.
            var context = new GameContext(engine.Factory, engine.Rng, engine.Events);

            // --- ACT ---
            var nextState = context.Battle.ResolveBonusStrike(
                engine.CurrentState,
                attacker,
                defender,
                0,
                engine.Events,
                context);

            // --- ASSERT ---
            var unitP1 = nextState.Board.Lines[0].Player1Unit!;
            var unitP2 = nextState.Board.Lines[0].Player2Unit!;

            Assert.Equal(1, unitP1.CurrentStats.Health);
            Assert.Equal(7, unitP2.CurrentStats.Health);
        }
    }
}