using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using CardGame.Core.Cards.Models;
using CardGame.Core.Cards.Data;
using CardGame.Core.State.Models;
using CardGame.Core.State.Enums;
using CardGame.Core.Events.Interfaces;
using CardGame.Core.Events;
/// <summary>
/// Manages the entire visual presentation of the gameplay scene.
/// Acts as the View layer, rendering the GameState and handling UI interactions.
/// </summary>
public partial class UIManager : Node
{
	
	[ExportGroup("Kontenery")]
    /// <summary>Container for the local player's hand cards.</summary>
    [Export] public Control HandContainer;
    /// <summary>Container for the board lines (lanes).</summary>
    [Export] public Control BoardContainer;
    /// <summary>Container for the opponent's hand (card backs).</summary>
    [Export] public Control EnemyHandContainer;
    /// <summary>Container for floating texts and visual effects.</summary>
    [Export] public Control VFXContainer;

	[ExportGroup("UI Elementy")]
    /// <summary>Button to end the current phase/turn.</summary>
    [Export] public Button EndTurnButton;
    /// <summary>Button to cancel the current spell casting or targeting action.</summary>
    [Export] public Button CancelSpellButton;
    /// <summary>Button shown at game over to restart the match.</summary>
    [Export] public Button RestartButton;
    /// <summary>Line2D used to draw a visual arrow during targeting.</summary>
    [Export] public Line2D TargetingArrow;

	[ExportSubgroup("Etykiety")]
	[Export] public Label PlayerHpLabel;
	[Export] public Label PlayerManaLabel;
	[Export] public Label EnemyHpLabel;
	[Export] public Label EnemyManaLabel;

	[ExportGroup("System Powiadomień")]
    /// <summary>Layer for modal popups and large messages, rendered on top.</summary>
    [Export] public Control NotificationLayer;
	[Export] public Control MessagePanel;
	[Export] public Label MessageLabel;

	[ExportGroup("Szablony")]
    /// <summary>The PackedScene used to instantiate individual cards.</summary>
    [Export] public PackedScene CardSceneTemplate;
    /// <summary>The PackedScene used to instantiate board lines.</summary>
    [Export] public PackedScene LineSceneTemplate;
    /// <summary>The PackedScene used to instantiate the Choice Modal.</summary>
    [Export] public PackedScene ChoiceModalScene;
	[ExportGroup("Mulligan UI")]
	[Export] public Control MulliganPanel;
	[Export] public Control MulliganCardsContainer;
	[Export] public Button ConfirmMulliganButton;
	[Export] public Label MulliganCounterLabel;

	[Export] public Control PlayerBloodGrid;     
	[Export] public Control EnemyBloodGrid;     
	[Export] public Label DeckCountLabel;       
	[Export] public Control GraveyardContainer;



    // Ghost Unit State (Visual placeholder during targeting)
    private CardView _ghostUnit;
	private int _ghostLineIdx = -1;
	public CardView GetGhostUnit() => _ghostUnit;


    /// <summary>Fired when a CardView (in hand or on board) is clicked.</summary>
    public event Action<CardView> OnCardClicked;
    /// <summary>Fired when the End Turn button is clicked.</summary>
    public event Action OnEndTurnClicked;
    /// <summary>Fired when the Restart button is clicked.</summary>
    public event Action OnRestartClicked;
    /// <summary>Fired when the Cancel Spell button is clicked.</summary>
    public event Action OnCancelSpellClicked;
    /// <summary>Fired when the Confirm Mulligan button is clicked.</summary>
    public event Action OnConfirmMulliganClicked;
	private ChoiceModal _choiceModalInstance;

