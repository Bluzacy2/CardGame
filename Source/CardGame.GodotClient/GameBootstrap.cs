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
using CardGame.Core.Cards.Logic;
using CardGame.Core.Events.Interfaces;
using CardGame.Core.Decks;
using CardGame.Core.Events;

public partial class GameBootstrap : Node2D
{
    [ExportGroup("Kontenery")]
    [Export] public Control HandContainer;
    [Export] public Control BoardContainer;
    [Export] public Control EnemyHandContainer;
    [Export] public Control VFXContainer;

    [ExportGroup("UI Elementy")]
    [Export] public Button EndTurnButton;
    [Export] public Label NotificationLabel;
    [Export] public Line2D TargetingArrow;
    [Export] public Label PlayerHpLabel;
    [Export] public Label PlayerManaLabel;
    [Export] public Label EnemyHpLabel;
    [Export] public Label EnemyManaLabel;
    [Export] public Button CancelSpellButton;

    [ExportGroup("System Powiadomień")]
    [Export] public Control NotificationLayer;
    [Export] public Control MessagePanel;
    [Export] public Label MessageLabel;
    [Export] public Button RestartButton;

    [ExportGroup("Szablony")]
    [Export] public PackedScene CardSceneTemplate;
    [Export] public PackedScene LineSceneTemplate;

    private GameEngine _engine;
    private int _playerId = 1; // Ty
    private int _botId = 2;    // Bot
    private AIPlayerController _botController;
    private GameState _lastRenderedState;
    private bool _isBotThinking = false;

    // --- TARGETING ---
    private enum InputState { Normal, TargetingSpell }
    private InputState _inputState = InputState.Normal;
    private CardInstance _spellBeingCast;

    // --- MESSAGE QUEUE ---
    private Queue<(string text, Color color, float duration)> _messageQueue = new Queue<(string, Color, float)>();
    private bool _isMessagePlaying = false;
    private Tween _activeMessageTween;

    public override void _Ready()
    {
        InitializeGame();

        // Konfiguracja UI
        if (NotificationLayer != null)
        {
            NotificationLayer.Visible = true;
            NotificationLayer.MouseFilter = Control.MouseFilterEnum.Ignore;
        }
        if (MessagePanel != null)
        {
            MessagePanel.Visible = false;
            // Ustawiamy Pivot na środek dla ładnego skalowania (jeśli rozmiar jest znany)
            MessagePanel.PivotOffset = MessagePanel.Size / 2;
        }

        if (CancelSpellButton != null)
        {
            CancelSpellButton.Visible = false;
            if (!CancelSpellButton.IsConnected(Button.SignalName.Pressed, Callable.From(CancelTargeting)))
                CancelSpellButton.Pressed += CancelTargeting;
        }

        if (EndTurnButton != null && !EndTurnButton.IsConnected(Button.SignalName.Pressed, Callable.From(OnEndTurnPressed)))
            EndTurnButton.Pressed += OnEndTurnPressed;

        if (RestartButton != null)
        {
            RestartButton.Pressed += () => GetTree().ReloadCurrentScene();
            RestartButton.Visible = false;
        }

        if (TargetingArrow != null) TargetingArrow.Visible = false;

        UpdateUI();
        QueueMessage("START GRY", Colors.White, 2.0f);
    }

    public override void _Process(double delta)
    {
        if (_engine == null) return;

        // 1. Sprawdzenie końca gry
        if (_engine.IsGameOver)
        {
            if (MessagePanel != null && !MessagePanel.Visible && !_isMessagePlaying)
                ShowGameOver();
            return;
        }

        // 2. Rysowanie strzałki
        if (_inputState == InputState.TargetingSpell && TargetingArrow != null)
        {
            TargetingArrow.ClearPoints();
            TargetingArrow.AddPoint(new Vector2(GetViewportRect().Size.X / 2, GetViewportRect().Size.Y - 50));
            TargetingArrow.AddPoint(GetGlobalMousePosition());
        }

        // 3. Wykrywanie zmian stanu
        if (_lastRenderedState == null || _engine.CurrentState != _lastRenderedState)
        {
            if (_lastRenderedState != null)
            {
                CheckGameChanges(_engine.CurrentState, _lastRenderedState);
            }
            UpdateUI();
            _lastRenderedState = _engine.CurrentState;
        }

        // 4. Obsługa Bota
        if (_engine.CurrentState.ActivePlayerId == _botId && !_isBotThinking)
        {
            RunBotTurn();
        }

        // 5. Przetwarzanie Kolejki Komunikatów
        ProcessMessageQueue();
    }

