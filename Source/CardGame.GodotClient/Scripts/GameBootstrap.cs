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
using CardGame.Core.Decks.Data;
/// <summary>
/// The main entry point and orchestrator for the gameplay scene in Godot.
/// Responsible for initializing the core GameEngine, connecting UI and Input systems,
/// and managing the main game loop (updates, turns, and game over states).
/// </summary>
public partial class GameBootstrap : Node2D
{
    /// <summary>
    /// Reference to the UIManager which handles all visual representations.
    /// Must be assigned in the Godot Editor inspector.
    /// </summary>
    [Export] public UIManager UI;
    /// <summary>
    /// The core logic engine of the card game (platform-independent).
    /// </summary>
    private GameEngine _engine;
	private int _playerId = 1;
	private int _botId = 2;
    /// <summary>
    /// Stores the last processed game state to prevent redundant UI updates.
    /// </summary>
    private GameState _lastRenderedState;
    /// <summary>
    /// Handles player input (mouse clicks, drag & drop) and converts them into GameCommands.
    /// </summary>
    private InputController _inputController;
    /// <summary>
    /// Manages the AI thread and execution flow.
    /// </summary>
    private BotCoordinator _botCoordinator;
    /// <summary>
    /// Called when the node enters the scene tree for the first time.
    /// Validates dependencies and starts the game initialization process.
    /// </summary>
    public override void _Ready()
	{
		if (UI == null) { GD.PrintErr("Brak UI Managera!"); return; }

		InitializeGame();
	}
    /// <summary>
    /// Called every frame. Manages the game loop flow.
    /// 1. Checks for Game Over condition.
    /// 2. Updates InputController state (for targeting/interactions).
    /// 3. Updates UI if the game state has changed.
    /// 4. Triggers AI turn if active.
    /// </summary>
    /// <param name="delta">Time elapsed since the last frame.</param>
    public override void _Process(double delta)
	{
		if (_engine == null) return;

        // 1. Check Game Over
        if (_engine.IsGameOver)
		{
			UI.ShowGameOverScreen(_engine.WinnerId, _playerId, _botId);
			return;
		}
        // 2. Update Input Controller State
        if (_inputController != null)
		{
			_inputController.UpdateState(_engine.CurrentState);
		}
        // 3. Update Visuals
        if (_engine.CurrentState != _lastRenderedState)
		{
			UI.UpdateDisplay(_engine.CurrentState, _playerId);
			_lastRenderedState = _engine.CurrentState;
		}

        // 4. Handle Bot Turn
        if (_engine.CurrentState.ActivePlayerId == _botId && !_botCoordinator.IsThinking)
		{
			_botCoordinator.PlayTurn();
		}
	}
    /// <summary>
    /// Initializes the Card Library, RNG, Decks, and the core GameEngine.
    /// Handles loading decks from the global GameSession or creates fallback decks for debugging.
    /// </summary>
    private void InitializeGame()
	{
        // 1. Load Card Database
        string jsonPath = ProjectSettings.GlobalizePath("res://Data/Cards/cards.json");
		try { CardLibrary.Instance.LoadFromJson(jsonPath); } catch { }

		var rng = new DeterministicRng(new Random().Next());
		var factory = new CardFactory(CardLibrary.Instance, rng);

        // 2. Load Decks (from Session or Fallback)

        List<CardInstance> d1 = new List<CardInstance>();
		List<CardInstance> d2 = new List<CardInstance>();
        // Attempt to load selected decks from the main menu selection
        if (GameSession.Instance != null && GameSession.Instance.SelectedPlayerDeck != null)
		{
			GD.Print($"[BOOTSTRAP] Ładowanie wybranej talii: {GameSession.Instance.SelectedPlayerDeck.Name}");
			d1 = LoadDeckFromData(GameSession.Instance.SelectedPlayerDeck, _playerId, factory);

			if (GameSession.Instance.SelectedBotDeck != null)
			{
				d2 = LoadDeckFromData(GameSession.Instance.SelectedBotDeck, _botId, factory);
			}
		}
		else
		{
            // Fallback for direct scene launch (Debug)
            GD.Print("[BOOTSTRAP] Brak danych w sesji (Debug Mode?). Ładowanie ostatniej talii z dysku.");
			var playerDeckData = GetLatestPlayerDeck();
			d1 = LoadDeckFromData(playerDeckData, _playerId, factory);
		}

        // Safeguards: Create random decks if loading failed
        if (d1.Count == 0) d1 = CreateRandomDeck(factory, _playerId);

		if (d2.Count == 0)
		{
			var botDeckData = GetRandomBotDeck();
			d2 = LoadDeckFromData(botDeckData, _botId, factory);
			if (d2.Count == 0) d2 = CreateRandomDeck(factory, _botId);
		}

        // 3. Start Engine
        var state = GameState.Initial(1, d1, d2, rng);
		_engine = new GameEngine(state, rng.Seed);

		SetupSubsystems();

        // 4. Handle Initial Mulligan Phase
        if (_engine.CurrentState.CurrentPhase == GamePhase.Mulligan)
		{
			_inputController.StartMulligan(_engine.CurrentState.PlayerA.Hand.ToList());
			_engine.ExecuteCommand(new ConfirmMulliganCommand(_botId, new List<int>()));
		}
	}
    /// <summary>
    /// Instantiates and configures helper subsystems (InputController, BotCoordinator)
    /// and binds UI events to game commands.
    /// </summary>
    private void SetupSubsystems()
	{
        // A. Input System
        _inputController = new InputController();
		AddChild(_inputController);
		_inputController.Initialize(_playerId, UI);
        // Listen for commands generated by player input
        _inputController.OnPlayerCommand += ExecutePlayerCommand;
        // UI Event Binding (Interaction -> Controller)
        UI.OnCardClicked += (view) => _inputController.HandleCardClick(view.MyCardData);
		UI.OnCancelSpellClicked += _inputController.CancelTargeting;
		UI.OnConfirmMulliganClicked += _inputController.ConfirmMulligan;

		ConnectLinesSignals();

        // B. Bot System
        // Initialize AI Controller with BeamSearch strategy
        var aiCtrl = new AIPlayerController(_engine, _botId, new StandardStrategy(), AISolverType.BeamSearch);
		_botCoordinator = new BotCoordinator();
		AddChild(_botCoordinator);
		_botCoordinator.Initialize(_engine, _botId, aiCtrl);
		_botCoordinator.OnBotCommand += ExecuteBotCommand;

        // C. General UI Buttons
        UI.OnEndTurnClicked += () => ExecutePlayerCommand(new EndPhaseCommand(_playerId));
		UI.OnRestartClicked += () => GetTree().ReloadCurrentScene();
	}
    /// <summary>
    /// Executes a command initiated by the local human player.
    /// Handles validation feedback (e.g., visual shake or error message) if the move is invalid.
    /// </summary>
    /// <param name="cmd">The command to execute.</param>
    private void ExecutePlayerCommand(IGameCommand cmd)
	{
		var stateBefore = _engine.CurrentState;
		var result = _engine.ExecuteCommand(cmd);
        // Validation Feedback: If the state didn't change (and it wasn't an EndPhase command), the move was invalid.
        if (cmd.PlayerId == _playerId && result.NewState == stateBefore && !(cmd is EndPhaseCommand))
		{
			UI.ShowFloatingText("Nie można!", GetViewportRect().Size / 2, Colors.Red);
		}
		else
		{
            // Visualize events (damage numbers, death animations)
            UI.HandleVisualEvents(result.EventsHappened, _playerId);
		}

		UI.UpdateDisplay(_engine.CurrentState, _playerId);
		_lastRenderedState = _engine.CurrentState;
	}
    /// <summary>
    /// Executes a command initiated by the AI bot.
    /// </summary>
    /// <param name="cmd">The command generated by the AI.</param>
    private void ExecuteBotCommand(IGameCommand cmd)
	{
        // GD.Print($"[BOT] Executing: {cmd.GetType().Name}");
        GD.Print($"[BOT] Wykonuje: {cmd.GetType().Name}");
		var result = _engine.ExecuteCommand(cmd);
		UI.HandleVisualEvents(result.EventsHappened, _playerId);
		UI.UpdateDisplay(_engine.CurrentState, _playerId);
	}
    /// <summary>
    /// Connects drag-and-drop signals from all board lines to the InputController.
    /// </summary>
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
    /// <summary>
    /// Handler for when a card is dropped onto a board line.
    /// </summary>
    /// <param name="cardId">Instance ID of the dropped card.</param>
    /// <param name="lineIdx">Index of the target line.</param>
    private void OnCardDropped(int cardId, int lineIdx)
	{
		var card = _engine.CurrentState.PlayerA.Hand.FirstOrDefault(c => c.InstanceId == cardId);
		if (card != null) _inputController.HandleCardDrop(card, lineIdx);
	}
    /// <summary>
    /// Creates a completely random deck for testing purposes.
    /// </summary>
    private List<CardInstance> CreateRandomDeck(CardFactory f, int o)
	{
		var l = new List<CardInstance>();
		int[] ids = { 1, 2, 4, 8, 7, 3, 2, 1, 4 };
		var r = new Random();
		for (int i = 0; i < 30; i++) l.Add(f.CreateCard(ids[r.Next(ids.Length)], o));
		return l;
	}
    /// <summary>
    /// Finds the most recently modified deck file in the Decks folder.
    /// </summary>
    private DeckData GetLatestPlayerDeck()
	{
		string path = ProjectSettings.GlobalizePath("res://Data/Decks/");
		if (!System.IO.Directory.Exists(path)) return null;

		var directory = new System.IO.DirectoryInfo(path);
		return directory.GetFiles("*.json")
			.OrderByDescending(f => f.LastWriteTime)
			.Select(f => LoadDeckFromFile(f.FullName))
			.FirstOrDefault(d => d != null);
	}
    /// <summary>
    /// Selects a random deck for the bot from either BotDecks or player Decks folder.
    /// </summary>
    private DeckData GetRandomBotDeck()
	{
		string path = ProjectSettings.GlobalizePath("res://Data/BotDecks/");
		if (!System.IO.Directory.Exists(path)) path = ProjectSettings.GlobalizePath("res://Data/Decks/");
		if (!System.IO.Directory.Exists(path)) return null;

		var directory = new System.IO.DirectoryInfo(path);
		var files = directory.GetFiles("*.json");
		if (files.Length == 0) return null;

		var randomFile = files[new Random().Next(files.Length)];
		return LoadDeckFromFile(randomFile.FullName);
	}
    /// <summary>
    /// Deserializes a deck JSON file into a DeckData object.
    /// </summary>
    private DeckData LoadDeckFromFile(string fullPath)
	{
		try
		{
			string json = System.IO.File.ReadAllText(fullPath);
			return System.Text.Json.JsonSerializer.Deserialize<DeckData>(json);
		}
		catch { return null; }
	}
    /// <summary>
    /// Converts a DeckData object (list of IDs) into a list of playable CardInstance objects using the CardFactory.
    /// </summary>
    private List<CardInstance> LoadDeckFromData(DeckData data, int ownerId, CardFactory factory)
	{
		var instances = new List<CardInstance>();
		if (data == null || data.CardIds == null) return instances;

		foreach (int id in data.CardIds)
		{
			try
			{
				instances.Add(factory.CreateCard(id, ownerId));
			}
			catch
			{
				GD.PrintErr($"[DECK] Karta ID {id} nie istnieje w cards.json!");
			}
		}
		return instances;
	}
}