	private GameState _lastRenderedState;
	private Tween _activeMessageTween;
    /// <summary>
    /// Initializes UI components, hides overlays, connects internal signals, and instantiates the ChoiceModal.
    /// </summary>
    public override void _Ready()
	{
		if (NotificationLayer != null)
		{
			NotificationLayer.Visible = true;
			NotificationLayer.MouseFilter = Control.MouseFilterEnum.Ignore;
		}
		if (MessagePanel != null)
		{
			MessagePanel.Visible = false;
			MessagePanel.PivotOffset = MessagePanel.Size / 2;
		}
		if (TargetingArrow != null) TargetingArrow.Visible = false;

		if (EndTurnButton != null) EndTurnButton.Pressed += () => OnEndTurnClicked?.Invoke();
		if (RestartButton != null) RestartButton.Pressed += () => OnRestartClicked?.Invoke();
		if (CancelSpellButton != null) CancelSpellButton.Pressed += () => OnCancelSpellClicked?.Invoke();
        // Initialize Choice Modal (hidden by default)
        SetCancelButtonVisible(false);
		if (MulliganPanel != null) MulliganPanel.Visible = false;
		if (ConfirmMulliganButton != null)
			ConfirmMulliganButton.Pressed += () => OnConfirmMulliganClicked?.Invoke();

		if (ChoiceModalScene != null && NotificationLayer != null)
		{
			var instance = ChoiceModalScene.Instantiate();
			_choiceModalInstance = instance as ChoiceModal;

			if (_choiceModalInstance != null)
			{
				NotificationLayer.AddChild(_choiceModalInstance);
				_choiceModalInstance.Visible = false;
			}
		}
	}

    /// <summary>
    /// Creates a semi-transparent "Ghost" unit on the board.
    /// Used to visualize where a unit will be placed while the player is selecting a target for its Battlecry.
    /// </summary>
    /// <param name="card">The card data to visualize.</param>
    /// <param name="lineIdx">The board line index where the ghost should appear.</param>
    public void CreateGhostUnit(CardInstance card, int lineIdx)
	{
		if (BoardContainer == null || CardSceneTemplate == null) return;

		RemoveGhostUnit();

		if (lineIdx >= 0 && lineIdx < BoardContainer.GetChildCount())
		{
			var lineView = BoardContainer.GetChild(lineIdx) as LineView;

			if (lineView != null && lineView.PlayerSlot != null)
			{
				_ghostUnit = CardSceneTemplate.Instantiate<CardView>();

                // Add to player slot (bottom of lane)
                lineView.PlayerSlot.AddChild(_ghostUnit);

				_ghostUnit.Render(card);
				_ghostUnit.Modulate = new Color(1, 1, 1, 0.5f);  // Semi-transparent
                _ghostUnit.MouseFilter = Control.MouseFilterEnum.Ignore; // Ignore mouse events so we can target through it

                _ghostLineIdx = lineIdx;
			}
		}
	}
    /// <summary>
    /// Removes the current ghost unit from the board.
    /// </summary>
    public void RemoveGhostUnit()
	{
		if (_ghostUnit != null)
		{
            // Optional spacer cleanup if used in previous iterations
            var parent = _ghostUnit.GetParent();
			if (parent != null)
			{
				var spacer = parent.GetNodeOrNull("GhostSpacer");
				if (spacer != null) spacer.QueueFree();
			}

			_ghostUnit.QueueFree();
			_ghostUnit = null;
			_ghostLineIdx = -1;
		}
	}
    /// <summary>
    /// Returns the screen position from which the targeting arrow should originate.
    /// If a Ghost Unit exists, returns its center. Otherwise, returns a default position (hand).
    /// </summary>
    public Vector2 GetArrowStartPosition()
	{
		if (_ghostUnit != null && IsInstanceValid(_ghostUnit))
		{
			return _ghostUnit.GetGlobalRect().GetCenter();
		}
		return new Vector2(GetViewport().GetVisibleRect().Size.X / 2, GetViewport().GetVisibleRect().Size.Y - 100);
	}
    /// <summary>
    /// Displays the choice modal with text options (e.g., for Expectancy card).
    /// </summary>
    /// <param name="options">List of option texts.</param>
    /// <param name="onSelected">Callback action with the selected index.</param>
    /// <param name="enabledStates">List of booleans indicating which options are enabled.</param>
    public void ShowChoiceModal(IEnumerable<string> options, Action<int> onSelected)
	{
		if (_choiceModalInstance == null) return;
		_choiceModalInstance.SetCallback(onSelected);
		_choiceModalInstance.ShowOptions(options);
	}
    /// <summary>
    /// Displays the choice modal with a grid of cards (e.g., for Tutor/Search effects).
    /// </summary>
    /// <param name="cards">List of cards to display.</param>
    /// <param name="onSelected">Callback action with the selected index.</param>
    public void ShowCardSelectionModal(List<CardInstance> cards, Action<int> onSelected)
	{
		if (_choiceModalInstance == null) return;

		if (CardSceneTemplate == null) GD.PrintErr("[UI] CardSceneTemplate is missing in UIManager!");

		_choiceModalInstance.SetCallback(onSelected);
		_choiceModalInstance.ShowCardGrid(cards, CardSceneTemplate);
	}
    /// <summary>
    /// Hides the choice modal.
    /// </summary>
    public void HideChoiceModal()
	{
		if (_choiceModalInstance != null) _choiceModalInstance.HideModal();
	}
    /// <summary>
    /// Highlights valid targets on the board with a colored border.
    /// </summary>
    /// <param name="type">The type of targets to highlight (Enemy, Friendly, etc.).</param>
    /// <param name="playerId">The local player's ID.</param>
    public void HighlightTargets(TargetType type, int playerId)
	{
		var allViews = GetAllCardViewsOnBoard();
		ClearHighlights();

		foreach (var view in allViews)
		{
			if (view == _ghostUnit) continue;
			if (view.MyCardData == null) continue;

			int owner = view.MyCardData.OwnerPlayerId;
			bool isFriendly = (owner == playerId);
			bool match = false;

			switch (type)
			{
				case TargetType.TargetEnemyUnit: match = !isFriendly; break;
				case TargetType.TargetFriendlyUnit: match = isFriendly; break;
				case TargetType.SelectedTarget: match = true; break;
				case TargetType.OtherFriendlyUnits: match = isFriendly; break;
			}

			if (match)
			{
				view.SetHighlight(true, isFriendly ? Colors.Green : Colors.Red);
			}
		}
	}

