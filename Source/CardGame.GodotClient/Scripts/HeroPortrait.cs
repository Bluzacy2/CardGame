using Godot;
using System;

public partial class HeroPortrait : Control
{
    [Export] public Label HpLabel;
    [Export] public Label ManaLabel;
    [Export] public TextureRect Avatar;
    [Export] public Control HighlightBorder;

    // ID gracza, którego ten portret reprezentuje (1 lub 2)
    public int OwnerId { get; set; }

    // Zdarzenie kliknięcia (dla targetowania czarami w bohatera)
    public event Action<int> OnHeroClicked;

    public override void _Ready()
    {
        if (HighlightBorder != null) HighlightBorder.Visible = false;

        // Obsługa kliknięcia (wymaga, aby Control miał MouseFilter = Stop)
        this.GuiInput += (eventData) =>
        {
            if (eventData is InputEventMouseButton mb
                && mb.Pressed
                && mb.ButtonIndex == MouseButton.Left)
            {
                OnHeroClicked?.Invoke(OwnerId);
            }
        };
    }

    public void UpdateStats(int hp, int currentMana, int maxMana)
    {
        if (HpLabel != null)
        {
            HpLabel.Text = hp.ToString();
            // Prosta wizualizacja niskiego HP
            HpLabel.Modulate = hp <= 10 ? Colors.Red : Colors.White;
        }

        if (ManaLabel != null)
        {
            ManaLabel.Text = $"{currentMana}/{maxMana}";
        }
    }

    public void SetHighlight(bool active, Color color)
    {
        if (HighlightBorder != null)
        {
            HighlightBorder.Visible = active;
            HighlightBorder.Modulate = color;
        }
    }

    // Metoda pomocnicza dla Drop Data (jeśli w przyszłości będziesz przeciągać kartę na bohatera)
    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        return data.VariantType == Variant.Type.Int; // ID karty
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        int cardId = (int)data;
        // W przyszłości można tu dodać emitowanie sygnału o zagraniu jednostki/czaru na bohatera
        GD.Print($"[UI] Upuszczono kartę {cardId} na bohatera {OwnerId}");
    }
}