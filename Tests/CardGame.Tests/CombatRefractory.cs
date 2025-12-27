using System.Collections.Generic;
using System.Linq;
using CardGame.Core.Application;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Models; // DODAJ TO
using CardGame.Core.Commands.Implementations;
using CardGame.Core.State.Enums;
using CardGame.Core.GameRules.Damage; // DODAJ TO
using CardGame.Core.GameRules.Battle; // DODAJ TO
using Xunit;

namespace CardGame.Tests
{
    public class ComplexMechanicsTests
    {
        [Fact]
        public void FullSystemTest_Combat_Splash_Burn_And_Tick()
        {
            // --- ARRANGE ---
            // Definiujemy karty w formacie JSON, aby sprawdzić system Data-Driven
            string json = @"[
                { 
                  ""Id"": 1, ""Name"": ""Megumin"", ""Type"": ""Unit"", ""Cost"": 4, ""Attack"": 3, ""Health"": 2, 
                  ""Keywords"": [""SplashDamage"", ""BurnSource""], 
                  ""KeywordParams"": {""SplashDamage"": 2} 
                },
                { ""Id"": 2, ""Name"": ""Recruit"", ""Type"": ""Unit"", ""Cost"": 1, ""Attack"": 2, ""Health"": 2 },
                { ""Id"": 3, ""Name"": ""BigVictim"", ""Type"": ""Unit"", ""Cost"": 1, ""Attack"": 0, ""Health"": 10 }
            ]";

            // Korzystamy z Twojego TestHelpera do stworzenia silnika
            var engine = TestHelpers.CreateEngineWithCards(json);
            var factory = engine.Factory;

            /* 
               USTAWIENIE PLANSZY:
               Linia 0: P1 Recruit (2/2)  vs  P2 Recruit (2/2)  -> Powinni się wytrade'ować (obaj zginąć)
               Linia 1: P1 Megumin (3/2)  vs  P2 BigVictim (10 HP) -> Megumin bije za 3, nakłada Burn.
               Linia 2: P2 BigVictim (10 HP) -> Powinien oberwać Splashem od Megumin z Linii 1.
            */

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

            // Ustawiamy fazę na Combat, żeby EndPhaseCommand odpalił ResolveCombatPhase
            engine.CurrentState = engine.CurrentState.With(currentPhase: GamePhase.Combat);

            // --- ACT ---
            // 1. Wykonujemy fazę walki
            engine.ExecuteCommand(new EndPhaseCommand(1));

            // --- ASSERT ---
            var board = engine.CurrentState.Board;

            // TEST 1: Jednoczesność (Duel)
            // Obie jednostki na L0 miały po 2 HP i atak 2. Powinny zniknąć (zginąć jednocześnie).
            Assert.Null(board.Lines[0].Player1Unit);
            Assert.Null(board.Lines[0].Player2Unit);

            // TEST 2: Splash Damage (Flexible Param)
            // Megumin z L1 atakowała. Jej SplashDamage wynosi 2. 
            // SideVictim na L2 powinien mieć 10 - 2 = 8 HP.
            var sideUnit = board.Lines[2].Player2Unit;
            Assert.NotNull(sideUnit);
            Assert.Equal(8, sideUnit.CurrentStats.Health);

            // TEST 3: BurnSource (Nakładanie statusu)
            // MainVictim na L1 powinien zostać trafiony przez Megumin i mieć status Burning.
            var updatedMainVictim = board.Lines[1].Player2Unit;
            Assert.NotNull(updatedMainVictim);
            Assert.Contains(Keyword.Burning, updatedMainVictim.CurrentStats.Keywords);

            // TEST 4: Burning Tick (Obrażenia na koniec rundy)
            // MainVictim dostał: 3 (atak bezpośredni) + 1 (Burning tick na koniec fazy).
            // HP: 10 - 3 - 1 = 6.
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

            // Umieszczamy na planszy
            engine.CurrentState = engine.CurrentState.UpdateBoard(engine.CurrentState.Board
                .WithUnitPlacedAt(0, 1, attacker)
                .WithUnitPlacedAt(0, 2, defender));

            // Pobieramy context (z dostępem do BattleService)
            var context = new GameContext(engine.Factory, engine.Rng, engine.Events, new DamageCalculator());

            // --- ACT ---
            // Symulujemy Bonus Attack (Strike) za pomocą BattleService
            // Używamy metody ResolveBonusStrike (jednostronna)
            var nextState = context.Battle.ResolveBonusStrike(
                engine.CurrentState,
                attacker,
                defender,
                0,
                engine.Events,
                context);

            // --- ASSERT ---
            var unitP1 = nextState.Board.Lines[0].Player1Unit;
            var unitP2 = nextState.Board.Lines[0].Player2Unit;

            // Napastnik (3/1) NIE powinien dostać 10 obrażeń zwrotnych, bo to Strike, a nie Duel.
            Assert.Equal(1, unitP1.CurrentStats.Health);

            // Obrońca powinien dostać 3 obrażenia.
            Assert.Equal(7, unitP2.CurrentStats.Health);
        }
    }
}