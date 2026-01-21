using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using CardGame.Core.Cards.Models;
using CardGame.Core.Cards.Data;
using CardGame.Core.Commands.Implementations;
using CardGame.Core.Commands.Interfaces;
using CardGame.Core.State.Models;
using CardGame.Core.Cards.Logic;

namespace CardGame.GodotClient
{
    /// <summary>
    /// Controls player input and translates it into game commands.
    /// Manages input states (Idle, Targeting, Pending), card interactions, and mulligan phase.
    /// Acts as the bridge between the UI events and the Game Engine.
    /// </summary>
    public partial class InputController : Node
    {
        #region Enums & Fields

        /// <summary>
        /// Represents the current state of player input.
        /// </summary>
        public enum InputState
        {
            /// <summary>Waiting for turn or animation, no input allowed.</summary>
            Idle,
            /// <summary>Standard turn state, can play cards from hand.</summary>
            Normal,
            /// <summary>Player selected a card and is choosing a target.</summary>
            TargetingCard,
            /// <summary>Engine requested a decision (choice or target) from the player.</summary>
            PendingTarget,
            /// <summary>Initial mulligan phase for card replacement.</summary>
            Mulligan
        }

        private InputState _currentState = InputState.Idle;
        private int _playerId;
        private UIManager _ui;
        private GameState _latestGameState;

        // Temporary state for current action
        private CardInstance _selectedCardHand;
        private int? _pendingUnitLineIdx;

        private PendingInteraction _currentPending;

        // Mulligan State
        private List<int> _mulliganSelection = new List<int>();
        private const int MAX_MULLIGAN_SWAPS = 3;
        private List<CardInstance> _mulliganHandCache;

        #endregion

        #region Events

        /// <summary>
        /// Event triggered when the player completes a valid action, producing a command for the engine.
        /// </summary>
        public event Action<IGameCommand> OnPlayerCommand;

        #endregion

        #region Initialization & Lifecycle

        /// <summary>
        /// Initializes the controller with player ID and UI manager reference.
        /// </summary>
        /// <param name="playerId">The local player's ID.</param>
        /// <param name="uiManager">Reference to the UIManager for visual feedback.</param>
        public void Initialize(int playerId, UIManager uiManager)
        {
            _playerId = playerId;
            _ui = uiManager;
        }

        public override void _Process(double delta)
        {
            bool isChoiceMode = _currentState == InputState.PendingTarget &&
                                _currentPending?.RequiredTargetType == TargetType.Choice;

            if ((_currentState == InputState.TargetingCard || _currentState == InputState.PendingTarget) && !isChoiceMode)
            {
                UpdateTargetingArrow();
            }
            else
            {
                _ui.HideTargetingArrow();
            }
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event is InputEventMouseButton mb && mb.Pressed)
            {
                if (mb.ButtonIndex == MouseButton.Right)
                {
                    if (_currentState == InputState.TargetingCard) CancelTargeting();
                }
                else if (mb.ButtonIndex == MouseButton.Left)
                {
                    if (_currentState == InputState.TargetingCard || _currentState == InputState.PendingTarget)
                    {
                        TrySelectTargetUnderMouse();
                    }
                }
            }
        }

        #endregion

        #region State Management

        /// <summary>
        /// Updates the controller state based on the latest GameState from the engine.
        /// Handles phase transitions and pending interactions.
        /// </summary>
        /// <param name="gameState">The current state of the game.</param>
        public void UpdateState(GameState gameState)
        {
            _latestGameState = gameState;

            // 1. Priority: Mulligan Phase
            if (gameState.CurrentPhase == CardGame.Core.State.Enums.GamePhase.Mulligan)
            {
                if (_currentState != InputState.Mulligan)
                    StartMulligan(gameState.PlayerA.Hand.ToList());
                return;
            }

            // 2. Priority: Pending Interaction (Choices/Targets)
            if (gameState.PendingInteraction != null)
            {
                // HIDE BOT ACTIONS: If it's bot's turn to choose, disable player UI
                if (gameState.ActivePlayerId != _playerId)
                {
                    if (_currentState != InputState.Idle)
                    {
                        _currentState = InputState.Idle;
                        _ui.HideChoiceModal();
                        _ui.HideTargetingArrow();
                        _ui.ClearHighlights();
                    }
                    return;
                }

                // Guard against re-initializing the same interaction
                bool isSameInteraction = _currentPending != null &&
                                         _currentPending.SourceCardInstanceId == gameState.PendingInteraction.SourceCardInstanceId &&
                                         _currentPending.RequiredTargetType == gameState.PendingInteraction.RequiredTargetType;

                if (_currentState == InputState.PendingTarget && isSameInteraction)
                {
                    return;
                }

                if (_currentState == InputState.TargetingCard) CancelTargeting();

                StartPendingResolution(gameState.PendingInteraction);
                return;
            }

            // Reset if pending interaction was resolved
            if (_currentState == InputState.PendingTarget && gameState.PendingInteraction == null)
            {
                _currentPending = null;
                _currentState = InputState.Normal;
                _ui.HideChoiceModal();
                _ui.HideTargetingArrow();
                _ui.ClearHighlights();
                _ui.ShowBigMessage("", 0);
            }

            // 3. Normal Player Turn
            if (gameState.ActivePlayerId == _playerId)
            {
                if (_currentState != InputState.TargetingCard && _currentState != InputState.PendingTarget)
                {
                    _currentState = InputState.Normal;
                }
            }
            else
            {
                _currentState = InputState.Idle;
                _ui.HideTargetingArrow();
                _ui.ClearHighlights();
            }
        }

