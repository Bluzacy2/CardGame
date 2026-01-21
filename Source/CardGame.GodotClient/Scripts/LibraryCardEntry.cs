using Godot;
using System;
using CardGame.Core.Cards.Models;
/// <summary>
/// Represents a single entry in the card library UI (e.g., in Deck Editor).
/// Displays a card preview and controls for adding/removing copies from the deck.
/// </summary>
public partial class LibraryCardEntry : VBoxContainer
{
    [Export] public Control CardHolder;
    [Export] public Label CountLabel;
    [Export] public Button AddBtn;
    [Export] public Button RemoveBtn;
    /// <summary>
    /// Event triggered when the user requests to add this card to the deck.
    /// </summary>
    public event Action<int> OnAddRequest;
    /// <summary>
    /// Event triggered when the user requests to remove this card from the deck.
    /// </summary>
    public event Action<int> OnRemoveRequest;

    private int _cardId;

    public override void _Ready()
    {
        if (AddBtn != null) AddBtn.Pressed += () => OnAddRequest?.Invoke(_cardId);
        if (RemoveBtn != null) RemoveBtn.Pressed += () => OnRemoveRequest?.Invoke(_cardId);
    }
    /// <summary>
    /// Initializes the library entry with card data and visual template.
    /// </summary>
    /// <param name="card">The card instance data.</param>
    /// <param name="cardScene">The PackedScene template for creating the card visual.</param>
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
    /// <summary>
    /// Updates the UI counter displaying current copies in deck vs maximum allowed.
    /// </summary>
    /// <param name="current">Current number of copies in deck.</param>
    /// <param name="max">Maximum allowed copies.</param>
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