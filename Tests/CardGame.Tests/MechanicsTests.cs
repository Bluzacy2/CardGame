using System.Collections.Generic;
using System.Linq;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Models;
using CardGame.Core.Commands.Implementations;
using Xunit;

namespace CardGame.Tests
{
    public class MechanicsTests
    {
        [Fact]
        public void BuffSystem_ShouldBeAdditive_And_HealingShouldNotExceedMaxHp()
        {
            // SCENARIUSZ:
            // 1. Unit 1/5.
            // 2. Buff +2/+2 -> Unit 3/7.
            // 3. Obrażenia 4 -> Unit 3/3.
            // 4. Ulecz 10 -> Unit 3/7 (nie 3/15! Ma wrócić do MaxHP).

            // ARRANGE
            string json = @"[
                { ""Id"": 1, ""Name"": ""Tank"", ""Type"": ""Unit"", ""Cost"": 1, ""Attack"": 1, ""Health"": 5 },
                { ""Id"": 2, ""Name"": ""Buffer"", ""Type"": ""Unit"", ""Cost"": 1, ""Attack"": 1, ""Health"": 1,
                  ""Effects"": [ { ""Trigger"": ""OnPlayed"", ""Targeting"": ""AllFriendlyUnits"", ""Actions"": [ { ""Type"": ""BuffStats"", ""BuffAtk"": 2, ""BuffHp"": 2 } ] } ] },
                { ""Id"": 3, ""Name"": ""Healer"", ""Type"": ""Unit"", ""Cost"": 1, ""Attack"": 1, ""Health"": 1,
                  ""Effects"": [ { ""Trigger"": ""OnPlayed"", ""Targeting"": ""AllFriendlyUnits"", ""Actions"": [ { ""Type"": ""Heal"", ""Amount"": 10 } ] } ] },
                { ""Id"": 4, ""Name"": ""Damager"", ""Type"": ""Unit"", ""Cost"": 1, ""Attack"": 1, ""Health"": 1,
                  ""Effects"": [ { ""Trigger"": ""OnPlayed"", ""Targeting"": ""AllFriendlyUnits"", ""Actions"": [ { ""Type"": ""DealDamage"", ""Amount"": 4 } ] } ] }
            ]";

            var engine = TestHelpers.CreateEngineWithCards(json);
            var factory = engine.Factory;

            // Stawiamy Tanka (1/5) na stole
            var tank = factory.CreateCard(1, 1);
            engine.CurrentState = engine.CurrentState.UpdateBoard(engine.CurrentState.Board.WithUnitPlacedAt(0, 1, tank));

            // Ręka: Buffer, Damager, Healer
            var pA = engine.CurrentState.PlayerA
                .WithCardAddedToHand(factory.CreateCard(2, 1))
                .WithCardAddedToHand(factory.CreateCard(3, 1))
                .WithCardAddedToHand(factory.CreateCard(4, 1));

            engine.CurrentState = engine.CurrentState.UpdatePlayer(pA.With(maxBlood: 10, currentBlood: 10));

            // --- KROK 1: BUFF ---
            engine.ExecuteCommand(new PlayUnitCommand(1, pA.Hand[0].InstanceId, 1)); // Zagrywamy Buffera

            var tankAfterBuff = engine.CurrentState.Board.Lines[0].Player1Unit;
            Assert.Equal(3, tankAfterBuff.CurrentStats.Attack); // 1 + 2
            Assert.Equal(7, tankAfterBuff.CurrentStats.Health); // 5 + 2
            Assert.Equal(7, tankAfterBuff.MaxHealth);

            // --- KROK 2: OBRAŻENIA ---
            engine.ExecuteCommand(new PlayUnitCommand(1, pA.Hand[1].InstanceId, 2)); // Zagrywamy Damagera

            var tankAfterDmg = engine.CurrentState.Board.Lines[0].Player1Unit;
            Assert.Equal(3, tankAfterDmg.CurrentStats.Health); // 7 - 4 = 3

