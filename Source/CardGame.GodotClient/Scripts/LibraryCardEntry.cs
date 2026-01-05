using Godot;
using System;
using CardGame.Core.Cards.Models;

public partial class LibraryCardEntry : VBoxContainer
{
    [Export] public Control CardHolder;
    [Export] public Label CountLabel;
    [Export] public Button AddBtn;
    [Export] public Button RemoveBtn;

    public event Action<int> OnAddRequest;
    public event Action<int> OnRemoveRequest;

    private int _cardId;

    public override void _Ready()
    {
        if (AddBtn != null) AddBtn.Pressed += () => OnAddRequest?.Invoke(_cardId);
        if (RemoveBtn != null) RemoveBtn.Pressed += () => OnRemoveRequest?.Invoke(_cardId);
    }

    public void Setup(CardInstance card, PackedScene cardScene)
    {
        _cardId = card.InstanceId;

        foreach (Node child in CardHolder.GetChildren()) child.QueueFree();

        var cardView = cardScene.Instantiate<CardView>();
        CardHolder.AddChild(cardView);
        cardView.Render(card);

        // Ważne: Karta w bibliotece ma pozwalać na Drag&Drop, więc Stop.
        // Ponieważ usunęliśmy _GuiInput z tego skryptu, nie będzie konfliktu.
        cardView.MouseFilter = MouseFilterEnum.Stop;
    }

    public void UpdateCounter(int current, int max)
    {
        if (CountLabel != null)
        {
            CountLabel.Text = $"{current} / {max}";
            CountLabel.Modulate = current == max ? Colors.Green : (current > 0 ? Colors.Yellow : Colors.White);
        }
        if (AddBtn != null) AddBtn.Disabled = (current >= max);
        if (RemoveBtn != null) RemoveBtn.Disabled = (current <= 0);
    }
}