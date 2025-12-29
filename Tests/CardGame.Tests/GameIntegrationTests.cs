using System.Linq;
using CardGame.Core.Commands.Implementations;
using CardGame.Core.Cards.Models;
using CardGame.Core.State.Models;
using CardGame.Core.Cards.Data;
using Xunit;

namespace CardGame.Tests
{
    public class GameIntegrationTests
    {
        [Fact]
        public void AoE_TargetAllEnemies_ShouldDamageAllEnemies()
        {
            string json = @"[
                { ""Id"": 1, ""Name"": ""Bombardier"", ""Type"": ""Unit"", ""Cost"": 1, ""Attack"": 1, ""Health"": 1,
                  ""Effects"": [ { ""Trigger"": ""OnPlayed"", ""Actions"": [ { ""Type"": ""DealDamage"", ""Target"": ""AllEnemyUnits"", ""Amount"": 2 } ] } ] },
                { ""Id"": 2, ""Name"": ""Dummy"", ""Type"": ""Unit"", ""Cost"": 0, ""Attack"": 0, ""Health"": 2 }
            ]";
            var engine = TestHelpers.CreateEngineWithCards(json);
            var enemy1 = engine.Factory.CreateCard(2, 2);
            var enemy2 = engine.Factory.CreateCard(2, 2);
            engine.CurrentState = engine.CurrentState.With(board: engine.CurrentState.Board.WithUnitPlacedAt(0, 2, enemy1).WithUnitPlacedAt(1, 2, enemy2));
            var bomber = engine.Factory.CreateCard(1, 1);
            engine.CurrentState = engine.CurrentState.UpdatePlayer(engine.CurrentState.PlayerA.WithCardAddedToHand(bomber));
            engine.ExecuteCommand(new PlayUnitCommand(1, bomber.InstanceId, 0));
            Assert.Empty(engine.CurrentState.Board.GetAllUnits().Where(u => u.OwnerPlayerId == 2));
        }

        [Fact]
        public void RadioDemon_GlobalBuff_ShouldBuffExistingAndNewUnits()
        {
            string json = @"[
                { ""Id"": 1, ""Name"": ""Radio Demon"", ""Type"": ""Unit"", ""Cost"": 1, ""Attack"": 1, ""Health"": 1,
                  ""Effects"": [ { ""Trigger"": ""OnPlayed"", ""Targeting"": ""FriendlyHero"", ""Actions"": [ { ""Type"": ""ModifyGlobalBuff"", ""BuffAtk"": 1, ""BuffHp"": 1 } ] } ] },
                { ""Id"": 2, ""Name"": ""Small Guy"", ""Type"": ""Unit"", ""Cost"": 0, ""Attack"": 1, ""Health"": 1 }
            ]";
            var engine = TestHelpers.CreateEngineWithCards(json);
            var existingGuy = engine.Factory.CreateCard(2, 1);
            engine.CurrentState = engine.CurrentState.UpdateBoard(engine.CurrentState.Board.WithUnitPlacedAt(0, 1, existingGuy));
            var demon = engine.Factory.CreateCard(1, 1);
            engine.CurrentState = engine.CurrentState.UpdatePlayer(engine.CurrentState.PlayerA.WithCardAddedToHand(demon).WithCardAddedToHand(engine.Factory.CreateCard(2, 1)));
            engine.ExecuteCommand(new PlayUnitCommand(1, demon.InstanceId, 1));
            Assert.Equal(2, engine.CurrentState.Board.Lines[0].Player1Unit!.CurrentStats.Attack);
            engine.ExecuteCommand(new PlayUnitCommand(1, engine.CurrentState.PlayerA.Hand[0].InstanceId, 2));
            Assert.Equal(2, engine.CurrentState.Board.Lines[2].Player1Unit!.CurrentStats.Attack);
        }

        [Fact]
        public void Interruption_DoubleTarget_ShouldAutoResolve_WhenOneValidTargetPerStep()
        {
            string json = @"[
                { ""Id"": 1, ""Name"": ""Double Agent"", ""Type"": ""Unit"", ""Cost"": 1, ""Attack"": 1, ""Health"": 1,
                  ""Effects"": [ 
                    { ""Trigger"": ""OnPlayed"", 
                      ""Actions"": [ 
                        { ""Type"": ""DestroyUnit"", ""Target"": ""TargetFriendlyUnit"" },
                        { ""Type"": ""DestroyUnit"", ""Target"": ""TargetEnemyUnit"" } 
                      ] 
                    } 
                  ] 
                },
                { ""Id"": 2, ""Name"": ""Dummy"", ""Type"": ""Unit"", ""Cost"": 0, ""Attack"": 0, ""Health"": 5 }
            ]";
            var engine = TestHelpers.CreateEngineWithCards(json);
            var ally = engine.Factory.CreateCard(2, 1);
            var enemy = engine.Factory.CreateCard(2, 2);
            engine.CurrentState = engine.CurrentState.With(board: engine.CurrentState.Board.WithUnitPlacedAt(0, 1, ally).WithUnitPlacedAt(1, 2, enemy));
            var agent = engine.Factory.CreateCard(1, 1);
            engine.CurrentState = engine.CurrentState.UpdatePlayer(engine.CurrentState.PlayerA.WithCardAddedToHand(agent));
            engine.ExecuteCommand(new PlayUnitCommand(1, agent.InstanceId, 2));
            Assert.Null(engine.CurrentState.Board.Lines[0].Player1Unit);
            Assert.Null(engine.CurrentState.Board.Lines[1].Player2Unit);
        }
    }
}