        #endregion

        #region Input Handlers

        /// <summary>
        /// Handles click events on cards (from hand or board).
        /// </summary>
        /// <param name="card">The clicked card instance.</param>
        public void HandleCardClick(CardInstance card)
        {
            if (_currentState == InputState.Mulligan)
            {
                ProcessMulliganClick(card);
                return;
            }

            if (_currentState == InputState.Normal)
            {
                if (card.OwnerPlayerId == _playerId && _ui.IsCardInHand(card))
                {
                    if (card.Definition.Type == CardType.Spell)
                        TryPlayCardFromHand(card, null);
                }
            }
        }

        /// <summary>
        /// Handles dropping a card onto a specific board line.
        /// </summary>
        /// <param name="card">The dropped card.</param>
        /// <param name="lineIdx">The target line index.</param>
        public void HandleCardDrop(CardInstance card, int lineIdx)
        {
            if (_currentState != InputState.Normal) return;

            if (card.Definition.Type == CardType.Unit)
            {
                bool needsTarget = HasManualTarget(card);
                bool hasValidTargets = CheckIfHasValidTargetsOnBoard(card);

                if (needsTarget && hasValidTargets)
                {
                    // Enter targeting mode with Ghost Unit
                    TryPlayCardFromHand(card, lineIdx);
                }
                else
                {
                    OnPlayerCommand?.Invoke(new PlayUnitCommand(_playerId, card.InstanceId, lineIdx));
                }
            }
            else if (card.Definition.Type == CardType.Spell)
            {
                TryPlayCardFromHand(card, null);
            }
        }

        #endregion

        #region Gameplay Logic

        private void TryPlayCardFromHand(CardInstance card, int? unitLineIdx)
        {
            bool needsTarget = HasManualTarget(card);
            bool hasValidTargets = CheckIfHasValidTargetsOnBoard(card);

            // If card needs a target AND there are valid targets -> Enter targeting mode
            if (needsTarget && hasValidTargets)
            {
                _currentState = InputState.TargetingCard;
                _selectedCardHand = card;
                _pendingUnitLineIdx = unitLineIdx;

                // Create Ghost Unit only for Unit cards
                if (card.Definition.Type == CardType.Unit && unitLineIdx.HasValue)
                {
                    _ui.CreateGhostUnit(card, unitLineIdx.Value);
                }

                _ui.ShowBigMessage("WYBIERZ CEL", 0, Colors.Yellow);
                _ui.SetCancelButtonVisible(true);
                _ui.HighlightTargets(GetTargetTypeForCard(card), _playerId);
            }
            else
            {
                // Play immediately (Global Spell or Unit without targets)
                if (card.Definition.Type == CardType.Spell)
                {
                    // Ensure clean state before firing command
                    CancelTargeting();
                    OnPlayerCommand?.Invoke(new PlaySpellCommand(_playerId, card.InstanceId));
                }
                // Units without targets are handled in HandleCardDrop
            }
        }

        /// <summary>
        /// Cancels the current targeting action and resets UI.
        /// </summary>
        public void CancelTargeting()
        {
            _currentState = InputState.Normal;
            _selectedCardHand = null;
            _pendingUnitLineIdx = null;

            _ui.RemoveGhostUnit();
            _ui.HideTargetingArrow();
            _ui.SetCancelButtonVisible(false);
            _ui.ClearHighlights();
            _ui.ShowBigMessage("", 0);
        }

        private void UpdateTargetingArrow()
        {
            Vector2 start = _ui.GetArrowStartPosition();
            _ui.UpdateTargetingArrow(start, GetViewport().GetMousePosition());
        }

        private void TrySelectTargetUnderMouse()
        {
            var mousePos = GetViewport().GetMousePosition();
            var targetCard = _ui.FindCardUnderMouse(mousePos);

            if (targetCard != null && targetCard.MyCardData != null)
            {
                if (targetCard == _ui.GetGhostUnit()) return;
                SubmitTarget(targetCard.MyCardData.InstanceId);
            }
        }

        private void SubmitTarget(int targetId)
        {
            if (_currentState == InputState.TargetingCard)
            {
                if (_selectedCardHand.Definition.Type == CardType.Unit)
                {
                    int line = _pendingUnitLineIdx ?? 0;
                    OnPlayerCommand?.Invoke(new PlayUnitCommand(_playerId, _selectedCardHand.InstanceId, line, targetId));
                }
                else if (_selectedCardHand.Definition.Type == CardType.Spell)
                {
                    OnPlayerCommand?.Invoke(new PlaySpellCommand(_playerId, _selectedCardHand.InstanceId, targetId));
                }

                _ui.RemoveGhostUnit();
                CancelTargeting();
            }
            else if (_currentState == InputState.PendingTarget)
            {
                OnPlayerCommand?.Invoke(new SelectTargetCommand(_playerId, targetId));

                _ui.ClearHighlights();
                _ui.ShowBigMessage("", 0);
                _ui.HideTargetingArrow();
            }
        }

