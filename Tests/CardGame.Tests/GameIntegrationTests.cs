using System.Linq;
using CardGame.Core.Commands.Implementations;
using CardGame.Core.Cards.Models;
using CardGame.Core.State.Models;

using Xunit;


namespace CardGame.Tests
{
    public class GameIntegrationTests
    {
        [Fact]
        public void AoE_TargetAllEnemies_ShouldDamageAllEnemies()
        {
            // ARRANGE
            string json = @"[
                { 
                  ""Id"": 1, ""Name"": ""Bombardier"", ""Type"": ""Unit"", ""Cost"": 1, ""Attack"": 1, ""Health"": 1,
                  ""Effects"": [ 
                    { 
                      ""Trigger"": ""OnPlayed"", 
                      ""Actions"": [ 
                        { 
                          ""Type"": ""DealDamage"", 
                          ""Target"": ""AllEnemyUnits"",
                          ""Amount"": 2 
                        } 
                      ] 
                    } 
                  ] 
                },
                { ""Id"": 2, ""Name"": ""Enemy Dummy"", ""Type"": ""Unit"", ""Cost"": 0, ""Attack"": 0, ""Health"": 2 }
            ]";

            var engine = TestHelpers.CreateEngineWithCards(json);
            var factory = engine.Factory;

            // Ustawienie planszy: 2 wrogów (2 HP)
            var enemy1 = factory.CreateCard(2, 2);
            var enemy2 = factory.CreateCard(2, 2);

            // Wa¿ne: Musimy stworzyæ now¹ planszê z jednostkami
            var board = engine.CurrentState.Board
                .WithUnitPlacedAt(0, 2, enemy1)
                .WithUnitPlacedAt(1, 2, enemy2);

            // Gracz ma Bombardiera
            var bomber = factory.CreateCard(1, 1);
            var pA = engine.CurrentState.PlayerA.WithCardAddedToHand(bomber);

            // Aktualizacja stanu
            engine.CurrentState = engine.CurrentState.With(board: board, playerA: pA);

            // ACT
            // Zagrywamy Bombardiera na liniê 0
            engine.ExecuteCommand(new PlayUnitCommand(1, bomber.InstanceId, 0));

            // ASSERT
            // Sprawdzamy czy wrogowie zniknêli z planszy (GetAllUnits zwraca tylko ¿ywych)
            var unitsOnBoard = engine.CurrentState.Board.GetAllUnits();

            Assert.DoesNotContain(unitsOnBoard, u => u.InstanceId == enemy1.InstanceId);
            Assert.DoesNotContain(unitsOnBoard, u => u.InstanceId == enemy2.InstanceId);
        }

        [Fact]
        public void RadioDemon_GlobalBuff_ShouldBuffExistingAndNewUnits()
        {
            // ARRANGE
            string json = @"[
                { ""Id"": 1, ""Name"": ""Radio Demon"", ""Type"": ""Unit"", ""Cost"": 1, ""Attack"": 1, ""Health"": 1,
                  ""Effects"": [ { ""Trigger"": ""OnPlayed"", ""Targeting"": ""FriendlyHero"", ""Actions"": [ { ""Type"": ""ModifyGlobalBuff"", ""BuffAtk"": 1, ""BuffHp"": 1 } ] } ] },
                { ""Id"": 2, ""Name"": ""Small Guy"", ""Type"": ""Unit"", ""Cost"": 0, ""Attack"": 1, ""Health"": 1 }
            ]";

            var engine = TestHelpers.CreateEngineWithCards(json);
            var factory = engine.Factory;

            // Sytuacja: Jeden 'Small Guy' ju¿ stoi na stole
            var existingGuy = factory.CreateCard(2, 1);
            engine.CurrentState = engine.CurrentState.UpdateBoard(
                engine.CurrentState.Board.WithUnitPlacedAt(0, 1, existingGuy));

            // Gracz ma Radio Demona i drugiego 'Small Guy' w rêce
            var demon = factory.CreateCard(1, 1);
            var newGuy = factory.CreateCard(2, 1);

            engine.CurrentState = engine.CurrentState.UpdatePlayer(
                engine.CurrentState.PlayerA
                    .WithCardAddedToHand(demon)
                    .WithCardAddedToHand(newGuy));