    /// <summary>
    /// Helper for legacy calls. Highlights valid targets for a spell.
    /// </summary>
    public void HighlightValidTargets(CardInstance spell, int playerId)
	{
		HighlightTargets(TargetType.SelectedTarget, playerId);
	}
    /// <summary>
    /// Removes highlights from all cards on the board.
    /// </summary>
    public void ClearHighlights()
	{
		var allViews = GetAllCardViewsOnBoard();
		foreach (var view in allViews)
		{
			view.SetHighlight(false, Colors.White);
		}
	}
    /// <summary>
    /// Checks if a given card instance is currently visually in the player's hand.
    /// </summary>
    public bool IsCardInHand(CardInstance card)
	{
		if (HandContainer == null) return false;
		return HandContainer.GetChildren()
			.OfType<CardView>()
			.Any(cv => cv.MyCardData?.InstanceId == card.InstanceId);
	}
    /// <summary>
    /// Renders the board lines and units.
    /// </summary>
    private void RenderBoard(GameState state)
	{
		if (BoardContainer == null || LineSceneTemplate == null) return;
        // Ensure 4 lines exist
        if (BoardContainer.GetChildCount() < 4)
		{
			for (int i = BoardContainer.GetChildCount(); i < 4; i++)
			{
				BoardContainer.AddChild(LineSceneTemplate.Instantiate<LineView>());
			}
		}

		int lineIdx = 0;
		foreach (Node child in BoardContainer.GetChildren())
		{
			if (child is LineView lineView && lineIdx < state.Board.Lines.Count)
			{
                // Pass a lambda to handle clicks, bubbling the event up to UIManager
                lineView.Render(
					state.Board.Lines[lineIdx],
					CardSceneTemplate,
					(clickedCardView) => OnCardClicked?.Invoke(clickedCardView)
				);
				lineIdx++;
			}
		}
	}
    /// <summary>
    /// Main render loop. Updates the entire UI based on the current GameState.
    /// </summary>
    /// <param name="currentState">The current game state to render.</param>
    /// <param name="playerId">The local player's ID.</param>
    public void UpdateDisplay(GameState currentState, int playerId)
	{
		if (currentState == null) return;
        // GHOST UNIT LOGIC:
        // If a ghost exists at a specific line, check if a real unit has replaced it.
        // If yes -> Remove Ghost.
        // If no -> Skip rendering board for that frame to avoid flickering/deleting the ghost.
        if (_ghostUnit != null && _ghostLineIdx != -1)
		{
			var realUnit = currentState.Board.Lines[_ghostLineIdx].Player1Unit; // Assuming Player 1 is always local
            if (realUnit != null)
			{
				RemoveGhostUnit();
			}
			else
			{
				return; // Wait for the real unit to appear
            }
		}
		if (MessagePanel != null && MessageLabel != null)
		{
			string who = currentState.ActivePlayerId == playerId ? "TWOJA TURA" : "TURA BOTA";
			string faza = GetPhaseFriendlyName(currentState.CurrentPhase);

			MessageLabel.Text = $"{faza}\n{who}\nRUNDA {currentState.TurnNumber}";

			MessagePanel.Visible = true;
			MessagePanel.Modulate = new Color(1, 1, 1, 1);

			if (_activeMessageTween != null && _activeMessageTween.IsValid())
				_activeMessageTween.Kill();
		}

		UpdateStats(currentState, playerId);
		RenderHand(currentState, playerId);
		RenderEnemyHand(currentState, playerId);
		RenderBoard(currentState);
		RenderPiles(currentState, playerId); 

		_lastRenderedState = currentState;
	}
    /// <summary>
    /// Renders the deck count and the top card of the graveyard.
    /// </summary>
    private void RenderPiles(GameState state, int playerId)
	{
		var p1 = state.GetPlayer(playerId);
		if (DeckCountLabel != null)
			DeckCountLabel.Text = $"{p1.DrawPile.Count}x";

		if (GraveyardContainer != null && p1.DiscardPile.Any())
		{
			foreach (Node child in GraveyardContainer.GetChildren()) child.QueueFree();
			var lastDead = p1.DiscardPile.Last();
			var view = CardSceneTemplate.Instantiate<CardView>();
			GraveyardContainer.AddChild(view);
			view.Render(lastDead);
			view.Modulate = new Color(0.5f, 0.5f, 0.5f);
		}
	}