    // --- SYSTEM LOKALIZACJI OBRAŻEŃ ---

    private void HandleVisualEvents(IEnumerable<IGameEvent> events)
    {
        foreach (var e in events)
        {
            if (e is UnitDamagedEvent ude)
            {
                Vector2 spawnPos;
                Color color = Colors.Red;
                string text = $"-{ude.Amount}";

                if (ude.Unit == null)
                {
                    if (_engine.CurrentState.ActivePlayerId == _playerId)
                        spawnPos = new Vector2(GetViewportRect().Size.X / 2, 80);
                    else
                        spawnPos = new Vector2(GetViewportRect().Size.X / 2, GetViewportRect().Size.Y - 150);
                }
                else
                {
                    spawnPos = FindVisualPosition(ude.Unit.InstanceId);
                }

                ShowFloatingText(text, spawnPos, color);
            }
        }
    }

    private Vector2 FindVisualPosition(int cardId)
    {
        Vector2 fallback = GetViewportRect().Size / 2;
        if (BoardContainer == null) return fallback;
        var targetNode = FindCardViewRecursive(BoardContainer, cardId);
        if (targetNode != null)
        {
            return targetNode.GetGlobalRect().GetCenter() + new Vector2(0, -50);
        }
        return fallback;
    }

    private CardView FindCardViewRecursive(Node parent, int cardId)
    {
        foreach (Node child in parent.GetChildren())
        {
            if (child is CardView cv && cv.MyCardData != null && cv.MyCardData.InstanceId == cardId)
                return cv;

            var res = FindCardViewRecursive(child, cardId);
            if (res != null) return res;
        }
        return null;
    }