            // --- KROK 3: LECZENIE (OVERHEAL CHECK) ---
            engine.ExecuteCommand(new PlayUnitCommand(1, pA.Hand[2].InstanceId, 3)); // Zagrywamy Healera (Leczy 10)

            var tankAfterHeal = engine.CurrentState.Board.Lines[0].Player1Unit;
            Assert.Equal(7, tankAfterHeal.CurrentStats.Health); // Powinno wrócić do Max (7), nie więcej
        }

        [Fact]
        public void ApplyStatus_ShouldAddKeyword_WithoutResettingStats()
        {
            // TESTUJEMY: ApplyStatusHandler (czy działa AddPermanentBuff z keywordem)

            // ARRANGE
            string json = @"[
                { ""Id"": 1, ""Name"": ""Knight"", ""Type"": ""Unit"", ""Cost"": 1, ""Attack"": 2, ""Health"": 2 },
                { ""Id"": 2, ""Name"": ""Armorer"", ""Type"": ""Unit"", ""Cost"": 1, ""Attack"": 1, ""Health"": 1,
                  ""Effects"": [ { ""Trigger"": ""OnPlayed"", ""Targeting"": ""AllFriendlyUnits"", ""Actions"": [ { ""Type"": ""ApplyStatus"", ""StringParam"": ""Armored"" } ] } ] }
            ]";

            var engine = TestHelpers.CreateEngineWithCards(json);

            var knight = engine.Factory.CreateCard(1, 1);
            engine.CurrentState = engine.CurrentState.UpdateBoard(engine.CurrentState.Board.WithUnitPlacedAt(0, 1, knight));

            var armorer = engine.Factory.CreateCard(2, 1);
            engine.CurrentState = engine.CurrentState.UpdatePlayer(engine.CurrentState.PlayerA
                .With(maxBlood: 10, currentBlood: 10)
                .WithCardAddedToHand(armorer));

            // ACT
            engine.ExecuteCommand(new PlayUnitCommand(1, armorer.InstanceId, 1));

            // ASSERT
            var knightOnBoard = engine.CurrentState.Board.Lines[0].Player1Unit;

            // Statystyki nie powinny się zmienić (2/2)
            Assert.Equal(2, knightOnBoard.CurrentStats.Attack);
            Assert.Equal(2, knightOnBoard.CurrentStats.Health);