	public void ToggleMulliganPanel(bool visible)
	{
		if (MulliganPanel != null) MulliganPanel.Visible = visible;
	}

	public void UpdateMulliganCounter(int current, int max)
	{
		if (MulliganCounterLabel != null)
			MulliganCounterLabel.Text = $"Do wymiany: {current} / {max}";
	}

	public void RenderMulliganCards(IEnumerable<CardInstance> cards, List<int> selectedIds)
	{
		if (MulliganCardsContainer == null) return;

		foreach (Node child in MulliganCardsContainer.GetChildren()) child.QueueFree();

		foreach (var card in cards)
		{
			var view = CardSceneTemplate.Instantiate<CardView>();
			MulliganCardsContainer.AddChild(view);
			view.Render(card);

			bool isSelected = selectedIds.Contains(card.InstanceId);
			view.SetMulliganSelected(isSelected);

			view.OnClicked += (v) => OnCardClicked?.Invoke(v);
		}
	}

	public void UpdateTargetingArrow(Vector2 start, Vector2 end)
	{
		if (TargetingArrow != null)
		{
			TargetingArrow.Visible = true;
			TargetingArrow.ClearPoints();
			TargetingArrow.AddPoint(start);
			TargetingArrow.AddPoint(end);
		}
	}

	public void HideTargetingArrow()
	{
		if (TargetingArrow != null) TargetingArrow.Visible = false;
	}

	public void SetCancelButtonVisible(bool visible)
	{
		if (CancelSpellButton != null) CancelSpellButton.Visible = visible;
	}

	public void SetEndTurnEnabled(bool enabled)
	{
		if (EndTurnButton != null) EndTurnButton.Disabled = !enabled;
	}
    /// <summary>
    /// Displays floating text (e.g. damage numbers) at a specific position.
    /// </summary>
    public void ShowFloatingText(string text, Vector2 pos, Color color)
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

	public CardView FindCardUnderMouse(Vector2 mousePos)
	{
		if (BoardContainer == null) return null;
		return FindCardUnderMouseRecursive(BoardContainer, mousePos);
	}
    /// <summary>
    /// Displays a large message in the center of the screen (e.g., "Your Turn").
    /// </summary>
    public void ShowBigMessage(string text, float duration = 0f, Color? color = null)
    {
        if (MessagePanel == null || MessageLabel == null) return;

        if (_activeMessageTween != null && _activeMessageTween.IsValid()) _activeMessageTween.Kill();

        if (string.IsNullOrEmpty(text))
        {
            MessagePanel.Visible = false;
            return;
        }

        MessageLabel.Text = text;
        MessageLabel.Modulate = color ?? Colors.White;

        MessagePanel.Visible = true;
        MessagePanel.Modulate = new Color(1, 1, 1, 1);
        MessagePanel.Scale = Vector2.One;

        if (duration > 0f)
        {
            _activeMessageTween = CreateTween();
            _activeMessageTween.TweenProperty(MessagePanel, "scale", Vector2.One, 0.3f)
                .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            _activeMessageTween.TweenInterval(duration);
            _activeMessageTween.TweenProperty(MessagePanel, "modulate:a", 0.0f, 0.5f);
            _activeMessageTween.TweenCallback(Callable.From(() => MessagePanel.Visible = false));
        }
        else
        {
            MessagePanel.Scale = Vector2.One;
        }
    }

