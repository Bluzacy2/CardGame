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

namespace CardGame.GodotClient
{
    /// <summary>
    /// Manages the entire visual presentation of the gameplay scene.
    /// Acts as the View layer, rendering the GameState and handling UI interactions.
    /// </summary>
    public partial class UIManager : Node
    {
        #region Scene References (Exports)

        [ExportGroup("Containers")]
        /// <summary>Container for the local player's hand cards.</summary>
        [Export] public Control HandContainer;
        /// <summary>Container for the board lines (lanes).</summary>
        [Export] public Control BoardContainer;
        /// <summary>Container for the opponent's hand (card backs).</summary>
        [Export] public Control EnemyHandContainer;
        /// <summary>Container for floating texts and visual effects.</summary>
        [Export] public Control VFXContainer;

        [ExportGroup("UI Elements")]
        /// <summary>Button to end the current phase/turn.</summary>
        [Export] public Button EndTurnButton;
        /// <summary>Button to cancel the current spell casting or targeting action.</summary>
        [Export] public Button CancelSpellButton;
        /// <summary>Button shown at game over to restart the match.</summary>
        [Export] public Button RestartButton;
        /// <summary>Line2D used to draw a visual arrow during targeting.</summary>
        [Export] public Line2D TargetingArrow;

        [ExportSubgroup("Labels")]
        [Export] public Label PlayerHpLabel;
        [Export] public Label PlayerManaLabel;
        [Export] public Label EnemyHpLabel;
        [Export] public Label EnemyManaLabel;

        [ExportGroup("Notifications")]
        /// <summary>Layer for modal popups and large messages, rendered on top.</summary>
        [Export] public Control NotificationLayer;
        [Export] public Control MessagePanel;
        [Export] public Label MessageLabel;

        [ExportGroup("Templates")]
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

        [ExportGroup("Piles")]
        [Export] public Control PlayerBloodGrid;
        [Export] public Control EnemyBloodGrid;
        [Export] public Label DeckCountLabel;
        [Export] public Control GraveyardContainer;

        #endregion

        #region Private Fields

        // Ghost Unit State (Visual placeholder during targeting)
        private CardView _ghostUnit;
        private int _ghostLineIdx = -1;

        private ChoiceModal _choiceModalInstance;
        private GameState _lastRenderedState;
        private Tween _activeMessageTween;

        #endregion

        #region Events

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

        #endregion

        #region Initialization

        /// <summary>
        /// Initializes UI components, hides overlays, connects internal signals, and instantiates the ChoiceModal.
        /// </summary>
        public override void _Ready()
        {
            SetupUI();
        }

        private void SetupUI()
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

            SetCancelButtonVisible(false);
            if (MulliganPanel != null) MulliganPanel.Visible = false;
            if (ConfirmMulliganButton != null)
                ConfirmMulliganButton.Pressed += () => OnConfirmMulliganClicked?.Invoke();

            // Initialize Choice Modal (hidden by default)
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

        #endregion

        #region Ghost Unit Management

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

        /// <summary>
        /// Removes the current ghost unit from the board.
        /// </summary>
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

        /// <summary>
        /// Gets the current ghost unit instance.
        /// </summary>
        public CardView GetGhostUnit() => _ghostUnit;

        #endregion

        #region Public API (Input & Logic Interface)

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
        /// <param name="options">List of option text labels.</param>
        /// <param name="onSelected">Callback action with the selected index.</param>
        /// <param name="enabledStates">Optional list of booleans indicating which options are enabled.</param>
        public void ShowChoiceModal(IEnumerable<string> options, Action<int> onSelected, List<bool> enabledStates = null)
        {
            if (_choiceModalInstance == null) return;
            _choiceModalInstance.SetCallback(onSelected);
            _choiceModalInstance.ShowOptions(options, enabledStates);
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
                // Ignore ghost unit and empty cards
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

        #endregion

        #region Rendering Core

        /// <summary>
        /// Main render loop. Updates the entire UI based on the current GameState.
        /// </summary>
        /// <param name="currentState">The current game state to render.</param>
        /// <param name="playerId">The local player's ID.</param>
        public void UpdateDisplay(GameState currentState, int playerId)
        {
            if (currentState == null) return;

            // LOGIKA CZYSZCZENIA DUCHA:
            if (_ghostUnit != null && _ghostLineIdx != -1)
            {
                var realUnit = currentState.Board.Lines[_ghostLineIdx].Player1Unit; // Zakładamy Player 1
                if (realUnit != null)
                {
                    RemoveGhostUnit();
                }
                else
                {
                    return; // Jeśli wciąż celujemy, nie odświeżaj
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
                    lineView.Render(
                        state.Board.Lines[lineIdx],
                        CardSceneTemplate,
                        (clickedCardView) => OnCardClicked?.Invoke(clickedCardView)
                    );
                    lineIdx++;
                }
            }
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
                view.Scale = new Vector2(0.8f, 0.8f);
            }
        }

