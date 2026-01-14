using Xunit;
using CardGame.Core.State.Enums;
using CardGame.Core.Commands.Implementations;
using CardGame.Core.GameRules.Auras;
using System.Linq;

namespace CardGame.Tests
{
    public class AntiMatterMonsterTests
    {
        [Fact]
        public void MonsterTax_ShouldIncreaseEnemyCost_AndResetOnDeath()
        {
            // 1. SETUP - DODANO Attack i Health
            string json = @"[
        { 
            ""Id"": 60, ""Name"": ""Anti Matter"", ""Type"": ""Unit"", 
            ""Attack"": 6, ""Health"": 8, ""Cost"": 8, ""Effects"": [
            { ""Trigger"": ""Passive"", ""Zone"": ""Board"", ""Actions"": [
                { ""Type"": ""BuffStats"", ""Target"": ""EnemySpellsInHand"", ""Amount"": 6 }
            ]}
        ]},
        { 
            ""Id"": 46, ""Name"": ""Tea Maid"", ""Type"": ""Unit"", 
            ""Attack"": 1, ""Health"": 5, ""Cost"": 3, ""Effects"": [
            { ""Trigger"": ""Passive"", ""Zone"": ""Board"", ""Actions"": [
                { ""Type"": ""BuffStats"", ""Target"": ""FriendlySpellsInHand"", ""Amount"": -1 }
            ]}
        ]},
        { ""Id"": 7, ""Name"": ""Glock"", ""Type"": ""Spell"", ""Cost"": 2 }
    ]";

            var engine = TestHelpers.CreateEngineWithCards(json);
            var f = engine.Factory;
            var auraSystem = new AuraSystem();

            var monster = f.CreateCard(60, 1); // Gracz 1
            var maid = f.CreateCard(46, 2);    // Gracz 2
            var spell = f.CreateCard(7, 2);    // Czar w ręce Gracza 2

            engine.CurrentState = engine.CurrentState.UpdatePlayer(engine.CurrentState.PlayerB.WithCardAddedToHand(spell));

            // 2. TEST: Sam potwór na stole
            engine.CurrentState = engine.CurrentState.UpdateBoard(engine.CurrentState.Board.WithUnitPlacedAt(0, 1, monster));
            engine.CurrentState = auraSystem.RecalculateAuras(engine.CurrentState);

            var spellAfterTax = engine.CurrentState.PlayerB.Hand.First();
            // Expected: 2 (Base) - (-6) = 8
            Assert.Equal(8, spellAfterTax.CurrentStats.BloodCost);

            // 3. TEST: Dochodzi Tea Maid (Gracz 2 obniża koszty)
            engine.CurrentState = engine.CurrentState.UpdateBoard(engine.CurrentState.Board.WithUnitPlacedAt(0, 2, maid));
            engine.CurrentState = auraSystem.RecalculateAuras(engine.CurrentState);

            var spellWithMaid = engine.CurrentState.PlayerB.Hand.First();
            // Expected: 2 (Base) - (-6 tax) - (1 reduction) = 7
            Assert.Equal(7, spellWithMaid.CurrentStats.BloodCost);

            // 4. TEST: Potwór ginie
            var deadMonster = monster.TakeDamage(99);
            engine.CurrentState = engine.CurrentState.UpdateBoard(engine.CurrentState.Board.UpdateUnit(deadMonster));
            engine.ExecuteCommand(new EndPhaseCommand(1)); // To usunie potwora z planszy i przeliczy aury

            var spellFinal = engine.CurrentState.PlayerB.Hand.First();
            // Expected: 2 (Base) - 1 (Reduction) = 1
            Assert.Equal(1, spellFinal.CurrentStats.BloodCost);
        }
    }
}