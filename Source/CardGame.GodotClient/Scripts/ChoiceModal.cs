using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using CardGame.Core.Cards.Models; // Potrzebne do CardInstance

public partial class ChoiceModal : Control
{
    [Export] public Control ButtonContainer;
    [Export] public Label TitleLabel;

    // --- NOWE REFERENCJE (Przypisz w Inspektorze!) ---
    [Export] public ScrollContainer CardGridScroll;
    [Export] public Control CardGrid;

    private Action<int> _currentCallback;

    public override void _Ready()
    {
        Visible = false;
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
    }

    public void SetCallback(Action<int> callback)
    {
        _currentCallback = callback;
    }

    // Tryb 1: Przyciski (Expectancy)
    public void ShowOptions(IEnumerable<string> options, List<bool> enabledStates = null)
    {
        SetupView(mode: 0); // 0 = Buttons

        // ... (Twój istniejący kod pętli tworzenia przycisków) ...
        // ... (Wklej tu zawartość poprzedniej metody ShowOptions) ...
        // Pamiętaj tylko o dodaniu czyszczenia CardGrid:
        foreach (Node child in CardGrid.GetChildren()) child.QueueFree();

        // --- Skrócona wersja dla kontekstu (użyj swojej pełnej z poprzedniego kroku) ---
        Visible = true;
        MoveToFront();
        if (ButtonContainer == null) return;
        foreach (Node child in ButtonContainer.GetChildren()) child.QueueFree();

        int index = 0;
        var list = options.ToList();
        foreach (var txt in list)
        {
            var btn = new Button();
            btn.Text = txt;
            btn.CustomMinimumSize = new Vector2(300, 60);

            // Logika enabled...
            bool isEnabled = (enabledStates == null) || (index >= enabledStates.Count) || enabledStates[index];
            btn.Disabled = !isEnabled;

            int capture = index;
            btn.Pressed += () => OptionClicked(capture);
            ButtonContainer.AddChild(btn);
            index++;
        }
    }

    // Tryb 2: Siatka Kart (Critical Thinking)
    public void ShowCardGrid(List<CardInstance> cards, PackedScene cardTemplate)
    {
        SetupView(mode: 1); // Włączamy widok Grid

        // DIAGNOSTYKA
        if (CardGrid == null)
        {
            GD.PrintErr("CRITICAL: CardGrid is null! Przypisz go w Inspektorze w ChoiceModal.tscn");
            return;
        }
        if (cardTemplate == null)
        {
            GD.PrintErr("CRITICAL: cardTemplate is null! UIManager nie przekazał szablonu karty.");
            return;
        }

        GD.Print($"[MODAL] Wyświetlam Grid. Liczba kart: {cards.Count}");

        // Czyścimy stare dzieci
        foreach (Node child in CardGrid.GetChildren())
            child.QueueFree();

        int index = 0;
        foreach (var card in cards)
        {
            try
            {
                // BEZPIECZNE INSTANCJONOWANIE (Bez generyka <T>)
                var node = cardTemplate.Instantiate();
                var cardView = node as CardView;

                if (cardView == null)
                {
                    GD.PrintErr($"[MODAL] Błąd: Scena karty nie zawiera skryptu CardView! Node type: {node.GetType().Name}");
                    continue;
                }

                CardGrid.AddChild(cardView);

                // Konfiguracja karty
                cardView.Render(card);
                cardView.CustomMinimumSize = new Vector2(140, 190); // Wymuś rozmiar

                // Mysz musi działać
                cardView.MouseFilter = MouseFilterEnum.Stop;

                // Callback
                int capturedIndex = index;
                cardView.OnClicked += (cv) => OptionClicked(capturedIndex);

                // Opcjonalnie: Wyłączamy mechanikę Drag&Drop w oknie wyboru, żeby nie psuć UI
                // (Wymagałoby dodania flagi w CardView, ale na razie zostawmy)

                index++;
            }
            catch (Exception e)
            {
                GD.PrintErr($"[MODAL] Wyjątek przy tworzeniu karty: {e.Message}");
            }
        }

        // Wymuszenie przeliczenia układu
        if (CardGrid is Container c) c.QueueSort();
    }

    private void SetupView(int mode)
    {
        Visible = true;
        MoveToFront();

        if (mode == 0) // Buttons
        {
            if (ButtonContainer != null) ButtonContainer.Visible = true;
            if (CardGridScroll != null) CardGridScroll.Visible = false;
            TitleLabel.Text = "WYBIERZ OPCJĘ";
        }
        else // Grid
        {
            if (ButtonContainer != null) ButtonContainer.Visible = false;
            if (CardGridScroll != null) CardGridScroll.Visible = true;
            TitleLabel.Text = "WYBIERZ KARTĘ Z TALII";
        }
    }

    private void OptionClicked(int index)
    {
        Visible = false;
        _currentCallback?.Invoke(index);
    }

    public void HideModal()
    {
        Visible = false;
    }
}