using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using CardGame.Core.Cards.Models;
using CardGame.Core.Cards.Data;
using CardGame.Core.Commands.Implementations;
using CardGame.Core.Cards.Logic; // Do EffectTargetResolver
using CardGame.Core.Commands.Interfaces;

public partial class InputController : Node
{
    private enum InputState { Normal, TargetingSpell, Mulligan }
    private InputState _currentState = InputState.Normal;

    private CardInstance _spellBeingCast;
    private int _playerId;
    private UIManager _ui; // Potrzebne do rysowania strzałki i podświetlania

    private List<int> _mulliganSelection = new List<int>();
    private const int MAX_MULLIGAN_SWAPS = 3;
    private List<CardInstance> _currentHandCache;

    public event Action<IGameCommand> OnPlayerCommand;

    public void Initialize(int playerId, UIManager uiManager)
    {
        _playerId = playerId;
        _ui = uiManager;
    }

    public override void _Process(double delta)
    {
        if (_currentState == InputState.TargetingSpell)
        {
            UpdateTargetingArrow();
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_currentState == InputState.TargetingSpell)
        {
            if (@event is InputEventMouseButton mb && mb.Pressed)
            {
                if (mb.ButtonIndex == MouseButton.Right) CancelTargeting();
                else if (mb.ButtonIndex == MouseButton.Left) TrySelectTarget();
            }
        }
    }

    // --- API publiczne (wywoływane przez sygnały z UI) ---

    public void HandleCardDrop(CardInstance card, int lineIdx)
    {
        if (card.Definition.Type == CardType.Unit)
        {
            OnPlayerCommand?.Invoke(new PlayUnitCommand(_playerId, card.InstanceId, lineIdx));
        }
        else if (card.Definition.Type == CardType.Spell)
        {
            TryStartTargeting(card);
        }
    }

    public void StartMulligan(List<CardInstance> hand)
    {
        _currentState = InputState.Mulligan;
        _mulliganSelection.Clear();
        _currentHandCache = hand;

        _ui.ToggleMulliganPanel(true);
        _ui.UpdateMulliganCounter(0, MAX_MULLIGAN_SWAPS);
        _ui.RenderMulliganCards(hand, _mulliganSelection);
        _ui.HandContainer.Visible = false; // Ukryj dolną rękę
    }

    public void ConfirmMulligan()
    {
        if (_currentState != InputState.Mulligan) return;

        // 1. Wyślij komendę
        var cmd = new ConfirmMulliganCommand(_playerId, new List<int>(_mulliganSelection));
        OnPlayerCommand?.Invoke(cmd);

        // 2. Posprzątaj (Bootstrap lub Engine zmieni fazę, my resetujemy UI)
        _currentState = InputState.Normal;
        _ui.ToggleMulliganPanel(false);
        _ui.HandContainer.Visible = true;
    }

    // Ta metoda jest wywoływana przez sygnał z UI -> Bootstrap -> InputController
    public void HandleCardClick(CardInstance card)
    {
        if (_currentState == InputState.Mulligan)
        {
            ProcessMulliganClick(card);
        }
        else if (card.Definition.Type == CardType.Spell)
        {
            TryStartTargeting(card);
        }
    }
    private void ProcessMulliganClick(CardInstance card)
    {
        int id = card.InstanceId;

        if (_mulliganSelection.Contains(id))
        {
            _mulliganSelection.Remove(id);
        }
        else
        {
            if (_mulliganSelection.Count < MAX_MULLIGAN_SWAPS)
            {
                _mulliganSelection.Add(id);
            }
        }

        // Odśwież widok w panelu mulliganu
        _ui.UpdateMulliganCounter(_mulliganSelection.Count, MAX_MULLIGAN_SWAPS);
        _ui.RenderMulliganCards(_currentHandCache, _mulliganSelection);
    }

    public void CancelTargeting()
    {
        _currentState = InputState.Normal;
        _spellBeingCast = null;
        _ui.HideTargetingArrow();
        _ui.SetCancelButtonVisible(false);
        _ui.ClearHighlights();
        _ui.ShowBigMessage("", 0); // Ukryj "WYBIERZ CEL" (lub przywróć poprzedni stan)
    }

    // --- Logika Wewnętrzna ---

    private void TryStartTargeting(CardInstance card)
    {
        if (HasManualTarget(card))
        {
            _currentState = InputState.TargetingSpell;
            _spellBeingCast = card;

            _ui.SetCancelButtonVisible(true);
            _ui.ShowBigMessage("WYBIERZ CEL", 0, Colors.Yellow);
            _ui.HighlightValidTargets(card, _playerId); // Metoda w UI (już ją masz, ew. trzeba dopasować argumenty)
        }
        else
        {
            // Czar bez celu (AoE)
            OnPlayerCommand?.Invoke(new PlaySpellCommand(_playerId, card.InstanceId));
        }
    }

    private void UpdateTargetingArrow()
    {
        // Zakładamy środek dołu ekranu
        Vector2 start = new Vector2(GetViewport().GetVisibleRect().Size.X / 2, GetViewport().GetVisibleRect().Size.Y - 100);
        _ui.UpdateTargetingArrow(start, GetViewport().GetMousePosition());
    }

    private void TrySelectTarget()
    {
        var mousePos = GetViewport().GetMousePosition();
        var targetView = _ui.FindCardUnderMouse(mousePos);

        if (targetView != null && targetView.MyCardData != null)
        {
            // Sprawdzenie poprawności celu można zrobić tu, albo zaufać silnikowi (który odrzuci błędny cel)
            // Wyślij komendę
            OnPlayerCommand?.Invoke(new PlaySpellCommand(_playerId, _spellBeingCast.InstanceId, targetView.MyCardData.InstanceId));
            CancelTargeting();
        }
    }

    private bool HasManualTarget(CardInstance c)
    {
        // Ta logika powtarza się, można by ją przenieść do Core/Helpers
        return c.Definition.Effects.Any(e => e.Actions.Any(a =>
            a.Target == TargetType.TargetEnemyUnit ||
            a.Target == TargetType.TargetFriendlyUnit ||
            a.Target == TargetType.SelectedTarget));
    }
}