using System.Collections.Generic;
using System.Linq;
using CardGame.Core.Application;
using CardGame.Core.Cards.Data;
using CardGame.Core.Commands.Implementations;
using CardGame.Core.State.Enums;
using Xunit;

namespace CardGame.Tests
{
    public class DeathTriggersFullTests
    {
        private string GetDeathMechanicsJson() => @"[
          {
            ""Id"": 1, ""Name"": ""Bone Sommelier"", ""Type"": ""Unit"", ""Cost"": 1, ""Attack"": 1, ""Health"": 1,
            ""Effects"": [ { ""Trigger"": ""OnDeath"", ""Actions"": [ { ""Type"": ""AddCardToHand"", ""Target"": ""FriendlyHero"", ""ValueParam"": 900 } ] } ]
          },
          {
            ""Id"": 2, ""Name"": ""Mokke"", ""Type"": ""Unit"", ""Cost"": 1, ""Attack"": 1, ""Health"": 2,
            ""Effects"": [ { ""Trigger"": ""OnDeath"", ""Actions"": [ { ""Type"": ""Heal"", ""Target"": ""FriendlyHero"", ""Amount"": 3 } ] } ]
          },
          {
            ""Id"": 3, ""Name"": ""BoomBots"", ""Type"": ""Unit"", ""Cost"": 1, ""Attack"": 1, ""Health"": 1,
            ""Effects"": [ { ""Trigger"": ""OnDeath"", ""Actions"": [ { ""Type"": ""DealDamage"", ""Target"": ""EnemyHero"", ""Amount"": 2 } ] } ]
          },
          {
            ""Id"": 900, ""Name"": ""The Alluring Goblet"", ""Type"": ""Spell"", ""Cost"": 1,
            ""Effects"": [ {
                ""Trigger"": ""OnPlayed"", ""Targeting"": ""TargetFriendlyUnit"", 
                ""Actions"": [ 
                    { ""Type"": ""ApplyStatus"", ""Target"": ""SelectedTarget"", ""StatusKeyword"": ""Unkillable"" },
                    { ""Type"": ""DestroyUnit"", ""Target"": ""SelectedTarget"" }
                ]
            } ]
          },
          { ""Id"": 99, ""Name"": ""Executioner"", ""Type"": ""Spell"", ""Cost"": 0, 
            ""Effects"": [ { ""Trigger"": ""OnPlayed"", ""Actions"": [ { ""Type"": ""DealDamage"", ""Target"": ""TargetEnemyUnit"", ""Amount"": 10 } ] } ] }
        ]";

        [Fact]
        public void BoneSommelier_Death_GivesGobletToCorrectPlayer()
        {
            var engine = TestHelpers.CreateEngineWithCards(GetDeathMechanicsJson());
            var sommelier = engine.Factory.CreateCard(1, 1); // P1
            var killer = engine.Factory.CreateCard(99, 2);    // P2

            engine.CurrentState = engine.CurrentState.UpdateBoard(engine.CurrentState.Board.WithUnitPlacedAt(0, 1, sommelier));
            engine.CurrentState = engine.CurrentState.UpdatePlayer(engine.CurrentState.PlayerB.WithCardAddedToHand(killer));
            engine.CurrentState = engine.CurrentState.With(
    currentPhase: GamePhase.ActionOnly,
    activePlayerId: 2 // <--- To jest kluczowe!
);  

            // P2 zabija jednostkę P1
            engine.ExecuteCommand(new PlaySpellCommand(2, killer.InstanceId, sommelier.InstanceId));

            // Bone Sommelier (P1) umiera -> Goblet powinien trafić do P1
            Assert.Contains(engine.CurrentState.PlayerA.Hand, c => c.Definition.Id == "900");
            Assert.DoesNotContain(engine.CurrentState.PlayerB.Hand, c => c.Definition.Id == "900");
        }

        [Fact]
        public void Mokke_Death_HealsFriendlyHero()
        {
            var engine = TestHelpers.CreateEngineWithCards(GetDeathMechanicsJson());
            var mokke = engine.Factory.CreateCard(2, 1);

            // P1 ma 10 HP
            engine.CurrentState = engine.CurrentState.UpdatePlayer(engine.CurrentState.PlayerA.With(health: 10));
            engine.CurrentState = engine.CurrentState.UpdateBoard(engine.CurrentState.Board.WithUnitPlacedAt(0, 1, mokke));

            // Symulujemy śmierć przez obrażenia (np. z walki lub efektu)
            var deadMokke = mokke.TakeDamage(10);
            engine.CurrentState = engine.CurrentState.UpdateBoard(engine.CurrentState.Board.UpdateUnit(deadMokke));

            // Wywołujemy silnik, aby sprzątnął zwłoki i odpalił triggery
            engine.ExecuteCommand(new EndPhaseCommand(1));

            Assert.Equal(13, engine.CurrentState.PlayerA.Health);
        }

        [Fact]
        public void BoomBots_Death_DamagesEnemyHero()
        {
            var engine = TestHelpers.CreateEngineWithCards(GetDeathMechanicsJson());
            var bot = engine.Factory.CreateCard(3, 1); // Gracz 1
            engine.CurrentState = engine.CurrentState.UpdateBoard(engine.CurrentState.Board.WithUnitPlacedAt(0, 1, bot));

            // Zabijamy bota
            var deadBot = bot.TakeDamage(5);
            engine.CurrentState = engine.CurrentState.UpdateBoard(engine.CurrentState.Board.UpdateUnit(deadBot));
            engine.ExecuteCommand(new EndPhaseCommand(1));

            // Gracz 2 powinien stracić 2 HP (30 -> 28)
            Assert.Equal(28, engine.CurrentState.PlayerB.Health);
        }

        [Fact]
        public void AlluringGoblet_Combo_ReturnsUnitToHand()
        {
            var engine = TestHelpers.CreateEngineWithCards(GetDeathMechanicsJson());
            var unit = engine.Factory.CreateCard(2, 1); // Mokke
            var goblet = engine.Factory.CreateCard(900, 1);

            engine.CurrentState = engine.CurrentState.UpdateBoard(engine.CurrentState.Board.WithUnitPlacedAt(0, 1, unit));
            engine.CurrentState = engine.CurrentState.UpdatePlayer(engine.CurrentState.PlayerA.WithCardAddedToHand(goblet));
            engine.CurrentState = engine.CurrentState.With(currentPhase: GamePhase.ActionOnly);

            // Zagranie Goblet na Mokke
            // Mechanika: ApplyStatus(Unkillable) -> DestroyUnit -> Trigger Unkillable(MoveToHand)
            engine.ExecuteCommand(new PlaySpellCommand(1, goblet.InstanceId, unit.InstanceId));

            // Jednostka powinna zniknąć z planszy i wrócić do ręki
            Assert.Null(engine.CurrentState.Board.Lines[0].Player1Unit);
            Assert.Contains(engine.CurrentState.PlayerA.Hand, c => c.InstanceId == unit.InstanceId);

            // Ponieważ Mokke "zginął" (mimo że wrócił do ręki), jego OnDeath też powinien się odpalić!
            Assert.Equal(30, engine.CurrentState.PlayerA.Health); // 30 + 3 z Mokke
        }
    }
}