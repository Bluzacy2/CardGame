using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CardGame.Core.Application;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Factories;
using CardGame.Core.Cards.Models;
using CardGame.Core.State.Models;
using CardGame.Core.State.Enums;
using CardGame.Core.Commands.Implementations;
using CardGame.Core.Commands.Interfaces; // Naprawia CS0246
using CardGame.Core.AI;
using CardGame.Core.AI.Strategies;

public partial class GameBootstrap : Node2D
{
    [ExportGroup("Kontenery")]
    [Export] public Control HandContainer;
    [Export] public Control BoardContainer;
    [Export] public Control EnemyHandContainer;

    [ExportGroup("UI Elementy")]
    [Export] public Button EndTurnButton;
    [Export] public Label PlayerHpLabel;
    [Export] public Label PlayerManaLabel;
    [Export] public Label EnemyHpLabel;
    [Export] public Label EnemyManaLabel;

    [ExportGroup("Szablony")]
    [Export] public PackedScene CardSceneTemplate;
    [Export] public PackedScene LineSceneTemplate;

    private GameEngine _engine;
    private int _playerId = 1; // Ty
    private int _botId = 2;    // Bot
    private AIPlayerController _botController;
    private GameState _lastRenderedState;
    private bool _isBotThinking = false;

    public override void _Ready()
    {
        InitializeGame();

        if (EndTurnButton != null)
        {
            if (!EndTurnButton.IsConnected(Button.SignalName.Pressed, Callable.From(OnEndTurnPressed)))
                EndTurnButton.Pressed += OnEndTurnPressed;
        }

        UpdateUI();
    }

    public override void _Process(double delta)
    {
        if (_engine == null || _engine.IsGameOver) return;

        // 1. Aktualizacja UI jeśli stan się zmienił
        if (_engine.CurrentState != _lastRenderedState)
        {
            UpdateUI();
        }

        // 2. Obsługa Bota
        if (_engine.CurrentState.ActivePlayerId == _botId && !_isBotThinking)
        {
            RunBotTurn();
        }
    }

    private async void RunBotTurn()
    {
        _isBotThinking = true;

        // Blokada przycisku
        if (EndTurnButton != null) EndTurnButton.Disabled = true;

        await Task.Delay(800);

        // Obliczenia w tle
        await Task.Run(() =>
        {
            var moves = _botController.BeamSolver.FindBestMoves(_engine.CurrentState);

            if (moves.Any())
            {
                var best = moves.First();
                // --- POPRAWKA: Wywołanie .CallDeferred() NA obiekcie Callable ---
                Callable.From(() => ExecuteBotCommand(best.Command)).CallDeferred();
            }
            else
            {
                // Koniec tury, jeśli brak ruchów
                Callable.From(() => ExecuteBotCommand(new EndPhaseCommand(_botId))).CallDeferred();
            }
        });
    }

    // Metoda wykonująca ruch na wątku głównym
    private void ExecuteBotCommand(IGameCommand command)
    {
        GD.Print($"[BOT] Wykonuje: {command.GetType().Name}");
        _engine.ExecuteCommand(command);

        // Jeśli bot nie skończył tury (zagrał jednostkę), pozwalamy mu myśleć dalej w następnej klatce
        if (!(command is EndPhaseCommand))
        {
            _isBotThinking = false;
        }
        else
        {
            // Bot skończył, tura gracza
            _isBotThinking = false;
            if (EndTurnButton != null) EndTurnButton.Disabled = false;
        }

        UpdateUI();
    }

    private void InitializeGame()
    {
        string jsonPath = ProjectSettings.GlobalizePath("res://Data/Cards/cards.json");
        try { CardLibrary.Instance.LoadFromJson(jsonPath); }
        catch (Exception e) { GD.PrintErr("BŁĄD JSON: " + e.Message); return; }

        var rng = new DeterministicRng(new Random().Next());
        var factory = new CardFactory(CardLibrary.Instance, rng);

        var deck1 = CreateDeck(factory, 1);
        var deck2 = CreateDeck(factory, 2);

        var state = GameState.Initial(1, deck1, deck2, rng)
                             .With(currentPhase: GamePhase.UnitOnly);

        _engine = new GameEngine(state, rng.Seed);

        // Inicjalizacja bota (Beam Search jest szybszy i bezpieczniejszy dla UI na start)
        _botController = new AIPlayerController(_engine, _botId, new StandardStrategy(), AISolverType.BeamSearch);

        // Generowanie linii (zabezpieczenie)
        if (BoardContainer != null && BoardContainer.GetChildCount() == 0 && LineSceneTemplate != null)
        {
            for (int i = 0; i < 4; i++)
            {
                var line = LineSceneTemplate.Instantiate<LineView>();
                BoardContainer.AddChild(line);
            }
        }
        ConnectLines();
    }

