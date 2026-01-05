using Godot;
using System;
using CardGame.Core.Cards.Models;

public partial class CardSlot : PanelContainer
{
    [Signal] public delegate void CardDroppedInSlotEventHandler(int cardDefId, CardSlot slot);
    // Usuwamy sygnał usunięcia przez kliknięcie, bo robimy to przez panel biblioteki (-)
    // [Signal] public delegate void CardRemovedFromSlotEventHandler(CardSlot slot); 

    public CardView HostedCardView { get; private set; }
    public int? CardDefId { get; private set; }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop; // Musi łapać Drop
    }

    public void PlaceCard(CardView cardVisual, int cardId)
    {
        ClearSlot();
        CardDefId = cardId;
        HostedCardView = cardVisual;
        AddChild(HostedCardView);

        // Karta w slocie nie powinna być interaktywna
        HostedCardView.MouseFilter = MouseFilterEnum.Ignore;
    }

    public void ClearSlot()
    {
        if (HostedCardView != null)
        {
            HostedCardView.QueueFree();
            HostedCardView = null;
        }
        CardDefId = null;
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        return data.VariantType == Variant.Type.Int;
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        int cardId = (int)data;
        EmitSignal(SignalName.CardDroppedInSlot, cardId, this);
    }
}