        private void UpdateStats(GameState state, int playerId)
        {
            var p1 = state.GetPlayer(playerId);
            var p2 = state.GetOpponent(playerId);

            if (PlayerHpLabel != null) PlayerHpLabel.Text = $"HP: {p1.Health}";
            if (PlayerManaLabel != null) PlayerManaLabel.Text = $"Krew: {p1.CurrentBlood}/{p1.MaxBlood}";
            if (EnemyHpLabel != null) EnemyHpLabel.Text = $"HP: {p2.Health}";
            if (EnemyManaLabel != null) EnemyManaLabel.Text = $"Krew: {p2.CurrentBlood}/{p2.MaxBlood}";

            DrawBlood(PlayerBloodGrid, p1.CurrentBlood, p1.MaxBlood);
            DrawBlood(EnemyBloodGrid, p2.CurrentBlood, p2.MaxBlood);
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
        #endregion

        #region UI Control Methods

        public void ToggleMulliganPanel(bool visible) { if (MulliganPanel != null) MulliganPanel.Visible = visible; }
        public void UpdateMulliganCounter(int current, int max) { if (MulliganCounterLabel != null) MulliganCounterLabel.Text = $"Swap: {current} / {max}"; }

        public void RenderMulliganCards(IEnumerable<CardInstance> cards, List<int> selectedIds)
        {
            if (MulliganCardsContainer == null) return;
            foreach (Node child in MulliganCardsContainer.GetChildren()) child.QueueFree();
            foreach (var card in cards)
            {
                var view = CardSceneTemplate.Instantiate<CardView>();
                MulliganCardsContainer.AddChild(view);
                view.Render(card);
                view.SetMulliganSelected(selectedIds.Contains(card.InstanceId));
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

        public void HideTargetingArrow() { if (TargetingArrow != null) TargetingArrow.Visible = false; }
        public void SetCancelButtonVisible(bool visible) { if (CancelSpellButton != null) CancelSpellButton.Visible = visible; }
        public void SetEndTurnEnabled(bool enabled) { if (EndTurnButton != null) EndTurnButton.Disabled = !enabled; }

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
                _activeMessageTween.TweenInterval(duration);
                _activeMessageTween.TweenProperty(MessagePanel, "modulate:a", 0.0f, 0.5f);
                _activeMessageTween.TweenCallback(Callable.From(() => MessagePanel.Visible = false));
            }
        }

        public void ShowGameOverScreen(int? winnerId, int playerId, int botId)
        {
            string msg = winnerId == playerId ? "VICTORY!" : "DEFEAT";
            Color col = winnerId == playerId ? Colors.Green : Colors.Red;
            if (winnerId == null) { msg = "DRAW"; col = Colors.Gray; }

            ShowBigMessage(msg, 0, col);
            if (RestartButton != null) RestartButton.Visible = true;
            HideTargetingArrow();
        }

        private string GetPhaseFriendlyName(GamePhase phase)
        {
            return phase switch
            {
                GamePhase.Mulligan => "MULLIGAN",
                GamePhase.UnitOnly => "UNITS",
                GamePhase.UnitAndAction => "MIXED",
                GamePhase.ActionOnly => "SPELLS",
                _ => phase.ToString().ToUpper()
            };
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
                var res = FindCardUnderMouseRecursive(child, mousePos);
                if (res != null) return res;
            }
            return null;
        }

        private Vector2 FindVisualPosition(int cardId, int playerId)
        {
            if (cardId == -1) // Hero
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
        #endregion
    }
}