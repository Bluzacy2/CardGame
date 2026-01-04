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
using CardGame.Core.Commands.Interfaces;
using CardGame.Core.AI;
using CardGame.Core.AI.Strategies;
using CardGame.Core.Decks;
using CardGame.Core.Events;

public partial class GameBootstrap : Node2D
{
    [Export] public UIManager UI;

    private GameEngine _engine;
    private int _playerId = 1;
    private int _botId = 2;
    private GameState _lastRenderedState;

    private InputController _inputController;
    private BotCoordinator _botCoordinator;

    public override void _Ready()
    {
        if (UI == null) { GD.PrintErr("Brak UI Managera!"); return; }

        InitializeGame();
    }

    public override void _Process(double delta)
    {
        if (_engine == null) return;

        // 1. Game Over
        if (_engine.IsGameOver)
        {
            UI.ShowGameOverScreen(_engine.WinnerId, _playerId, _botId);
            return;
        }

        // 2. Aktualizacja UI
        if (_engine.CurrentState != _lastRenderedState)
        {
            UI.UpdateDisplay(_engine.CurrentState, _playerId);
            _lastRenderedState = _engine.CurrentState;
        }

        // 3. Tura Bota
        if (_engine.CurrentState.ActivePlayerId == _botId && !_botCoordinator.IsThinking)
        {
            _botCoordinator.PlayTurn();
        }
    }

    private void InitializeGame()
    {
        string jsonPath = ProjectSettings.GlobalizePath("res://Data/Cards/cards.json");
        try { CardLibrary.Instance.LoadFromJson(jsonPath); } catch { }

        var rng = new DeterministicRng(new Random().Next());
        var factory = new CardFactory(CardLibrary.Instance, rng);
        var d1 = CreateRandomDeck(factory, 1);
        var d2 = CreateRandomDeck(factory, 2);

        // WAŻNE: Initial ustawia fazę Mulligan
        var state = GameState.Initial(1, d1, d2, rng);
        _engine = new GameEngine(state, rng.Seed);

        SetupSubsystems();

        if (_engine.CurrentState.CurrentPhase == GamePhase.Mulligan)
        {
            _inputController.StartMulligan(_engine.CurrentState.PlayerA.Hand.ToList());
            // Bot auto-confirm
            _engine.ExecuteCommand(new ConfirmMulliganCommand(_botId, new List<int>()));
        }
        else
        {
            UI.UpdateDisplay(_engine.CurrentState, _playerId);
            UI.ShowBigMessage("START GRY", 2.0f, Colors.White);
        }
    }

    private void SetupSubsystems()
    {
        // A. Input
        _inputController = new InputController();
        AddChild(_inputController);
        _inputController.Initialize(_playerId, UI);
        _inputController.OnPlayerCommand += ExecutePlayerCommand;

        UI.OnCardClicked += (view) => _inputController.HandleCardClick(view.MyCardData);
        UI.OnCancelSpellClicked += _inputController.CancelTargeting;
        UI.OnConfirmMulliganClicked += _inputController.ConfirmMulligan;

        ConnectLinesSignals();

        // B. Bot
        var aiCtrl = new AIPlayerController(_engine, _botId, new StandardStrategy(), AISolverType.BeamSearch);
        _botCoordinator = new BotCoordinator();
        AddChild(_botCoordinator);
        _botCoordinator.Initialize(_engine, _botId, aiCtrl);
        _botCoordinator.OnBotCommand += ExecuteBotCommand;

        // C. UI Buttons
        UI.OnEndTurnClicked += () => ExecutePlayerCommand(new EndPhaseCommand(_playerId));
        UI.OnRestartClicked += () => GetTree().ReloadCurrentScene();
    }

    // --- BRAKUJĄCE METODY ---

    private void ExecutePlayerCommand(IGameCommand cmd)
    {
        var stateBefore = _engine.CurrentState;
        var result = _engine.ExecuteCommand(cmd);

        if (cmd.PlayerId == _playerId && result.NewState == stateBefore && !(cmd is EndPhaseCommand))
        {
            UI.ShowFloatingText("Nie można!", GetViewportRect().Size / 2, Colors.Red);
        }
        else
        {
            UI.HandleVisualEvents(result.EventsHappened, _playerId);
        }

        UI.UpdateDisplay(_engine.CurrentState, _playerId);
        _lastRenderedState = _engine.CurrentState;
    }

    private void ExecuteBotCommand(IGameCommand cmd)
    {
        GD.Print($"[BOT] Wykonuje: {cmd.GetType().Name}");
        var result = _engine.ExecuteCommand(cmd);
        UI.HandleVisualEvents(result.EventsHappened, _playerId);
        UI.UpdateDisplay(_engine.CurrentState, _playerId);

        // BotCoordinator sam resetuje IsThinking, ale UI musi wiedzieć czy odblokować tury
        // Tu logika jest uproszczona, bo BotCoordinator robi to sekwencyjnie
    }

    private void ConnectLinesSignals()
    {
        if (UI.BoardContainer == null) return;
        foreach (Node child in UI.BoardContainer.GetChildren())
        {
            if (child is LineView lv)
            {
                if (!lv.IsConnected(LineView.SignalName.CardDroppedOnLine, Callable.From<int, int>(OnCardDropped)))
                    lv.CardDroppedOnLine += OnCardDropped;
            }
        }
    }

    private void OnCardDropped(int cardId, int lineIdx)
    {
        var card = _engine.CurrentState.PlayerA.Hand.FirstOrDefault(c => c.InstanceId == cardId);
        if (card != null) _inputController.HandleCardDrop(card, lineIdx);
    }

    private List<CardInstance> CreateRandomDeck(CardFactory f, int o)
    {
        var l = new List<CardInstance>();
        int[] ids = { 1, 2, 4, 8, 7, 3, 2, 1, 4 };
        var r = new Random();
        for (int i = 0; i < 30; i++) l.Add(f.CreateCard(ids[r.Next(ids.Length)], o));
        return l;
    }
}