    // --- INPUT ---

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_inputState == InputState.TargetingSpell)
        {
            if (@event is InputEventMouseButton mb && mb.Pressed)
            {
                if (mb.ButtonIndex == MouseButton.Right) CancelTargeting();
                else if (mb.ButtonIndex == MouseButton.Left) TrySelectTargetAtMouse();
            }
        }
    }

    // --- BOTA ---
    private async void RunBotTurn()
    {
        _isBotThinking = true;

        // Blokujemy przycisk gracza
        if (EndTurnButton != null) EndTurnButton.Disabled = true;

        // --- NOWE: Czekanie na koniec komunikatów ---
        // Dopóki wyświetla się komunikat LUB są jakieś w kolejce -> czekaj.
        while (_isMessagePlaying || _messageQueue.Count > 0)
        {
            await Task.Delay(100); // Sprawdzaj co 100ms
        }
        // --------------------------------------------

        // Dodatkowe opóźnienie "na myślenie" (po zniknięciu napisów)
        await Task.Delay(800);

        await Task.Run(() =>
        {
            var moves = _botController.BeamSolver.FindBestMoves(_engine.CurrentState);

            if (moves.Any())
            {
                var best = moves.First();
                Callable.From(() => ExecuteBotCommand(best.Command)).CallDeferred();
            }
            else
            {
                Callable.From(() => ExecuteBotCommand(new EndPhaseCommand(_botId))).CallDeferred();
            }
        });
    }

    private void ExecuteBotCommand(IGameCommand command)
    {
        _engine.ExecuteCommand(command);
        HandleVisualEvents(_engine.Events.GetHistory());
        if (!(command is EndPhaseCommand)) _isBotThinking = false;
        else { _isBotThinking = false; if (EndTurnButton != null) EndTurnButton.Disabled = false; }
        UpdateUI();
    }

    // --- LOGIKA GRACZA ---

    private void OnCardDropped(int id, int line)
    {
        if (_engine.CurrentState.ActivePlayerId != _playerId) return;
        var c = _engine.CurrentState.PlayerA.Hand.FirstOrDefault(x => x.InstanceId == id);
        if (c == null) return;
        if (c.Definition.Type == CardType.Unit) ExecutePlayerCommand(new PlayUnitCommand(_playerId, id, line));
        else StartTargeting(c);
    }

    public void OnCardClickedInHand(CardView v)
    {
        if (_engine.CurrentState.ActivePlayerId != _playerId) return;
        var c = v.MyCardData; if (c == null) return;
        if (c.Definition.Type == CardType.Spell) StartTargeting(c);
    }

    private void ExecutePlayerCommand(IGameCommand cmd)
    {
        var prev = _engine.CurrentState;
        var res = _engine.ExecuteCommand(cmd);
        if (res.NewState == prev && !(cmd is EndPhaseCommand))
            ShowFloatingText("Nieprawidłowy ruch!", GetViewportRect().Size / 2, Colors.Red);
        else
            HandleVisualEvents(res.EventsHappened);
        UpdateUI();
    }

    private void OnEndTurnPressed() { if (_engine.CurrentState.ActivePlayerId == _playerId) ExecutePlayerCommand(new EndPhaseCommand(_playerId)); }

    // --- CELOWANIE ---

    private bool HasManualTarget(CardInstance card)
    {
        foreach (var effect in card.Definition.Effects)
            foreach (var action in effect.Actions)
                if (action.Target == TargetType.TargetEnemyUnit ||
                    action.Target == TargetType.TargetFriendlyUnit ||
                    action.Target == TargetType.SelectedTarget)
                    return true;
        return false;
    }

    private void StartTargeting(CardInstance card)
    {
        _inputState = InputState.TargetingSpell;
        _spellBeingCast = card;
        if (TargetingArrow != null) TargetingArrow.Visible = true;
        if (CancelSpellButton != null) CancelSpellButton.Visible = true;
        ShowFloatingText("WYBIERZ CEL", GetViewportRect().Size / 2, Colors.Yellow);
        HighlightValidTargets(card);
    }

    private void CancelTargeting()
    {
        _inputState = InputState.Normal;
        _spellBeingCast = null;
        if (TargetingArrow != null) TargetingArrow.Visible = false;
        if (CancelSpellButton != null) CancelSpellButton.Visible = false;
        ClearHighlights();
    }

    private void HighlightValidTargets(CardInstance c)
    {
        foreach (var v in GetAllCardViewsOnBoard())
        {
            if (v.MyCardData == null) continue;
            bool enemy = v.MyCardData.OwnerPlayerId != _playerId;
            v.SetHighlight(true, enemy ? Colors.Red : Colors.Green);
            v.OnClicked -= OnTargetClicked; v.OnClicked += OnTargetClicked;
        }
    }

    private void ClearHighlights()
    {
        foreach (var v in GetAllCardViewsOnBoard()) { v.SetHighlight(false, Colors.White); v.OnClicked -= OnTargetClicked; }
    }

    private void OnTargetClicked(CardView target)
    {
        ExecutePlayerCommand(new PlaySpellCommand(_playerId, _spellBeingCast.InstanceId, target.MyCardData.InstanceId));
        CancelTargeting();
    }

    private void TrySelectTargetAtMouse()
    {
        var target = FindCardUnderMouse(BoardContainer, GetGlobalMousePosition());
        if (target?.MyCardData != null) OnTargetClicked(target);
    }

    private List<CardView> GetAllCardViewsOnBoard() { var l = new List<CardView>(); FindCardViewsRecursive(BoardContainer, l); return l; }
    private void FindCardViewsRecursive(Node n, List<CardView> l) { if (n is CardView c) l.Add(c); foreach (Node child in n.GetChildren()) FindCardViewsRecursive(child, l); }
    private CardView FindCardUnderMouse(Node p, Vector2 m)
    {
        foreach (Node c in p.GetChildren())
        {
            if (c is CardView cv && cv.GetGlobalRect().HasPoint(m)) return cv;
            var res = FindCardUnderMouse(c, m); if (res != null) return res;
        }
        return null;
    }

    // --- INITIALIZE ---

    private void InitializeGame()
    {
        string jsonPath = ProjectSettings.GlobalizePath("res://Data/Cards/cards.json");
        try { CardLibrary.Instance.LoadFromJson(jsonPath); } catch { }
        var rng = new DeterministicRng(new Random().Next());
        var factory = new CardFactory(CardLibrary.Instance, rng);

        List<CardInstance> deck1, deck2;
        try
        {
            // Opcjonalne wczytywanie z plików
            // string decksPath = ProjectSettings.GlobalizePath("res://Data/Decks/");
            // DeckRepository.Instance.LoadDecksFromDirectory(decksPath);
            // var deckService = new DeckService(DeckRepository.Instance, factory);
            // deck1 = deckService.CreateDeckFromId("starter_deck", 1);
            // deck2 = deckService.CreateDeckFromId("starter_deck", 2);
            throw new Exception("Force manual");
        }
        catch
        {
            deck1 = CreateRandomDeck(factory, 1);
            deck2 = CreateRandomDeck(factory, 2);
        }

        var state = GameState.Initial(1, deck1, deck2, rng).With(currentPhase: GamePhase.UnitOnly);
        _engine = new GameEngine(state, rng.Seed);
        _botController = new AIPlayerController(_engine, _botId, new StandardStrategy(), AISolverType.BeamSearch);

        if (BoardContainer != null && BoardContainer.GetChildCount() == 0 && LineSceneTemplate != null)
            for (int i = 0; i < 4; i++) BoardContainer.AddChild(LineSceneTemplate.Instantiate<LineView>());

        ConnectLines();
    }

    private void ConnectLines()
    {
        if (BoardContainer == null) return;
        foreach (var c in BoardContainer.GetChildren()) if (c is LineView lv)
            {
                lv.CardDroppedOnLine -= OnCardDropped; lv.CardDroppedOnLine += OnCardDropped;
            }
    }

    private List<CardInstance> CreateRandomDeck(CardFactory f, int o)
    {
        var l = new List<CardInstance>(); int[] ids = { 1, 2, 4, 8, 7, 3, 2, 1, 4 };
        var r = new Random();
        for (int i = 0; i < 30; i++) l.Add(f.CreateCard(ids[r.Next(ids.Length)], o));
        return l;
    }

    // --- SYSTEM POWIADOMIEŃ I FAZ (ZAAWANSOWANY) ---

    // Metoda wykrywająca co się zmieniło i dodająca komunikaty do kolejki
    private void CheckGameChanges(GameState current, GameState last)
    {
        // 1. Czy odbyła się walka? (Numer tury wzrósł)
        // To zostawiamy jako osobny, ważny komunikat
        if (current.TurnNumber > last.TurnNumber)
        {
            QueueMessage("⚔ FAZA WALKI ⚔", new Color(1, 0.2f, 0.2f)); // Czerwony
        }

        // 2. Czy zmieniła się faza?
        // (Usunęliśmy osobny blok sprawdzający zmianę gracza)
        if (current.CurrentPhase != last.CurrentPhase && current.CurrentPhase != GamePhase.Combat)
        {
            string phaseDesc = GetDetailedPhaseName(current);

            if (!string.IsNullOrEmpty(phaseDesc))
            {
                // Ustal kolor na podstawie tego, CZYJA jest to faza
                Color phaseColor;

                if (current.ActivePlayerId == _playerId)
                {
                    phaseColor = new Color(0.2f, 1f, 0.4f); // Zielony (Ty)
                }
                else
                {
                    phaseColor = new Color(1f, 0.5f, 0.2f); // Pomarańczowy/Czerwony (Wróg)
                }

                // Wyświetlamy komunikat fazy w odpowiednim kolorze
                QueueMessage(phaseDesc, phaseColor);
            }
        }
    }

    private string GetDetailedPhaseName(GameState state)
    {
        string who = state.ActivePlayerId == _playerId ? "TY" : "WRÓG";
        switch (state.CurrentPhase)
        {
            case GamePhase.Mulligan: return "WYMIANA KART";
            case GamePhase.UnitOnly: return $"FAZA 1: JEDNOSTKI ({who})";
            case GamePhase.UnitAndAction: return $"FAZA 2: MIESZANA ({who})";
            case GamePhase.ActionOnly: return $"FAZA 3: ZAKLĘCIA ({who})";
            default: return "";
        }
    }

    // Dodawanie do kolejki
    private void QueueMessage(string text, Color color, float duration = 1.5f)
    {
        _messageQueue.Enqueue((text, color, duration));
        if (!_isMessagePlaying) ProcessMessageQueue();
    }

    // Przetwarzanie kolejki (wywoływane w _Process lub po zakończeniu animacji)
    private void ProcessMessageQueue()
    {
        if (_isMessagePlaying || _messageQueue.Count == 0) return;

        var msg = _messageQueue.Dequeue();
        PlayMessageAnimation(msg.text, msg.color, msg.duration);
    }

    // Fizyczne wyświetlenie komunikatu z animacją
    private void PlayMessageAnimation(string text, Color color, float duration)
    {
        if (MessagePanel == null || MessageLabel == null) return;

        _isMessagePlaying = true;

        // Reset (dla pewności)
        if (_activeMessageTween != null && _activeMessageTween.IsValid()) _activeMessageTween.Kill();

        MessageLabel.Text = text;
        MessageLabel.Modulate = color;

        MessagePanel.Visible = true;
        MessagePanel.Modulate = new Color(1, 1, 1, 1);
        MessagePanel.Scale = new Vector2(0.1f, 0.1f); // Start mały

        _activeMessageTween = CreateTween();
        // 1. Pop-up (Powiększenie)
        _activeMessageTween.TweenProperty(MessagePanel, "scale", Vector2.One, 0.3f)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);

        // 2. Czekanie
        _activeMessageTween.TweenInterval(duration);

        // 3. Fade Out
        _activeMessageTween.TweenProperty(MessagePanel, "modulate:a", 0.0f, 0.3f);

        // 4. Callback po zakończeniu
        _activeMessageTween.TweenCallback(Callable.From(() =>
        {
            MessagePanel.Visible = false;
            _isMessagePlaying = false;
            // Spróbuj odtworzyć następny komunikat w kolejce
            ProcessMessageQueue();
        }));
    }

    // Metoda dla Game Over (pomija kolejkę, wyświetla na stałe)
    private void ShowGameOver()
    {
        _messageQueue.Clear(); // Czyścimy kolejkę
        if (_activeMessageTween != null && _activeMessageTween.IsValid()) _activeMessageTween.Kill();

        string t = "REMIS"; Color c = Colors.Gray;
        if (_engine.WinnerId == _playerId) { t = "ZWYCIĘSTWO!"; c = Colors.Green; }
        else if (_engine.WinnerId == _botId) { t = "PORAŻKA"; c = Colors.Red; }

        if (MessagePanel != null)
        {
            MessageLabel.Text = t;
            MessageLabel.Modulate = c;
            MessagePanel.Visible = true;
            MessagePanel.Modulate = new Color(1, 1, 1, 1);
            MessagePanel.Scale = Vector2.One;
        }

        if (RestartButton != null) RestartButton.Visible = true;
        if (EndTurnButton != null) EndTurnButton.Disabled = true;
    }

    // Stara metoda ShowBigMessage (dla kompatybilności ze startem gry)
    private void ShowBigMessage(string text, float duration, Color color)
    {
        // Po prostu wrzucamy do kolejki
        QueueMessage(text, color, duration);
    }

    private void ShowFloatingText(string text, Vector2 pos, Color color)
    {
        if (VFXContainer == null) return;
        var label = new Label();
        label.Text = text;
        label.Position = pos;
        label.Modulate = color;
        label.AddThemeFontSizeOverride("font_size", 48);
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        label.AddThemeConstantOverride("outline_size", 8);

        VFXContainer.AddChild(label);

        var tween = CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(label, "position", pos + new Vector2(0, -120), 1.5f).SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(label, "modulate:a", 0.0f, 1.5f).SetEase(Tween.EaseType.In);
        tween.Chain().TweenCallback(Callable.From(label.QueueFree));
    }

    private void UpdateUI()
    {
        if (_engine == null) return;

        // Nie aktualizujemy _lastRenderedState tutaj, robimy to w _Process na końcu!

        var p1 = _engine.CurrentState.PlayerA; var p2 = _engine.CurrentState.PlayerB;
        if (PlayerHpLabel != null) PlayerHpLabel.Text = $"HP: {p1.Health}";
        if (PlayerManaLabel != null) PlayerManaLabel.Text = $"MP: {p1.CurrentBlood}/{p1.MaxBlood}";
        if (EnemyHpLabel != null) EnemyHpLabel.Text = $"HP: {p2.Health}";
        if (EnemyManaLabel != null) EnemyManaLabel.Text = $"MP: {p2.CurrentBlood}/{p2.MaxBlood}";

        foreach (Node c in HandContainer.GetChildren()) c.QueueFree();
        foreach (var c in p1.Hand)
        {
            var v = CardSceneTemplate.Instantiate<CardView>(); HandContainer.AddChild(v); v.Render(c);
            v.OnClicked += OnCardClickedInHand;
            if (c.CurrentStats.BloodCost > p1.CurrentBlood || _engine.CurrentState.ActivePlayerId != _playerId) v.Modulate = new Color(0.5f, 0.5f, 0.5f);
        }

        if (EnemyHandContainer != null)
        {
            foreach (Node c in EnemyHandContainer.GetChildren()) c.QueueFree();
            for (int i = 0; i < p2.Hand.Count; i++)
            {
                var v = CardSceneTemplate.Instantiate<CardView>(); EnemyHandContainer.AddChild(v);
                v.RenderCardBack(); v.Scale = new Vector2(0.6f, 0.6f);
            }
        }

        int idx = 0;
        foreach (Node c in BoardContainer.GetChildren())
        {
            if (c is LineView lv && idx < _engine.CurrentState.Board.Lines.Count)
                lv.Render(_engine.CurrentState.Board.Lines[idx], CardSceneTemplate);
            idx++;
        }
    }
}