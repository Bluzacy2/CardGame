using Godot;
using System;
using CardGame.Core.Cards.Models;

namespace CardGame.GodotClient
{
    /// <summary>
    /// Represents a single card entry in the Deck Editor's library list.
    /// Displays the card visual and provides buttons to add or remove copies from the deck.
    /// </summary>
    public partial class LibraryCardEntry : VBoxContainer
    {
        #region UI References
        /// <summary>Container where the card visual is instantiated.</summary>
        [Export] public Control CardHolder;
        /// <summary>Label displaying the current count of this card in the deck (e.g., "2/3").</summary>
        [Export] public Label CountLabel;
        /// <summary>Button to add a copy of this card to the deck.</summary>
        [Export] public Button AddBtn;
        /// <summary>Button to remove a copy of this card from the deck.</summary>
        [Export] public Button RemoveBtn;
        #endregion

        #region Events
        /// <summary>Event triggered when the Add button is clicked.</summary>
        public event Action<int> OnAddRequest;
        /// <summary>Event triggered when the Remove button is clicked.</summary>
        public event Action<int> OnRemoveRequest;
        #endregion

        private int _cardId;

        /// <summary>
        /// Called when the node enters the scene tree. Connects button signals to events.
        /// </summary>
        public override void _Ready()
        {
            if (AddBtn != null) AddBtn.Pressed += () => OnAddRequest?.Invoke(_cardId);
            if (RemoveBtn != null) RemoveBtn.Pressed += () => OnRemoveRequest?.Invoke(_cardId);
        }

        /// <summary>
        /// Initializes the entry with a specific card instance and visual template.
        /// </summary>
        /// <param name="card">The card data to display.</param>
        /// <param name="cardScene">The PackedScene used to instantiate the card visual.</param>
        public void Setup(CardInstance card, PackedScene cardScene)
        {
            _cardId = card.InstanceId;

            foreach (Node child in CardHolder.GetChildren()) child.QueueFree();

            var cardView = cardScene.Instantiate<CardView>();
            CardHolder.AddChild(cardView);
            cardView.Render(card);

            // Ensure the card can be dragged from the library (MouseFilter Stop allows drag detection)
            // Since _GuiInput was removed from this script, there is no conflict.
            cardView.MouseFilter = MouseFilterEnum.Stop;
        }

        /// <summary>
        /// Updates the UI counter and enables/disables buttons based on deck limits.
        /// </summary>
        /// <param name="current">Current number of copies in the deck.</param>
        /// <param name="max">Maximum allowed copies of this card.</param>
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
}