    public void ShowGameOverScreen(int? winnerId, int playerId, int botId)
	{
		string msg = "REMIS";
		Color col = Colors.Gray;
		if (winnerId == playerId) { msg = "ZWYCIĘSTWO!"; col = Colors.Green; }
		else if (winnerId == botId) { msg = "PORAŻKA..."; col = Colors.Red; }

		ShowBigMessage(msg, 0, col);

		if (RestartButton != null) RestartButton.Visible = true;
		if (EndTurnButton != null) EndTurnButton.Disabled = true;

		HideTargetingArrow();
		SetCancelButtonVisible(false);
	}

	public void HandleVisualEvents(IEnumerable<IGameEvent> events, int playerId)
	{
		foreach (var e in events)
		{
			if (e is UnitDamagedEvent ude)
			{
				string text = $"-{ude.Amount}";
				Vector2 pos = FindVisualPosition(ude.Unit?.InstanceId ?? -1, playerId);
				ShowFloatingText(text, pos, Colors.Red);
			}
		}
	}

	private List<CardView> GetAllCardViewsOnBoard()
	{
		var list = new List<CardView>();
		if (BoardContainer != null) FindCardViewsRecursive(BoardContainer, list);
		return list;
	}

	private void FindCardViewsRecursive(Node node, List<CardView> list)
	{
		if (node is CardView cv) list.Add(cv);
		foreach (Node child in node.GetChildren()) FindCardViewsRecursive(child, list);
	}

	private CardView FindCardUnderMouseRecursive(Node parent, Vector2 mousePos)
	{
		foreach (Node child in parent.GetChildren())
		{
			if (child is CardView cv && cv.GetGlobalRect().HasPoint(mousePos)) return cv;
			var result = FindCardUnderMouseRecursive(child, mousePos);
			if (result != null) return result;
		}
		return null;
	}

	private Vector2 FindVisualPosition(int cardId, int playerId)
	{
		if (cardId == -1)
			return new Vector2(GetViewport().GetVisibleRect().Size.X / 2, GetViewport().GetVisibleRect().Size.Y / 2);

		Vector2 fallback = GetViewport().GetVisibleRect().Size.X > 0 ? GetViewport().GetVisibleRect().Size / 2 : new Vector2(500, 300);
		if (BoardContainer == null) return fallback;

		var targetNode = FindCardViewRecursiveById(BoardContainer, cardId);
		if (targetNode != null)
			return targetNode.GetGlobalRect().GetCenter() + new Vector2(0, -50);

		return fallback;
	}

	private CardView FindCardViewRecursiveById(Node parent, int cardId)
	{
		foreach (Node child in parent.GetChildren())
		{
			if (child is CardView cv && cv.MyCardData != null && cv.MyCardData.InstanceId == cardId)
				return cv;
			var res = FindCardViewRecursiveById(child, cardId);
			if (res != null) return res;
		}
		return null;
	}
    /// <summary>
    /// Updates labels for HP, Mana, and Blood resources.
    /// </summary>
    private void UpdateStats(GameState state, int playerId)
	{
		var p1 = state.GetPlayer(playerId);
		var p2 = state.GetOpponent(playerId);

		if (PlayerHpLabel != null) PlayerHpLabel.Text = p1.Health.ToString();
		if (EnemyHpLabel != null) EnemyHpLabel.Text = p2.Health.ToString();

		DrawBlood(PlayerBloodGrid, p1.CurrentBlood, p1.MaxBlood);
		DrawBlood(EnemyBloodGrid, p2.CurrentBlood, p2.MaxBlood);
	}

