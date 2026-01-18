using Xunit;
using CardGame.Core.Application;
using CardGame.Core.State.Models;
using CardGame.Core.Events;
using CardGame.Core.State.Enums;
using CardGame.Core.Cards.Data;
using CardGame.Core.GameRules.Death;
using System.Collections.Generic;
using System.Linq;
using System.IO;

public class TrapSystemTests
{
    private void MockLoadLibraryForTrap()
    {
        // Updated Trap JSON with the new 'OppositeEnemyUnit' target
        string mockJson = @"
        [
            {
                ""Id"": 1,
                ""Name"": ""Target Dummy"",
                ""Type"": ""Unit"",
                ""Cost"": 1,
                ""Attack"": 1,
                ""Health"": 5,
                ""Keywords"": []
            },
            {
                ""Id"": 903,
                ""Name"": ""Trap"",
                ""Type"": ""Unit"",
                ""Cost"": 0,
                ""Attack"": 0,
                ""Health"": 1,
                ""Effects"": [
                    {
                        ""Trigger"": ""OnDeath"",
                        ""Actions"": [
                            { 
                                ""Type"": ""ApplyStatus"", 
                                ""Target"": ""OppositeEnemyUnit"", 
                                ""StatusKeyword"": ""Stunned"" 
                            }
                        ]
                    }
                ]
            }
        ]";

        string tempPath = Path.GetTempFileName();
        File.WriteAllText(tempPath, mockJson);
        CardLibrary.Instance.Clear();
        CardLibrary.Instance.LoadFromJson(tempPath);
        File.Delete(tempPath);
    }

    [Fact]
    public void TrapDeath_ShouldStunUnitInSameLane_EvenAfterTrapIsRemoved()
    {
        // --- SETUP ---
        MockLoadLibraryForTrap();
        var initialState = GameState.Initial(1, new List<CardGame.Core.Cards.Models.CardInstance>(), new List<CardGame.Core.Cards.Models.CardInstance>(), new DeterministicRng(1));
        var engine = new GameEngine(initialState, 1);
        var context = new GameContext(engine.Factory, engine.Rng, engine.Events);

        // Place Trap in Lane 2 for Player 1
        var trap = engine.Factory.CreateCard(903, 1);
        // Place a victim in Lane 2 for Player 2
        var victim = engine.Factory.CreateCard(1, 2);

        var state = engine.CurrentState.UpdateBoard(engine.CurrentState.Board
            .WithUnitPlacedAt(2, 1, trap)
            .WithUnitPlacedAt(2, 2, victim));

        // --- ACT ---
        // 1. Deal lethal damage to the trap
        var trapInLane = state.Board.Lines[2].Player1Unit;
        var damagedState = state.UpdateBoard(state.Board.UpdateUnit(trapInLane.TakeDamage(10)));

        // 2. Resolve Death
        // This will move Trap to Graveyard, then fire the OnDeath trigger
        var deathResolver = new DeathResolver();
        var finalState = deathResolver.ResolveDeaths(damagedState, engine.Events, context);

        // We must also process triggers because DeathResolver publishes the event, 
        // but the TriggerSystem executes the effect.
        finalState = new CardGame.Core.Events.Triggers.TriggerSystem().ProcessEvents(finalState, engine.Events, context);

        // --- ASSERT ---
        // 1. The Trap should be gone from the board
        Assert.Null(finalState.Board.Lines[2].Player1Unit);

        // 2. The Trap should be in the discard pile
        Assert.Contains(finalState.PlayerA.DiscardPile, c => c.Definition.Id == "903");

        // 3. The Victim should still be there but STUNNED
        var finalVictim = finalState.Board.Lines[2].Player2Unit;
        Assert.NotNull(finalVictim);
        Assert.Contains(CardGame.Core.Cards.Data.Keyword.Stunned, finalVictim.CurrentStats.Keywords);
    }
}