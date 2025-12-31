using System.Collections.Generic;
using System.Linq;
using CardGame.Core.Application;
using CardGame.Core.Cards.Data;
using CardGame.Core.Commands.Implementations;
using CardGame.Core.State.Enums;
using CardGame.Core.State.Models;
using Xunit;

namespace CardGame.Tests
{
    public class AdvancedMechanicsTests
    {
        private string GetTestJson() => @"[
            { ""Id"": 10, ""Name"": ""Machine"", ""Type"": ""Unit"", ""Subtypes"": [""Machine""], ""Attack"": 1, ""Health"": 1 },
            { ""Id"": 50, ""Name"": ""Soldat"", ""Type"": ""Unit"", ""Subtypes"": [""Mercenary""], ""Attack"": 1, ""Health"": 1,
              ""Effects"": [ { ""Trigger"": ""OnPlayed"", ""Condition"": { ""Condition"": ""HasSubtypeOnBoard"", ""TargetParam"": ""Machine"" }, 
              ""Actions"": [ { ""Type"": ""BuffStats"", ""Target"": ""Self"", ""BuffAtk"": 2, ""BuffHp"": 2 } ] } ] },
            { ""Id"": 60, ""Name"": ""Spawner"", ""Type"": ""Unit"", ""Attack"": 1, ""Health"": 1,
              ""Effects"": [ { ""Trigger"": ""OnDeath"", ""Actions"": [ { ""Type"": ""MakeAUnit"", ""ValueParam"": 701, ""StringParam"": ""AdjacentLanes"" } ] } ] },
            { ""Id"": 65, ""Name"": ""ManualSpawner"", ""Type"": ""Unit"", ""Attack"": 1, ""Health"": 1,
              ""Effects"": [ { ""Trigger"": ""OnPlayed"", ""Actions"": [ { ""Type"": ""MakeAUnit"", ""ValueParam"": 701, ""StringParam"": ""Choose"" } ] } ] },
            { ""Id"": 701, ""Name"": ""Token"", ""Type"": ""Unit"", ""Attack"": 1, ""Health"": 1 }
        ]";

        [Fact]
        public void Soldat_ConditionTest_ShouldOnlyBuffWhenMachinePresent()
        {
            var engine = TestHelpers.CreateEngineWithCards(GetTestJson());
            var factory = engine.Factory;

            // SCENARIUSZ 1: Brak maszyny
            var soldat1 = factory.CreateCard(50, 1);
            engine.CurrentState = engine.CurrentState.UpdatePlayer(engine.CurrentState.PlayerA.WithCardAddedToHand(soldat1));
            engine.ExecuteCommand(new PlayUnitCommand(1, soldat1.InstanceId, 0));

            var onBoard1 = engine.CurrentState.Board.Lines[0].Player1Unit;
            Assert.Equal(1, onBoard1.CurrentStats.Attack); // Powinien zostać 1/1

            // SCENARIUSZ 2: Jest maszyna
            var machine = factory.CreateCard(10, 1);
            var soldat2 = factory.CreateCard(50, 1);
            engine.CurrentState = engine.CurrentState.UpdateBoard(engine.CurrentState.Board.WithUnitPlacedAt(3, 1, machine));
            engine.CurrentState = engine.CurrentState.UpdatePlayer(engine.CurrentState.PlayerA.WithCardAddedToHand(soldat2));

            engine.ExecuteCommand(new PlayUnitCommand(1, soldat2.InstanceId, 1));

            var onBoard2 = engine.CurrentState.Board.Lines[1].Player1Unit;
            Assert.Equal(3, onBoard2.CurrentStats.Attack); // Powinien dostać buffa 3/3
        }

        [Fact]
        public void MakeAUnit_AdjacentLanes_ShouldHandleOccupiedLanes()
        {
            var engine = TestHelpers.CreateEngineWithCards(GetTestJson());
            var spawner = engine.Factory.CreateCard(60, 1);
            var blocker = engine.Factory.CreateCard(10, 1);

            // Ustawienie: [Blocker][Spawner][Puste][Puste]
            engine.CurrentState = engine.CurrentState.UpdateBoard(engine.CurrentState.Board
                .WithUnitPlacedAt(0, 1, blocker)
                .WithUnitPlacedAt(1, 1, spawner));

            // ACT: Zabijamy Spawnera
            var deadSpawner = spawner.TakeDamage(10);
            engine.CurrentState = engine.CurrentState.UpdateBoard(engine.CurrentState.Board.UpdateUnit(deadSpawner));
            engine.ExecuteCommand(new EndPhaseCommand(1));

            // ASSERT: 
            // Linia 0: Była zajęta przez blockera, więc Token nie mógł się tam zrespić.
            // Linia 2: Była pusta, więc Token powinien tam być.
            Assert.Equal("Machine", engine.CurrentState.Board.Lines[0].Player1Unit.Definition.Name);
            Assert.Equal("Token", engine.CurrentState.Board.Lines[2].Player1Unit.Definition.Name);
            Assert.Null(engine.CurrentState.Board.Lines[1].Player1Unit); // Spawner zniknął
        }

