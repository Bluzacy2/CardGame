using Godot;
using CardGame.Core.Cards.Models;

public partial class CardView : Control
{
	[Export] public Label NameLabel;
	[Export] public Label CostLabel;
	[Export] public Label HpLabel;
	[Export] public Label AtkLabel;
	[Export] public Button SelectButton; // <--- NOWE

	// Sygnał: "Ktoś mnie kliknął!" (wysyła samą siebie - dane karty)
	[Signal] public delegate void CardClickedEventHandler(CardView visual);

	public CardInstance MyCardData { get; private set; } // Żebyśmy wiedzieli co to za karta

	public override void _Ready()
	{
		// Przekazujemy kliknięcie dalej
		if(SelectButton != null)
			SelectButton.Pressed += () => EmitSignal(SignalName.CardClicked, this);
	}

	public void Render(CardInstance card)
	{
		MyCardData = card; // Zapamiętujemy dane!

		// Twoje labele (dostosuj do tego jak je podzieliłeś)
		if (NameLabel != null) NameLabel.Text = card.Definition.Name;
		if (CostLabel != null) CostLabel.Text = card.CurrentStats.BloodCost.ToString();
		
		// Zakładam że tak to podzieliłeś:
		if (AtkLabel != null) AtkLabel.Text = card.CurrentStats.Attack.ToString();
		if (HpLabel != null) HpLabel.Text = card.CurrentStats.Health.ToString();
	}
}
