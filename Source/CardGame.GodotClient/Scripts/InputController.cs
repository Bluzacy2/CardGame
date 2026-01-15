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
        // Rysuj strzałkę TYLKO jeśli celujemy w planszę
        // Jeśli mamy PendingTarget typu CHOICE (Menu), nie rysuj strzałki!
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
            // Prawy Przycisk: Anuluj
            if (mb.ButtonIndex == MouseButton.Right)
            {
                if (_currentState == InputState.TargetingCard) CancelTargeting();
            }
            // Lewy Przycisk: TERAZ OBSŁUGIWANY PRZEZ ON_CLICKED
            // Usuwamy stąd TrySelectTargetUnderMouse, bo kliknięcie w kartę
            // zostanie przechwycone przez Control (MouseFilter Stop) i nie dotrze tutaj.
            // Zostawiamy to puste lub obsługujemy kliknięcie w "pustkę" (anulowanie).
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
            // --- FIX: STRAŻNIK PRZED PĘTLĄ ---
            // Jeśli już jesteśmy w trakcie obsługi TEJ SAMEJ interakcji, nie rób nic!
            // Sprawdzamy czy stan to PendingTarget I czy obiekt interakcji jest ten sam (referencja)
            // (Jeśli w C# obiekty są różne co update, sprawdźmy chociaż typ i sourceId)

            bool isSameInteraction = _currentPending != null &&
                                     _currentPending.SourceCardInstanceId == gameState.PendingInteraction.SourceCardInstanceId &&
                                     _currentPending.RequiredTargetType == gameState.PendingInteraction.RequiredTargetType;

            if (_currentState == InputState.PendingTarget && isSameInteraction)
            {
                return; // JUŻ TO ROBIMY -> WYJDŹ
            }
            // ---------------------------------

            // Jeśli to nowa interakcja, zresetuj stare celowanie
            if (_currentState == InputState.TargetingCard) CancelTargeting();

            StartPendingResolution(gameState.PendingInteraction);
            return;
        }

        // Jeśli Pending zniknął (jest null), a my wciąż myślimy, że jest PendingTarget -> Wyjdź z tego stanu
        if (_currentState == InputState.PendingTarget && gameState.PendingInteraction == null)
        {
            _currentPending = null;
            _currentState = InputState.Normal;
            _ui.HideChoiceModal(); // Na wszelki wypadek zamykamy modal
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
        // 1. Mulligan (bez zmian)
        if (_currentState == InputState.Mulligan)
        {
            ProcessMulliganClick(card);
            return;
        }

        // 2. Wybieranie Celu (TO JEST NOWOŚĆ)
        // Jeśli jesteśmy w trybie celowania i kliknięto kartę...
        if (_currentState == InputState.TargetingCard || _currentState == InputState.PendingTarget)
        {
            // Nie pozwól wybrać Ducha jako celu
            if (_ui.GetGhostUnit() != null && _ui.GetGhostUnit().MyCardData == card) return;

            SubmitTarget(card.InstanceId);
            return;
        }

        // 3. Zagrywanie z ręki (Normal)
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
            // Sprawdzamy czy Unit wymaga celu (np. Polar Bear)
            bool needsTarget = HasManualTarget(card);
            bool hasValidTargets = CheckIfHasValidTargetsOnBoard(card);

            if (needsTarget && hasValidTargets)
            {
                // Tryb celowania z Duchem
                TryPlayCardFromHand(card, lineIdx);
            }
            else
            {
                // Graj natychmiast
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

        // Critical Thinking Fix: HasManualTarget zwróci false, więc wejdzie do else -> PlaySpellCommand

        if (needsTarget && CheckIfHasValidTargetsOnBoard(card))
        {
            _currentState = InputState.TargetingCard;
            _selectedCardHand = card;
            _pendingUnitLineIdx = unitLineIdx;

            // Tworzymy ducha TYLKO jeśli to Unit i mamy linię
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
            // Zagranie bez celowania (Critical Thinking, Final Mission bez celów itp.)
            if (card.Definition.Type == CardType.Spell)
            {
                OnPlayerCommand?.Invoke(new PlaySpellCommand(_playerId, card.InstanceId));
            }
            // Unity bez celu poszły w HandleCardDrop, tutaj nic nie robimy
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
            // Nie pozwól celować w ducha!
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

            // WAŻNE: Nie usuwamy Ducha tutaj. Zrobi to UIManager przy aktualizacji stanu (UpdateDisplay),
            // gdy prawdziwa jednostka pojawi się na stole.
            // _ui.RemoveGhostUnit(); <--- USUNIĘTE

            // Resetujemy tylko stan logiczny, UI wyczyści się przy UpdateDisplay
            _currentState = InputState.Normal;
            _selectedCardHand = null;
            _ui.HideTargetingArrow();
            _ui.SetCancelButtonVisible(false);
            _ui.ClearHighlights();
            _ui.ShowBigMessage("", 0);
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
        if (_currentState == InputState.PendingTarget && _currentPending == pending) return;

        _currentPending = pending;
        _currentState = InputState.PendingTarget;

        if (pending.RequiredTargetType == TargetType.Choice)
        {
            _ui.HideTargetingArrow();

            // --- WALIDACJA OPCJI ---
            // Ponieważ Core nie wysyła flagi "IsUsable", musimy to sprawdzić po stronie klienta.
            // Analizujemy tekst opcji i stan gry.

            var player = _latestGameState.PlayerA;
            List<bool> optionValidity = new List<bool>();

            foreach (var opt in pending.Options)
            {
                bool isValid = true;

                // Heurystyka: Szukamy słów kluczowych w opisie
                if (opt.Contains("Discard", StringComparison.OrdinalIgnoreCase))
                {
                    // Jeśli opcja dotyczy Discardu, sprawdź czy jest pusty
                    if (player.DiscardPile.Count == 0) isValid = false;
                }
                else if (opt.Contains("Deck", StringComparison.OrdinalIgnoreCase))
                {
                    // Jeśli opcja dotyczy Talii, sprawdź czy jest pusta
                    if (player.DrawPile.Count == 0) isValid = false;
                }

                optionValidity.Add(isValid);
            }

            // Przekazujemy listę walidacji do UI
            _ui.ShowChoiceModal(pending.Options, (index) =>
            {
                OnPlayerCommand?.Invoke(new SelectTargetCommand(_playerId, index));
                _ui.HideChoiceModal();
            }, optionValidity);

            return;
        }

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