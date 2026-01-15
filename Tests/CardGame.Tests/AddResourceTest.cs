using CardGame.Core.Application;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Factories;
using CardGame.Core.Cards.Models;
using CardGame.Core.Commands.Implementations;
using CardGame.Core.State.Enums;
using CardGame.Core.State.Models;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Xunit;

public class SacrificeBugTests
{
    private void SetupLibrary()
    {
        // We simulate the JSON data to populate the Library singleton
        var mockCards = new List<CardData>
        {
            new CardData {
                Id = 40, Name = "Widows Spider", Type = CardType.Unit,
                Cost = 1, Attack = 2, Health = 2,
                Effects = new List<EffectData> {
                    new EffectData {
                        Trigger = TriggerType.OnSacrificed,
                        Actions = new List<ActionData> {
                            new ActionData { Type = ActionType.AddResource, Target = TargetType.FriendlyHero, Amount = 1 }
                        }
                    }
                }
            },
            new CardData {
                Id = 100, Name = "Sacrificer", Type = CardType.Unit,
                Cost = 2, Attack = 2, Health = 2,
                Effects = new List<EffectData> {
                    new EffectData {
                        Trigger = TriggerType.OnPlayed,
                        Targeting = TargetType.TargetFriendlyUnit,
                        Actions = new List<ActionData> {
                            new ActionData { Type = ActionType.SacrificeUnit, Target = TargetType.SelectedTarget }
                        }
                    }
                }
            }
        };

        // Trick the library into loading our mock data
        string json = JsonSerializer.Serialize(mockCards);
        string path = "temp_cards.json";
        System.IO.File.WriteAllText(path, json);
        CardLibrary.Instance.Clear();
        CardLibrary.Instance.LoadFromJson(path);
    }

    [Fact]
    public void Test_Turn2_Sacrifice_ResourceGain()
    {
        SetupLibrary();
        var rng = new DeterministicRng(123);
        var factory = new CardFactory(CardLibrary.Instance, rng);

        // 1. Setup Player 1 at Turn 2 (2/2 Blood)
        var spider = factory.CreateCard(40, 1);
        var sacrificer = factory.CreateCard(100, 1);

        var p1 = new PlayerState(1, 20, 2, 2,
            new List<CardInstance> { sacrificer },
            new List<CardInstance>(),
            new List<CardInstance>());

        var state = new GameState(
            turnNumber: 2,
            currentPhase: GamePhase.UnitAndAction,
            activePlayerId: 1,
            board: BoardState.Empty().WithUnitPlacedAt(0, 1, spider),
            playerA: p1,
            playerB: PlayerState.Initial(2, new List<CardInstance>()),
            roundStartingPlayerId: 1
        );

        var engine = new GameEngine(state, 123);

        // 2. Play Sacrificer (Cost 2)
        // This command will now fully resolve because there is only one spider to sacrifice.
        engine.ExecuteCommand(new PlayUnitCommand(1, sacrificer.InstanceId, 1));

        // 3. VERIFY
        // Check that the spider is actually in the discard pile
        bool spiderInDiscard = engine.CurrentState.PlayerA.DiscardPile.Any(c => c.Definition.Id == "40");
        Assert.True(spiderInDiscard, "Spider should be dead and in the discard pile.");

        // Check Blood logic:
        // Started with 2.
        // Paid 2 for the Sacrificer -> 0.
        // Spider sacrificed -> +1.
        // EXPECTED RESULT: 1
        int finalBlood = engine.CurrentState.PlayerA.CurrentBlood;

        Assert.Equal(1, finalBlood);
    }
}