            // ACT 1: Zagraj Radio Demona
            engine.ExecuteCommand(new PlayUnitCommand(1, demon.InstanceId, 1));

            // ASSERT 1: Istniej¹cy goœæ powinien urosn¹æ (1/1 -> 2/2)
            var existingOnBoard = engine.CurrentState.Board.Lines[0].Player1Unit;
            Assert.Equal(2, existingOnBoard.CurrentStats.Attack);
            Assert.Equal(2, existingOnBoard.CurrentStats.Health);

            // ACT 2: Zagraj nowego goœcia z rêki
            engine.ExecuteCommand(new PlayUnitCommand(1, newGuy.InstanceId, 2));

            // ASSERT 2: Nowy goœæ powinien wejœæ od razu z buffem (1/1 -> 2/2)
            var newOnBoard = engine.CurrentState.Board.Lines[2].Player1Unit;
            Assert.Equal(2, newOnBoard.CurrentStats.Attack);
            Assert.Equal(2, newOnBoard.CurrentStats.Health);
        }

        [Fact]
        public void Interruption_DoubleTarget_ShouldPauseAndResume()
        {
            // ARRANGE
            // Karta: Zniszcz cel 1 (wybór) -> Zniszcz cel 2 (wrogi)
            string json = @"[
                { ""Id"": 1, ""Name"": ""Double Agent"", ""Type"": ""Unit"", ""Cost"": 1, ""Attack"": 1, ""Health"": 1,
                  ""Effects"": [ 
                    { ""Trigger"": ""OnPlayed"", 
                      ""Actions"": [ 
                        { ""Type"": ""DestroyUnit"", ""Target"": ""SelectedTarget"" },
                        { ""Type"": ""DestroyUnit"", ""Target"": ""TargetEnemyUnit"" } 
                      ] 
                    } 
                  ] 
                },
                { ""Id"": 2, ""Name"": ""Target Dummy"", ""Type"": ""Unit"", ""Cost"": 0, ""Attack"": 0, ""Health"": 5 }
            ]";

            var engine = TestHelpers.CreateEngineWithCards(json);
            var factory = engine.Factory;

            var allyTarget = factory.CreateCard(2, 1);
            var enemyTarget = factory.CreateCard(2, 2);

            // Plansza: Sojusznik na L0, Wróg na L1
            var board = engine.CurrentState.Board
                .WithUnitPlacedAt(0, 1, allyTarget)
                .WithUnitPlacedAt(1, 2, enemyTarget);

            var agent = factory.CreateCard(1, 1);
            engine.CurrentState = engine.CurrentState.With(board: board,
                playerA: engine.CurrentState.PlayerA.WithCardAddedToHand(agent));

            // ACT 1: Zagrywamy Agenta, celuj¹c w Sojusznika (Cel 1)
            // Stawiamy Agenta na Liniê 2
            engine.ExecuteCommand(new PlayUnitCommand(1, agent.InstanceId, 2, selectedTargetId: allyTarget.InstanceId));

            // ASSERT 1: Stan "W TRAKCIE"
            // 1. Sojusznik powinien zgin¹æ (akcja 1 wykonana)
            Assert.Null(engine.CurrentState.Board.Lines[0].Player1Unit);

            // 2. Gra powinna byæ zapauzowana (czeka na TargetEnemyUnit)
            Assert.NotNull(engine.CurrentState.PendingInteraction);
            Assert.Equal(CardGame.Core.Cards.Data.TargetType.TargetEnemyUnit,
                         engine.CurrentState.PendingInteraction.RequiredTargetType);

            // ACT 2: Wybieramy drugi cel (Wróg)
            engine.ExecuteCommand(new SelectTargetCommand(1, enemyTarget.InstanceId));

            // ASSERT 2: Stan "ZAKOÑCZONY"
            // 1. Gra wznowiona i zakoñczona (Pending null)
            Assert.Null(engine.CurrentState.PendingInteraction);

            // 2. Wróg powinien zgin¹æ (akcja 2 wykonana)
            Assert.Null(engine.CurrentState.Board.Lines[1].Player2Unit);
        }
    }
}