        [Fact]
        public void MakeAUnit_AdjacentLanes_ShouldSpawnInNeighbors()
        {
            var engine = TestHelpers.CreateEngineWithCards(GetTestJson());
            var spawner = engine.Factory.CreateCard(60, 1); // Spawner na OnDeath

            // Ustawiamy Spawnera w linii 1
            engine.CurrentState = engine.CurrentState.UpdateBoard(engine.CurrentState.Board.WithUnitPlacedAt(1, 1, spawner));

            // ACT: Zabijamy go
            var dead = spawner.TakeDamage(10);
            engine.CurrentState = engine.CurrentState.UpdateBoard(engine.CurrentState.Board.UpdateUnit(dead));
            engine.ExecuteCommand(new EndPhaseCommand(1)); // Sprzątanie zwłok odpala OnDeath

            // ASSERT: Tokeny powinny być w linii 0 i 2
            Assert.NotNull(engine.CurrentState.Board.Lines[0].Player1Unit);
            Assert.NotNull(engine.CurrentState.Board.Lines[2].Player1Unit);
        }

        public void MakeAUnit_FirstFree_ShouldSpawnInLineZero()
        {
            var engine = TestHelpers.CreateEngineWithCards(@"[
        { ""Id"": 99, ""Name"": ""Spawner"", ""Type"": ""Unit"", ""Cost"": 0, ""Effects"": [ { ""Trigger"": ""OnPlayed"", ""Actions"": [ { ""Type"": ""MakeAUnit"", ""ValueParam"": 701 } ] } ] },
        { ""Id"": 701, ""Name"": ""Token"", ""Type"": ""Unit"", ""Cost"": 0 }
    ]");
            var f = engine.Factory;
            var card = f.CreateCard(99, 1);

            // Gwarantujemy krew i fazę
            engine.CurrentState = engine.CurrentState.UpdatePlayer(engine.CurrentState.PlayerA
                .WithCardAddedToHand(card)
                .WithResourceChanged(ResourceType.Blood, 10, 10))
                .With(currentPhase: GamePhase.UnitOnly);

            // ACT: Gramy spawner w linię 3
            engine.ExecuteCommand(new PlayUnitCommand(1, card.InstanceId, 3));

            // ASSERT: Sprawdź czy Token (701) jest gdziekolwiek na planszy u Gracza 1
            var allUnits = engine.CurrentState.Board.GetAllUnits().Where(u => u.OwnerPlayerId == 1).ToList();

            Assert.Contains(allUnits, u => u.Definition.Name == "Token");
            Assert.NotNull(engine.CurrentState.Board.Lines[0].Player1Unit);
            Assert.Equal("Token", engine.CurrentState.Board.Lines[0].Player1Unit.Definition.Name);
        }

        [Fact]
        public void MakeAUnit_Random_ShouldPickAvailableSlot()
        {
            // Przygotowujemy planszę tak, że tylko jedna linia (nr 2) jest wolna
            var engine = TestHelpers.CreateEngineWithCards(@"[
                { ""Id"": 1, ""Name"": ""Dummy"", ""Type"": ""Unit"", ""Attack"": 1, ""Health"": 1 },
                { ""Id"": 99, ""Name"": ""RandomSpawn"", ""Type"": ""Unit"", ""Effects"": [ { ""Trigger"": ""OnPlayed"", ""Actions"": [ { ""Type"": ""MakeAUnit"", ""ValueParam"": 701, ""StringParam"": ""Random"" } ] } ] },
                { ""Id"": 701, ""Name"": ""Token"", ""Type"": ""Unit"", ""Attack"": 1, ""Health"": 1 }
            ]");
            var f = engine.Factory;

            engine.CurrentState = engine.CurrentState.UpdateBoard(engine.CurrentState.Board
                .WithUnitPlacedAt(0, 1, f.CreateCard(1, 1))
                .WithUnitPlacedAt(1, 1, f.CreateCard(1, 1))
                .WithUnitPlacedAt(3, 1, f.CreateCard(1, 1)));

            var randomCard = f.CreateCard(99, 1);
            engine.CurrentState = engine.CurrentState.UpdatePlayer(engine.CurrentState.PlayerA.WithCardAddedToHand(randomCard));

            // ACT: Zagranie karty (zajmuje ostatni wolny slot na planszy - NIE, PlayUnit potrzebuje wolnej linii)
            // Musimy zagrać ją "wirtualnie" lub zwolnić slot.
            // Zagrajmy w linię 2 - jedyną wolną.
            engine.ExecuteCommand(new PlayUnitCommand(1, randomCard.InstanceId, 2));

            // Teraz wszystkie linie są zajęte. Random nie powinien nic zrobić (lub zwrócić stary stan).
            Assert.Null(engine.CurrentState.PendingInteraction);
        }
    }
}