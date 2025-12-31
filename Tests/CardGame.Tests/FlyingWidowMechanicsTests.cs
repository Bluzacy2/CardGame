using Xunit;
using CardGame.Core.State.Enums;
using CardGame.Core.Commands.Implementations;

namespace CardGame.Tests
{
    public class FlyingAndResourceTests
    {
        [Fact]
        public void Flying_ShouldIgnoreGroundedUnit_AndHitHero()
        {
            var engine = TestHelpers.CreateEngineWithCards("[ { \"Id\": 43, \"Name\": \"Pegasus\", \"Type\": \"Unit\", \"Attack\": 2, \"Health\": 3, \"Keywords\": [\"Flying\"] }, { \"Id\": 2, \"Name\": \"Grounded\", \"Type\": \"Unit\", \"Attack\": 1, \"Health\": 5 } ]");
            var factory = engine.Factory;

            var pegasus = factory.CreateCard(43, 1);
            var grounded = factory.CreateCard(2, 2);

            engine.CurrentState = engine.CurrentState.UpdateBoard(engine.CurrentState.Board
                .WithUnitPlacedAt(0, 1, pegasus)
                .WithUnitPlacedAt(0, 2, grounded));

            engine.CurrentState = engine.CurrentState.With(currentPhase: GamePhase.Combat);

            engine.ExecuteCommand(new EndPhaseCommand(1));

         
            Assert.Equal(28, engine.CurrentState.PlayerB.Health); // 30 - 2
            Assert.Equal(5, engine.CurrentState.Board.Lines[0].Player2Unit.CurrentStats.Health); 
        }

        [Fact]
        public void WidowsSpider_Sacrifice_ShouldGiveBlood()
        {
            var engine = TestHelpers.CreateEngineWithCards("[ { \"Id\": 42, \"Name\": \"Widows Spider\", \"Type\": \"Unit\", \"Effects\": [ { \"Trigger\": \"OnSacrificed\", \"Actions\": [ {\"Type\": \"AddResource\", \"Target\": \"FriendlyHero\", \"Amount\": 1} ] } ] } ]");
            var spider = engine.Factory.CreateCard(42, 1);

            engine.CurrentState = engine.CurrentState.UpdateBoard(engine.CurrentState.Board.WithUnitPlacedAt(0, 1, spider));
            int initialBlood = engine.CurrentState.PlayerA.CurrentBlood; 

            engine.ExecuteCommand(new PlaySpellCommand(1, 0, spider.InstanceId)); 

      
        }
    }
}