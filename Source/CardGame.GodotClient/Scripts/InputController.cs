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

public partial class InputController : Node
{
    public enum InputState { Idle, Normal, TargetingCard, PendingTarget, Mulligan }

    private InputState _currentState = InputState.Idle;
    private int _playerId;
    private UIManager _ui;
    private GameState _latestGameState;

    private CardInstance _selectedCardHand;
    private int? _pendingUnitLineIdx;

    private PendingInteraction _currentPending;

    // Mulligan
    private List<int> _mulliganSelection = new List<int>();
    private const int MAX_MULLIGAN_SWAPS = 3;
    private List<CardInstance> _mulliganHandCache;

    public event Action<IGameCommand> OnPlayerCommand;

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

    public void UpdateState(GameState gameState)
    {
        _latestGameState = gameState;

        // 1. Priorytet: Mulligan
        if (gameState.CurrentPhase == CardGame.Core.State.Enums.GamePhase.Mulligan)
        {
            if (_currentState != InputState.Mulligan)
                StartMulligan(gameState.PlayerA.Hand.ToList());
            return;
        }

        // 2. Priorytet: Pending Interaction (Wybory/Cele)
        if (gameState.PendingInteraction != null)
        {
            // --- FIX: UKRYWANIE AKCJI BOTA ---
            // Jeśli gra czeka na decyzję, ale to tura Bota, nie wyświetlamy UI dla gracza.
            // Bot sam sobie poradzi przez AI Controller.
            if (gameState.ActivePlayerId != _playerId)
            {
                // Upewniamy się tylko, że UI gracza jest czyste
                if (_currentState != InputState.Idle)
                {
                    _currentState = InputState.Idle;
                    _ui.HideChoiceModal();
                    _ui.HideTargetingArrow();
                    _ui.ClearHighlights();
                }
                return;
            }
            // ---------------------------------

            // Strażnik przed pętlą (ten sam obiekt interaction)
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

        // Reset jeśli Pending zniknął
        if (_currentState == InputState.PendingTarget && gameState.PendingInteraction == null)
        {
            _currentPending = null;
            _currentState = InputState.Normal;
            _ui.HideChoiceModal();
            _ui.HideTargetingArrow();
            _ui.ClearHighlights();
            _ui.ShowBigMessage("", 0);
        }

        // 3. Normalna tura gracza
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

    public void HandleCardDrop(CardInstance card, int lineIdx)
    {
        if (_currentState != InputState.Normal) return;

        if (card.Definition.Type == CardType.Unit)
        {
            bool needsTarget = HasManualTarget(card);
            bool hasValidTargets = CheckIfHasValidTargetsOnBoard(card);

            if (needsTarget && hasValidTargets)
            {
                // Tryb celowania z Duchem
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

    private void TryPlayCardFromHand(CardInstance card, int? unitLineIdx)
    {
        bool needsTarget = HasManualTarget(card);
        bool hasValidTargets = CheckIfHasValidTargetsOnBoard(card);

        // Jeśli karta wymaga celu I są cele na stole
        if (needsTarget && hasValidTargets)
        {
            _currentState = InputState.TargetingCard;
            _selectedCardHand = card;
            _pendingUnitLineIdx = unitLineIdx;

            // --- FIX: DUCH TYLKO DLA JEDNOSTEK ---
            // Nie twórz ducha dla czarów, bo to zostawiało "kwadrat"
            if (card.Definition.Type == CardType.Unit && unitLineIdx.HasValue)
            {
                _ui.CreateGhostUnit(card, unitLineIdx.Value);
            }
            // -------------------------------------

            _ui.ShowBigMessage("WYBIERZ CEL", 0, Colors.Yellow);
            _ui.SetCancelButtonVisible(true);
            _ui.HighlightTargets(GetTargetTypeForCard(card), _playerId);
        }
        else
        {
            // Zagranie bez celowania (Global Spell lub Unit bez celu)
            if (card.Definition.Type == CardType.Spell)
            {
                // --- FIX: CZYSTE ZAGRANIE ---
                // Upewniamy się, że nie ma resztek UI przed wysłaniem
                CancelTargeting();
                OnPlayerCommand?.Invoke(new PlaySpellCommand(_playerId, card.InstanceId));
            }
            // Unity bez celu są obsłużone w HandleCardDrop
        }
    }

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

            // HEURYSTYKA: Tutor czy Modal?
            if (pending.Options.Count > 4)
            {
                var deck = _latestGameState.PlayerA.DrawPile.ToList();

                // Tutor (Siatka Kart)
                _ui.ShowCardSelectionModal(deck, (index) =>
                {
                    OnPlayerCommand?.Invoke(new SelectTargetCommand(_playerId, index));
                    _ui.HideChoiceModal();
                });
                return;
            }
            else
            {
                // Modal (Przyciski) - np. Expectancy
                var player = _latestGameState.PlayerA;
                List<bool> optionValidity = new List<bool>();

                foreach (var opt in pending.Options)
                {
                    bool isValid = true;
                    // Prosta walidacja po tekście (można rozbudować)
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

        // Zwykłe celowanie (np. 2 etap Final Mission)
        _ui.ShowBigMessage("WYBIERZ CEL EFEKTU", 0, Colors.Orange);
        _ui.HighlightTargets(pending.RequiredTargetType, _playerId);
    }

    // --- HELPERY ---
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

    // Mulligan...
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
}