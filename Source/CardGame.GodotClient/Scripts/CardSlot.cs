using Godot;
using System;
using CardGame.Core.Cards.Models;

namespace CardGame.GodotClient
{
    /// <summary>
    /// Represents a visual container slot in the Deck Editor where a card can be dropped.
    /// Handles drag-and-drop detection and holds a reference to the displayed card.
    /// </summary>
    public partial class CardSlot : PanelContainer
    {
        /// <summary>
        /// Signal emitted when a valid card is dropped onto this slot.
        /// </summary>
        /// <param name="cardDefId">The definition ID of the dropped card.</param>
        /// <param name="slot">The slot instance that received the drop.</param>
        [Signal] public delegate void CardDroppedInSlotEventHandler(int cardDefId, CardSlot slot);

        // Previously removed signal for card removal logic
        // [Signal] public delegate void CardRemovedFromSlotEventHandler(CardSlot slot); 

        /// <summary>
        /// Gets the visual representation of the card currently hosted in this slot.
        /// </summary>
        public CardView HostedCardView { get; private set; }

        /// <summary>
        /// Gets the definition ID of the card currently in this slot, or null if empty.
        /// </summary>
        public int? CardDefId { get; private set; }

        /// <summary>
        /// Called when the node enters the scene tree.
        /// Sets the mouse filter to Stop to ensure drop events are captured.
        /// </summary>
        public override void _Ready()
        {
            MouseFilter = MouseFilterEnum.Stop; // Musi łapać Drop
        }

        /// <summary>
        /// Places a visual card instance into this slot, clearing any existing card first.
        /// </summary>
        /// <param name="cardVisual">The visual card node to display.</param>
        /// <param name="cardId">The definition ID of the card.</param>
        public void PlaceCard(CardView cardVisual, int cardId)
        {
            ClearSlot();
            CardDefId = cardId;
            HostedCardView = cardVisual;
            AddChild(HostedCardView);

            // Disable mouse interaction for the card inside the slot so the slot handles input
            HostedCardView.MouseFilter = MouseFilterEnum.Ignore;
        }

        /// <summary>
        /// Removes and frees the currently hosted card visual from this slot.
        /// </summary>
        public void ClearSlot()
        {
            if (HostedCardView != null)
            {
                HostedCardView.QueueFree();
                HostedCardView = null;
            }
            CardDefId = null;
        }

        /// <summary>
        /// Checks if the dragged data is compatible with this slot (expects an integer Card ID).
        /// </summary>
        public override bool _CanDropData(Vector2 atPosition, Variant data)
        {
            return data.VariantType == Variant.Type.Int;
        }

        /// <summary>
        /// Handles the drop event, emitting the CardDroppedInSlot signal with the card ID.
        /// </summary>
        public override void _DropData(Vector2 atPosition, Variant data)
        {
            int cardId = (int)data;
            EmitSignal(SignalName.CardDroppedInSlot, cardId, this);
        }
    }
}