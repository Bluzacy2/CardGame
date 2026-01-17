using Xunit;
using CardGame.Core.Application;
using CardGame.Core.State.Models;
using CardGame.Core.Combat;
using CardGame.Core.Events;
using CardGame.Core.State.Enums;
using System.Collections.Generic;
using CardGame.Core.Commands.Implementations;
using CardGame.Core.Cards.Data;
using System.IO;
using System.Text;

public class CombatSystemTests
{
    private void MockLoadLibrary()
    {
        // We create a JSON string representing the minimal cards needed for the tests
        string mockJson = @"
        [
            {
                ""Id"": 1,
                ""Name"": ""Vanilla Unit"",
                ""Type"": ""Unit"",
                ""Cost"": 1,
                ""Attack"": 1,
                ""Health"": 5,
                ""Keywords"": []
            },
            {
                ""Id"": 26,
                ""Name"": ""Occultist"",
                ""Type"": ""Unit"",
                ""Cost"": 4,
                ""Attack"": 2,
                ""Health"": 4,
                ""Effects"": [
                    {
                        ""Trigger"": ""OnPreCombatLine"",
                        ""Targeting"": ""TargetEnemyUnit"",
                        ""Actions"": [
                            { ""Type"": ""ApplyStatus"", ""Target"": ""TargetEnemyUnit"", ""StatusKeyword"": ""Marked"" }
                        ]
                    }
                ]
            }
        ]";

        // Write to a temp file and load it
        string tempPath = Path.GetTempFileName();
        File.WriteAllText(tempPath, mockJson);
        CardLibrary.Instance.Clear();
        CardLibrary.Instance.LoadFromJson(tempPath);
        File.Delete(tempPath);
    }

    [Fact]
    public void SequentialCombat_FirstLaneLethal_ShouldEndGameImmediately()
    {
        MockLoadLibrary();
        var engine = SetupBasicEngine();
        var state = engine.CurrentState;

        // Player 1: Lethal unit in Lane 0
        var p1Unit = engine.Factory.CreateCard(1, 1);
        p1Unit = p1Unit.AddPermanentBuff(new CardGame.Core.Cards.Models.CardStats(20, 5, 0));

        // Player 2: Lethal unit in Lane 1
        var p2Unit = engine.Factory.CreateCard(1, 2);
        p2Unit = p2Unit.AddPermanentBuff(new CardGame.Core.Cards.Models.CardStats(20, 5, 0));

        state = state.UpdateBoard(state.Board
            .WithUnitPlacedAt(0, 1, p1Unit)
            .WithUnitPlacedAt(1, 2, p2Unit));

        state = state.With(currentPhase: GamePhase.Combat);
        var orchestrator = new CombatOrchestrator();
        var context = new GameContext(engine.Factory, engine.Rng, engine.Events);

        // ACT
        var finalState = orchestrator.ResolveCombatPhase(state, engine.Events, context);

        // ASSERT
        // Player B should be dead from Lane 0
        Assert.True(finalState.PlayerB.Health <= 0);
        // Player A should still be alive because Lane 1 never fired
        Assert.Equal(20, finalState.PlayerA.Health);
    }

    [Fact]
    public void OccultistTrigger_ShouldPauseCombat_AndResumeCorrectly()
    {
        MockLoadLibrary();
        var engine = SetupBasicEngine();
        var context = new GameContext(engine.Factory, engine.Rng, engine.Events);
        var orchestrator = new CombatOrchestrator();

        var occultist = engine.Factory.CreateCard(26, 1); // 2 Atk
        var enemy1 = engine.Factory.CreateCard(1, 2);    // 5 HP
        var enemy2 = engine.Factory.CreateCard(1, 2);

        var state = engine.CurrentState.UpdateBoard(engine.CurrentState.Board
            .WithUnitPlacedAt(0, 1, occultist)
            .WithUnitPlacedAt(0, 2, enemy1)
            .WithUnitPlacedAt(1, 2, enemy2));

        // Ensure we start at the beginning
        state = state.With(currentPhase: GamePhase.Combat, combatLineIndex: 0, combatStep: 0);

        // ACT 1: Occultist pauses the game
        var pausedState = orchestrator.ResolveCombatPhase(state, engine.Events, context);
        Assert.NotNull(pausedState.PendingInteraction);

        // ACT 2: Select target and resume
        var selectCmd = new SelectTargetCommand(1, enemy1.InstanceId);
        var resumedState = selectCmd.Execute(pausedState, engine.Events, context);
        var finalState = orchestrator.ResolveCombatPhase(resumedState, engine.Events, context);

        // ASSERT
        Assert.Null(finalState.PendingInteraction);
        Assert.Equal(4, finalState.CombatLineIndex);

        // Check health: 5 HP - (2 Atk * 2 for Marked) = 1 HP
        var unitInLane0 = finalState.Board.Lines[0].Player2Unit;
        Assert.NotNull(unitInLane0);

        // THE FIX: Change 3 to 1
        Assert.Equal(1, unitInLane0.CurrentStats.Health);
    }

    private GameEngine SetupBasicEngine()
    {
        var initialState = GameState.Initial(1, new List<CardGame.Core.Cards.Models.CardInstance>(), new List<CardGame.Core.Cards.Models.CardInstance>(), new DeterministicRng(123));
        return new GameEngine(initialState, 123);
    }
}