        private void StartPendingResolution(PendingInteraction pending)
        {
            _currentPending = pending;
            _currentState = InputState.PendingTarget;

            GD.Print($"[INPUT] StartPendingResolution. Typ: {pending.RequiredTargetType}, Opcji: {pending.Options.Count}");

            if (pending.RequiredTargetType == TargetType.Choice)
            {
                _ui.HideTargetingArrow();

                // HEURISTIC: Tutor (Card Grid) vs Modal (Buttons)
                if (pending.Options.Count > 4)
                {
                    var deck = _latestGameState.PlayerA.DrawPile.ToList();

                    // Show Card Grid
                    _ui.ShowCardSelectionModal(deck, (index) =>
                    {
                        OnPlayerCommand?.Invoke(new SelectTargetCommand(_playerId, index));
                        _ui.HideChoiceModal();
                    });
                    return;
                }
                else
                {
                    // Show Buttons
                    var player = _latestGameState.PlayerA;
                    List<bool> optionValidity = new List<bool>();

                    foreach (var opt in pending.Options)
                    {
                        bool isValid = true;
                        // Basic text-based validation
                        if (opt.Contains("Discard", StringComparison.OrdinalIgnoreCase) && player.DiscardPile.Count == 0) isValid = false;
                        else if (opt.Contains("Deck", StringComparison.OrdinalIgnoreCase) && player.DrawPile.Count == 0) isValid = false;
                        optionValidity.Add(isValid);
                    }

                    _ui.ShowChoiceModal(pending.Options, (index) =>
                    {
                        OnPlayerCommand?.Invoke(new SelectTargetCommand(_playerId, index));
                        _ui.HideChoiceModal();
                    }, optionValidity);
                }
                return;
            }

            // Standard targeting interaction
            _ui.ShowBigMessage("WYBIERZ CEL EFEKTU", 0, Colors.Orange);
            _ui.HighlightTargets(pending.RequiredTargetType, _playerId);
        }

        #endregion

        #region Helpers

        private bool HasManualTarget(CardInstance c)
        {
            return c.Definition.Effects.Any(e => e.Actions.Any(a => IsManualTarget(a.Target)));
        }

        private bool IsManualTarget(TargetType t) =>
            t == TargetType.TargetEnemyUnit ||
            t == TargetType.TargetFriendlyUnit ||
            t == TargetType.SelectedTarget ||
            t == TargetType.OtherFriendlyUnits;

        private TargetType GetTargetTypeForCard(CardInstance c)
        {
            foreach (var e in c.Definition.Effects)
                foreach (var a in e.Actions)
                    if (IsManualTarget(a.Target)) return a.Target;
            return TargetType.SelectedTarget;
        }

        private bool CheckIfHasValidTargetsOnBoard(CardInstance card)
        {
            if (_latestGameState == null) return false;
            foreach (var effect in card.Definition.Effects)
            {
                if (effect.Trigger != TriggerType.OnPlayed) continue;
                var tType = effect.Targeting;

                if (tType == TargetType.Self)
                {
                    var action = effect.Actions.FirstOrDefault(a => IsManualTarget(a.Target));
                    if (action != null) tType = action.Target;
                }

                var targets = EffectTargetResolver.GetPotentialTargets(tType, _latestGameState, card.InstanceId);
                if (targets.Count > 0) return true;
            }
            return false;
        }

        #endregion

        #region Mulligan Logic

        public void StartMulligan(List<CardInstance> hand)
        {
            _currentState = InputState.Mulligan;
            _mulliganSelection.Clear();
            _mulliganHandCache = hand;
            _ui.ToggleMulliganPanel(true);
            _ui.UpdateMulliganCounter(0, MAX_MULLIGAN_SWAPS);
            _ui.RenderMulliganCards(hand, _mulliganSelection);
        }

        private void ProcessMulliganClick(CardInstance card)
        {
            int id = card.InstanceId;
            if (_mulliganSelection.Contains(id)) _mulliganSelection.Remove(id);
            else if (_mulliganSelection.Count < MAX_MULLIGAN_SWAPS) _mulliganSelection.Add(id);
            _ui.UpdateMulliganCounter(_mulliganSelection.Count, MAX_MULLIGAN_SWAPS);
            _ui.RenderMulliganCards(_mulliganHandCache, _mulliganSelection);
        }

        public void ConfirmMulligan()
        {
            if (_currentState != InputState.Mulligan) return;
            var cmd = new ConfirmMulliganCommand(_playerId, new List<int>(_mulliganSelection));
            OnPlayerCommand?.Invoke(cmd);
            _ui.ToggleMulliganPanel(false);
        }

        #endregion
    }
}