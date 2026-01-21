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

public partial class UIManager : Node
{
	// --- REFERENCJE DO SCENY ---
	[ExportGroup("Kontenery")]
	[Export] public Control HandContainer;
	[Export] public Control BoardContainer;
	[Export] public Control EnemyHandContainer;
	[Export] public Control VFXContainer;

	[ExportGroup("UI Elementy")]
	[Export] public Button EndTurnButton;
	[Export] public Button CancelSpellButton;
	[Export] public Button RestartButton;
	[Export] public Line2D TargetingArrow;

	[ExportSubgroup("Etykiety")]
	[Export] public Label PlayerHpLabel;
	[Export] public Label PlayerManaLabel;
	[Export] public Label EnemyHpLabel;
	[Export] public Label EnemyManaLabel;

	[ExportGroup("System Powiadomień")]
	[Export] public Control NotificationLayer;
	[Export] public Control MessagePanel;
	[Export] public Label MessageLabel;

	[ExportGroup("Szablony")]
	[Export] public PackedScene CardSceneTemplate;
	[Export] public PackedScene LineSceneTemplate;
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



	// --- NOWE: GHOST UNIT ---
	private CardView _ghostUnit;
	private int _ghostLineIdx = -1;
	public CardView GetGhostUnit() => _ghostUnit;


	// --- ZDARZENIA UI ---
	public event Action<CardView> OnCardClicked;
	public event Action OnEndTurnClicked;
	public event Action OnRestartClicked;
	public event Action OnCancelSpellClicked;
	public event Action OnConfirmMulliganClicked;
	private ChoiceModal _choiceModalInstance;

	private GameState _lastRenderedState;
	private Tween _activeMessageTween;

	public override void _Ready()
	{
		// Konfiguracja początkowa
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

		SetCancelButtonVisible(false);
		if (MulliganPanel != null) MulliganPanel.Visible = false;
		if (ConfirmMulliganButton != null)
			ConfirmMulliganButton.Pressed += () => OnConfirmMulliganClicked?.Invoke();

		// --- INICJALIZACJA MODALA ---
		if (ChoiceModalScene != null && NotificationLayer != null)
		{
			var instance = ChoiceModalScene.Instantiate();
			_choiceModalInstance = instance as ChoiceModal;

			if (_choiceModalInstance != null)
			{
				NotificationLayer.AddChild(_choiceModalInstance);
				// TO JEST KLUCZOWE:
				_choiceModalInstance.Visible = false;
			}
		}
	}

	// --- PUBLICZNE API (Metody używane przez InputController i GameBootstrap) ---

	public void CreateGhostUnit(CardInstance card, int lineIdx)
	{
		if (BoardContainer == null || CardSceneTemplate == null) return;

		RemoveGhostUnit();

		if (lineIdx >= 0 && lineIdx < BoardContainer.GetChildCount())
		{
			var lineView = BoardContainer.GetChild(lineIdx) as LineView;

			// Sprawdzamy czy LineView ma przypisany PlayerSlot
			if (lineView != null && lineView.PlayerSlot != null)
			{
				_ghostUnit = CardSceneTemplate.Instantiate<CardView>();

				// Dodajemy ducha do dolnego slotu
				// CenterContainer sam wycentruje go idealnie na środku dolnej połowy
				lineView.PlayerSlot.AddChild(_ghostUnit);

				_ghostUnit.Render(card);
				_ghostUnit.Modulate = new Color(1, 1, 1, 0.5f); // Półprzezroczysty
				_ghostUnit.MouseFilter = Control.MouseFilterEnum.Ignore;

				_ghostLineIdx = lineIdx;
			}
		}
	}

