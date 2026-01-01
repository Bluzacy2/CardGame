using Godot;
using System;
using CardGame.Core.State.Models;
using CardGame.Core.Cards.Models;

public partial class LineView : Control
{
    [Export] public Label InfoLabel;
    [Export] public BoxContainer UnitsContainer;

    // Usunęliśmy ClickArea (przycisk), bo teraz reagujemy na Drop na całym panelu

    // Nowy sygnał: Karta (ID) została upuszczona na tę linię (Index)
    [Signal] public delegate void CardDroppedOnLineEventHandler(int cardInstanceId, int lineIndex);

    private int _myIndex;
    public override void _Ready()
    {
        // WYMUSZENIE ROZMIARU LINII
        // Rozciągnij na szerokość (x=0 w VBox oznacza fill), wysokość 130
        CustomMinimumSize = new Vector2(0, 130);
    }
    public void Render(Line lineData, PackedScene cardScene)
    {
        _myIndex = lineData.Index;
        if (InfoLabel != null) InfoLabel.Text = $"L{_myIndex}";
        _myIndex = lineData.Index;
        if (InfoLabel != null) InfoLabel.Text = $"L{_myIndex}";

        // --- ZABEZPIECZENIE ---
        if (UnitsContainer == null)
        {
            GD.PrintErr($"[BŁĄD KRYTYCZNY] LineView (L{_myIndex}) nie ma przypisanego UnitsContainer w Inspektorze! Otwórz Line.tscn i napraw to.");
            return;
        }
        // Czyścimy kontener
        foreach (Node child in UnitsContainer.GetChildren()) child.QueueFree();

        // --- 1. JEDNOSTKA WROGA (GÓRA) ---
        if (lineData.Player2Unit != null)
        {
            var botUnit = cardScene.Instantiate<CardView>();
            UnitsContainer.AddChild(botUnit);
            botUnit.Render(lineData.Player2Unit);
            botUnit.MouseFilter = MouseFilterEnum.Ignore; // Nie ruszamy kart wroga
            botUnit.Modulate = new Color(1, 0.8f, 0.8f); // Lekko czerwonawy odcień
        }
        else
        {
            // Puste miejsce (placeholder), żeby układ się nie rozjechał
            var spacer = new Control();
            spacer.CustomMinimumSize = new Vector2(140, 190); // Rozmiar karty
            spacer.MouseFilter = MouseFilterEnum.Ignore;
            UnitsContainer.AddChild(spacer);
        }

        // --- 2. SEPARATOR (Opcjonalny, bo VBox ma Separation) ---
        // Możemy dodać np. linię podziału (ColorRect)
        var splitLine = new ColorRect();
        splitLine.CustomMinimumSize = new Vector2(100, 2);
        splitLine.Color = new Color(1, 1, 1, 0.3f);
        UnitsContainer.AddChild(splitLine);

        // --- 3. JEDNOSTKA GRACZA (DÓŁ) ---
        if (lineData.Player1Unit != null)
        {
            var myUnit = cardScene.Instantiate<CardView>();
            UnitsContainer.AddChild(myUnit);
            myUnit.Render(lineData.Player1Unit);
            myUnit.MouseFilter = MouseFilterEnum.Ignore; // Na stole nie ciągamy
        }
        else
        {
            // To jest strefa zrzutu - musi być pusta, ale zajmować miejsce
            // W VBoxie myszka i tak trafi w tło LineView, więc nie musimy tu nic robić,
            // ale dla estetyki dodajmy pusty placeholder
            var spacer = new Control();
            spacer.CustomMinimumSize = new Vector2(140, 190);
            spacer.MouseFilter = MouseFilterEnum.Ignore;
            UnitsContainer.AddChild(spacer);
        }
    }

    // --- NOWOŚĆ: DRAG & DROP (Mechanika upuszczania) ---

    // 1. Czy możemy tu upuścić to, co trzyma myszka?
    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        // Sprawdzamy, czy to int (ID karty)
        bool isCard = data.VariantType == Variant.Type.Int;

        if (isCard)
        {
            // TO JEST WAŻNE: Jeśli to widzisz w konsoli machając nad linią, to strefa zrzutu działa!
            // Uwaga: To może spamować konsolę, więc patrz uważnie.
            // GD.Print($"[DEBUG] Karta nad linią {_myIndex}"); 
            return true;
        }
        return false;
    }

    // 2. Co się dzieje, gdy puścimy myszkę?
    public override void _DropData(Vector2 atPosition, Variant data)
    {
        int cardId = (int)data;
        GD.Print($"[DEBUG] UPUSZCZONO kartę {cardId} na linię {_myIndex}!"); // <--- CZY TO WIDZISZ?

        // Emitujemy sygnał
        EmitSignal(SignalName.CardDroppedOnLine, cardId, _myIndex);
    }
}