            // Keyword powinien zostać dodany
            Assert.Contains(Keyword.Armored, knightOnBoard.CurrentStats.Keywords);
        }

        [Fact]
        public void SoulGuard_ShouldPreventDeathOnce_AndSetDepleted()
        {
            // TESTUJEMY: SoulGuardPrevention

            // ARRANGE
            string json = @"[
                { ""Id"": 1, ""Name"": ""Guardian"", ""Type"": ""Unit"", ""Cost"": 1, ""Attack"": 1, ""Health"": 3, ""Keywords"": [""SoulGuard""] },
                { ""Id"": 2, ""Name"": ""Killer"", ""Type"": ""Unit"", ""Cost"": 1, ""Attack"": 1, ""Health"": 1,
                  ""Effects"": [ { ""Trigger"": ""OnPlayed"", ""Targeting"": ""AllFriendlyUnits"", ""Actions"": [ { ""Type"": ""DealDamage"", ""Amount"": 10 } ] } ] }
            ]";

            var engine = TestHelpers.CreateEngineWithCards(json);

            var guardian = engine.Factory.CreateCard(1, 1);
            engine.CurrentState = engine.CurrentState.UpdateBoard(engine.CurrentState.Board.WithUnitPlacedAt(0, 1, guardian));

            var killer = engine.Factory.CreateCard(2, 1);
            engine.CurrentState = engine.CurrentState.UpdatePlayer(engine.CurrentState.PlayerA
                .With(maxBlood: 10, currentBlood: 10)
                .WithCardAddedToHand(killer));

            // ACT 1: Zabijamy Guardiana (10 dmg w 3 hp)
            engine.ExecuteCommand(new PlayUnitCommand(1, killer.InstanceId, 1));

            // ASSERT 1: Powinien przeżyć
            var guardianSurvivor = engine.CurrentState.Board.Lines[0].Player1Unit;
            Assert.NotNull(guardianSurvivor);
            Assert.Equal(1, guardianSurvivor.CurrentStats.Health); // HP = 1
            Assert.Contains(Keyword.SoulGuard, guardianSurvivor.CurrentStats.Keywords); // Stary keyword jest
            Assert.Contains(Keyword.SoulGuardDepleted, guardianSurvivor.CurrentStats.Keywords); // Nowy (depleted) też jest

            // ACT 2: Zabijamy go znowu (Wracamy Killer'a do ręki cheatem i zagrywamy ponownie)
            var killer2 = engine.Factory.CreateCard(2, 1);
            engine.CurrentState = engine.CurrentState.UpdatePlayer(engine.CurrentState.PlayerA.WithCardAddedToHand(killer2));
            engine.ExecuteCommand(new PlayUnitCommand(1, killer2.InstanceId, 2));

            // ASSERT 2: Teraz powinien zginąć definitywnie
            var deadGuardian = engine.CurrentState.Board.Lines[0].Player1Unit;
            Assert.Null(deadGuardian);
        }

        [Fact]
        public void Unkillable_ShouldReturnToHand_ResetStats()
        {
            // TESTUJEMY: UnkillablePrevention i reset statystyk (Create new Instance)

            // ARRANGE
            string json = @"[
                { ""Id"": 1, ""Name"": ""Phoenix"", ""Type"": ""Unit"", ""Cost"": 1, ""Attack"": 1, ""Health"": 1, ""Keywords"": [""Unkillable""] },
                { ""Id"": 2, ""Name"": ""Killer"", ""Type"": ""Unit"", ""Cost"": 1, ""Attack"": 1, ""Health"": 1,
                  ""Effects"": [ { ""Trigger"": ""OnPlayed"", ""Targeting"": ""AllFriendlyUnits"", ""Actions"": [ { ""Type"": ""DealDamage"", ""Amount"": 10 } ] } ] }
            ]";

            var engine = TestHelpers.CreateEngineWithCards(json);

            var phoenix = engine.Factory.CreateCard(1, 1);
            // Dajemy mu buffa (+5/+5) przed śmiercią, żeby sprawdzić czy zniknie po powrocie do ręki
            var buffedPhoenix = phoenix.AddPermanentBuff(new CardStats(5, 5, 0));

            engine.CurrentState = engine.CurrentState.UpdateBoard(engine.CurrentState.Board.WithUnitPlacedAt(0, 1, buffedPhoenix));

            var killer = engine.Factory.CreateCard(2, 1);
            engine.CurrentState = engine.CurrentState.UpdatePlayer(engine.CurrentState.PlayerA
                .With(maxBlood: 10, currentBlood: 10)
                .WithCardAddedToHand(killer));

            // ACT: Zabijamy Feniksa (6 HP -> 10 Dmg)
            engine.ExecuteCommand(new PlayUnitCommand(1, killer.InstanceId, 1));

            // ASSERT:
            // 1. Nie ma go na stole
            Assert.Null(engine.CurrentState.Board.Lines[0].Player1Unit);

            // 2. Jest w ręce
            var phoenixInHand = engine.CurrentState.PlayerA.Hand.Last(); // Powinien być ostatni dodany
            Assert.Equal("Phoenix", phoenixInHand.Definition.Name);

            // 3. Jest "czysty" (1/1, a nie 6/6)
            Assert.Equal(1, phoenixInHand.CurrentStats.Attack);
            Assert.Equal(1, phoenixInHand.CurrentStats.Health);
        }
    }
}