	public void RemoveGhostUnit()
	{
		if (_ghostUnit != null)
		{
			// Próba znalezienia i usunięcia spacera
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
	public Vector2 GetArrowStartPosition()
	{
		if (_ghostUnit != null && IsInstanceValid(_ghostUnit))
		{
			return _ghostUnit.GetGlobalRect().GetCenter();
		}
		return new Vector2(GetViewport().GetVisibleRect().Size.X / 2, GetViewport().GetVisibleRect().Size.Y - 100);
	}

	public void ShowChoiceModal(IEnumerable<string> options, Action<int> onSelected)
	{
		if (_choiceModalInstance == null) return;
		_choiceModalInstance.SetCallback(onSelected);
		_choiceModalInstance.ShowOptions(options);
	}
	public void ShowCardSelectionModal(List<CardInstance> cards, Action<int> onSelected)
	{
		if (_choiceModalInstance == null) return;

		// DEBUG: Sprawdź czy mamy szablon
		if (CardSceneTemplate == null) GD.PrintErr("[UI] CardSceneTemplate is missing in UIManager!");

		_choiceModalInstance.SetCallback(onSelected);
		_choiceModalInstance.ShowCardGrid(cards, CardSceneTemplate);
	}
	public void HideChoiceModal()
	{
		if (_choiceModalInstance != null) _choiceModalInstance.HideModal();
	}

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

	// Ta metoda była zdublowana jako HighlightValidTargets - usuwamy duplikat, zostawiamy tę
	public void HighlightValidTargets(CardInstance spell, int playerId)
	{
		// Kompatybilność wsteczna - delegujemy do nowej logiki (uproszczone)
		// Lepiej używać HighlightTargets(TargetType)
		HighlightTargets(TargetType.SelectedTarget, playerId);
	}

	public void ClearHighlights()
	{
		var allViews = GetAllCardViewsOnBoard();
		foreach (var view in allViews)
		{
			view.SetHighlight(false, Colors.White);
		}
	}

	public bool IsCardInHand(CardInstance card)
	{
		if (HandContainer == null) return false;
		return HandContainer.GetChildren()
			.OfType<CardView>()
			.Any(cv => cv.MyCardData?.InstanceId == card.InstanceId);
	}
	private void RenderBoard(GameState state)
	{
		if (BoardContainer == null || LineSceneTemplate == null) return;

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
				// --- ZMIANA: Przekazujemy lambda, która odpali zdarzenie OnCardClicked ---
				lineView.Render(
					state.Board.Lines[lineIdx],
					CardSceneTemplate,
					(clickedCardView) => OnCardClicked?.Invoke(clickedCardView)
				);
				lineIdx++;
			}
		}
	}
	public void UpdateDisplay(GameState currentState, int playerId)
	{
		if (currentState == null) return;

		// LOGIKA CZYSZCZENIA DUCHA:
		// Jeśli mieliśmy ducha na linii X, a w nowym stanie na linii X pojawiła się prawdziwa jednostka gracza,
		// to możemy usunąć ducha, bo RenderBoard zaraz narysuje prawdziwą kartę.
		if (_ghostUnit != null && _ghostLineIdx != -1)
		{
			var realUnit = currentState.Board.Lines[_ghostLineIdx].Player1Unit; // Zakładamy Player 1
			if (realUnit != null)
			{
				RemoveGhostUnit();
			}
			else
			{
				// Jeśli wciąż celujemy (nie ma jednostki), nie odświeżaj tej jednej linii, żeby duch nie zniknął
				// Ale RenderBoard i tak czyści kontenery...
				// Rozwiązanie: Pozwól RenderBoard wyczyścić wszystko, a potem przywróć Ducha?
				// Nie, prościej: Jeśli jest duch, po prostu nie rób RenderBoard.
				// Gra i tak jest "zablokowana" w trybie celowania.
				return;
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
		RenderPiles(currentState, playerId); // Nowa metoda do Decku/Cmentarza

		_lastRenderedState = currentState;
	}

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

    public void ShowBigMessage(string text, float duration = 0f, Color? color = null)
    {
        if (MessagePanel == null || MessageLabel == null) return;

        if (_activeMessageTween != null && _activeMessageTween.IsValid()) _activeMessageTween.Kill();

        // --- FIX: Jeśli tekst pusty, ukryj panel i wyjdź ---
        if (string.IsNullOrEmpty(text))
        {
            MessagePanel.Visible = false;
            return;
        }
        // ----------------------------------------------------

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

	// --- HELPERY PRYWATNE ---

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
		if (cardId == -1) // Bohater
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
