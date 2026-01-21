using Godot;
using System;
using CardGame.Core.Cards.Models;
/// <summary>
/// Represents a visual slot in the UI where a card can be placed or dropped.
/// Handles drag-and-drop interactions and card hosting.
/// </summary>
public partial class CardSlot : PanelContainer
{
    /// <summary>
    /// Event emitted when a card is successfully dropped into this slot via drag-and-drop.
    /// </summary>
    /// <param name="cardDefId">The definition ID of the dropped card.</param>
    /// <param name="slot">The slot instance receiving the card.</param>
    [Signal] public delegate void CardDroppedInSlotEventHandler(int cardDefId, CardSlot slot);
    /// <summary>
    /// Gets the visual card instance currently hosted in this slot.
    /// </summary>
    public CardView HostedCardView { get; private set; }
    /// <summary>
    /// Gets the definition ID of the card currently hosted in this slot.
    /// </summary>
    public int? CardDefId { get; private set; }
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop; // Musi łapać Drop
    }
    /// <summary>
    /// Places a card visual into this slot, clearing any existing content.
    /// </summary>
    /// <param name="cardVisual">The CardView instance to display.</param>
    /// <param name="cardId">The definition ID of the card.</param>
    public void PlaceCard(CardView cardVisual, int cardId)
    {
        ClearSlot();
        CardDefId = cardId;
        HostedCardView = cardVisual;
        AddChild(HostedCardView);

        HostedCardView.MouseFilter = MouseFilterEnum.Ignore;
    }
    /// <summary>
    /// Removes the currently hosted card from this slot.
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
    /// Determines whether the dragged data can be dropped onto this slot.
    /// Accepts integers representing card definition IDs.
    /// </summary>
    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        return data.VariantType == Variant.Type.Int;
    }
    /// <summary>
    /// Handles the drop event, emitting a signal with the dropped card's ID.
    /// </summary>
    public override void _DropData(Vector2 atPosition, Variant data)
    {
        int cardId = (int)data;
        EmitSignal(SignalName.CardDroppedInSlot, cardId, this);
    }
}