	private string GetPhaseFriendlyName(GamePhase phase)
	{
		switch (phase)
		{
			case GamePhase.Mulligan: return "WYMIANA";
			case GamePhase.UnitOnly: return "JEDNOSTKI";
			case GamePhase.UnitAndAction: return "MIESZANA";
			case GamePhase.ActionOnly: return "AKCJE";
			default: return phase.ToString().ToUpper();
		}
	}
    /// <summary>
    /// Draws blood icons for the resource UI.
    /// </summary>
    private void DrawBlood(Control grid, int current, int max)
	{
		if (grid == null) return;
		foreach (Node child in grid.GetChildren()) child.QueueFree();

		for (int i = 0; i < max; i++)
		{
			var tr = new TextureRect();
			tr.CustomMinimumSize = new Vector2(25, 25);
			tr.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
			tr.Texture = GD.Load<Texture2D>(i < current ?
				"res://Assets/Menus/Game/bloodPoint.png" :
				"res://Assets/Menus/Game/bloodUnactive.png");
			grid.AddChild(tr);
		}
	}
    /// <summary>
    /// Renders the local player's hand.
    /// </summary>
    private void RenderHand(GameState state, int playerId)
	{
		if (HandContainer == null || CardSceneTemplate == null) return;

		var p1 = state.GetPlayer(playerId);

		foreach (Node child in HandContainer.GetChildren()) child.QueueFree();

		foreach (var card in p1.Hand)
		{
			var cardVis = CardSceneTemplate.Instantiate<CardView>();
			HandContainer.AddChild(cardVis);
			cardVis.Render(card);

			cardVis.OnClicked += (cv) => OnCardClicked?.Invoke(cv);

			if (card.CurrentStats.BloodCost > p1.CurrentBlood || state.ActivePlayerId != playerId)
				cardVis.Modulate = new Color(0.5f, 0.5f, 0.5f);
			else
				cardVis.Modulate = new Color(1, 1, 1);
		}
	}
    /// <summary>
    /// Renders the opponent's hand as card backs.
    /// </summary>
    private void RenderEnemyHand(GameState state, int playerId)
	{
		if (EnemyHandContainer == null || CardSceneTemplate == null) return;
		var p2 = state.GetOpponent(playerId);

		foreach (Node child in EnemyHandContainer.GetChildren()) child.QueueFree();

		for (int i = 0; i < p2.Hand.Count; i++)
		{
			var cardBack = CardSceneTemplate.Instantiate<CardView>();
			EnemyHandContainer.AddChild(cardBack);
			cardBack.RenderCardBack();


			cardBack.Scale = new Vector2(0.4f, 0.4f);

			cardBack.CustomMinimumSize = new Vector2(80, 110);
		}
	}
	public void ShowChoiceModal(IEnumerable<string> options, Action<int> onSelected, List<bool> enabledStates = null)
	{
		if (_choiceModalInstance == null) return;
		_choiceModalInstance.SetCallback(onSelected);
		_choiceModalInstance.ShowOptions(options, enabledStates);
	}
	private void CheckPhaseChange(GameState current, GameState last, int playerId)
	{
		if (current.TurnNumber > last.TurnNumber)
		{
			ShowBigMessage("⚔ FAZA WALKI ⚔", 1.5f, new Color(1, 0.2f, 0.2f));
		}

		if (current.CurrentPhase != last.CurrentPhase || current.ActivePlayerId != last.ActivePlayerId)
		{
			if (current.CurrentPhase == GamePhase.Combat) return;

			string who = current.ActivePlayerId == playerId ? "TY" : "WRÓG";
			string msg = "";
			Color col = current.ActivePlayerId == playerId ? new Color(0.2f, 1f, 0.4f) : new Color(1f, 0.6f, 0.2f);

			switch (current.CurrentPhase)
			{
				case GamePhase.Mulligan: msg = "WYMIANA KART"; break;
				case GamePhase.UnitOnly: msg = $"FAZA 1: JEDNOSTKI ({who})"; break;
				case GamePhase.UnitAndAction: msg = $"FAZA 2: MIESZANA ({who})"; break;
				case GamePhase.ActionOnly: msg = $"FAZA 3: ZAKLĘCIA ({who})"; break;
			}

			if (!string.IsNullOrEmpty(msg)) ShowBigMessage(msg, 1.5f, col);
		}
	}
}
