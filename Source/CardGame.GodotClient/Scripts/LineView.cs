using Godot;
using System;
using CardGame.Core.State.Models;
using CardGame.Core.Cards.Models;

public partial class LineView : Control
{
    [Export] public Label InfoLabel;
    [Export] public Control EnemySlot;
    [Export] public Control PlayerSlot;

    [Signal] public delegate void CardDroppedOnLineEventHandler(int cardInstanceId, int lineIndex);

    private int _myIndex;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
    }

    // --- ZMIANA: Dodano parametr onClickHandler ---
    public void Render(Line lineData, PackedScene cardScene, Action<CardView> onClickHandler)
    {
        _myIndex = lineData.Index;
        if (InfoLabel != null) InfoLabel.Text = $"L{_myIndex}";

        ClearSlot(EnemySlot);
        ClearSlot(PlayerSlot);

        // 2. Wrogowie (Góra)
        if (lineData.Player2Unit != null)
        {
            var botUnit = cardScene.Instantiate<CardView>();
            EnemySlot.AddChild(botUnit);
            botUnit.Render(lineData.Player2Unit);

            botUnit.MouseFilter = MouseFilterEnum.Stop; // Musi być Stop, żeby odebrać kliknięcie
            botUnit.Modulate = new Color(1, 0.8f, 0.8f);

            // PODPINAMY KLIKNIĘCIE
            if (onClickHandler != null) botUnit.OnClicked += onClickHandler;
        }

        // 3. Gracz (Dół)
        if (lineData.Player1Unit != null)
        {
            var myUnit = cardScene.Instantiate<CardView>();
            PlayerSlot.AddChild(myUnit);
            myUnit.Render(lineData.Player1Unit);

            myUnit.MouseFilter = MouseFilterEnum.Stop;

            // PODPINAMY KLIKNIĘCIE
            if (onClickHandler != null) myUnit.OnClicked += onClickHandler;
        }
    }

    private void ClearSlot(Control slot)
    {
        if (slot == null) return;
        foreach (Node child in slot.GetChildren())
        {
            // Omijamy Ducha (rozpoznajemy po przezroczystości)
            if (child is CardView cv && cv.Modulate.A < 0.9f) continue;
            child.QueueFree();
        }
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        return data.VariantType == Variant.Type.Int;
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        int cardId = (int)data;
        EmitSignal(SignalName.CardDroppedOnLine, cardId, _myIndex);
    }
}