    private void ConnectLines()
    {
        if (BoardContainer == null) return;
        foreach (var child in BoardContainer.GetChildren())
        {
            if (child is LineView lineView)
            {
                if (!lineView.IsConnected(LineView.SignalName.CardDroppedOnLine, Callable.From<int, int>(OnCardDropped)))
                    lineView.CardDroppedOnLine += OnCardDropped;
            }
        }
    }

    private void OnCardDropped(int cardId, int lineIdx)
    {
        if (_engine.CurrentState.ActivePlayerId != _playerId) return;

        var command = new PlayUnitCommand(_playerId, cardId, lineIdx);
        _engine.ExecuteCommand(command);
        UpdateUI();
    }

    private void OnEndTurnPressed()
    {
        if (_engine.CurrentState.ActivePlayerId == _playerId)
        {
            _engine.ExecuteCommand(new EndPhaseCommand(_playerId));
            UpdateUI();
        }
    }

    private void UpdateUI()
    {
        if (_engine == null || HandContainer == null || BoardContainer == null) return;
        _lastRenderedState = _engine.CurrentState;

        var p1 = _engine.CurrentState.PlayerA;
        var p2 = _engine.CurrentState.PlayerB;

        // Statystyki
        if (PlayerHpLabel != null) PlayerHpLabel.Text = $"HP: {p1.Health}";
        if (PlayerManaLabel != null) PlayerManaLabel.Text = $"Krew: {p1.CurrentBlood}/{p1.MaxBlood}";
        if (EnemyHpLabel != null) EnemyHpLabel.Text = $"Wróg HP: {p2.Health}";
        if (EnemyManaLabel != null) EnemyManaLabel.Text = $"Krew: {p2.CurrentBlood}/{p2.MaxBlood}";

        // Ręka Gracza
        foreach (Node child in HandContainer.GetChildren()) child.QueueFree();
        foreach (var card in p1.Hand)
        {
            var cardVis = CardSceneTemplate.Instantiate<CardView>();
            HandContainer.AddChild(cardVis);
            cardVis.Render(card);

            // Wizualizacja dostępności
            if (card.CurrentStats.BloodCost > p1.CurrentBlood || _engine.CurrentState.ActivePlayerId != _playerId)
                cardVis.Modulate = new Color(0.5f, 0.5f, 0.5f); // Przyciemnij
            else
                cardVis.Modulate = new Color(1, 1, 1); // Normalny
        }

        // Ręka Wroga (Rewersy)
        if (EnemyHandContainer != null)
        {
            foreach (Node child in EnemyHandContainer.GetChildren()) child.QueueFree();
            for (int i = 0; i < p2.Hand.Count; i++)
            {
                var cardBack = CardSceneTemplate.Instantiate<CardView>();
                EnemyHandContainer.AddChild(cardBack);
                cardBack.RenderCardBack();
                cardBack.Scale = new Vector2(0.6f, 0.6f);
            }
        }

        // Stół
        int lineIdx = 0;
        foreach (Node child in BoardContainer.GetChildren())
        {
            if (child is LineView lineView)
            {
                if (lineIdx < _engine.CurrentState.Board.Lines.Count)
                    lineView.Render(_engine.CurrentState.Board.Lines[lineIdx], CardSceneTemplate);
                lineIdx++;
            }
        }
    }

    private List<CardInstance> CreateDeck(CardFactory f, int ownerId)
    {
        var list = new List<CardInstance>();
        int[] ids = { 1, 2, 4, 8, 1, 2, 8, 7, 3, 2, 1, 4 };
        foreach (var id in ids) list.Add(f.CreateCard(id, ownerId));
        return list;
    }
}