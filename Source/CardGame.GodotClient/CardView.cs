using Godot;
using CardGame.Core.Cards.Models;

public partial class CardView : Control
{
	[Export] public Label NameLabel;
	[Export] public Label CostLabel;

	// ZMIANA: Zamiast jednego StatsLabel, mamy dwa osobne
	[Export] public Label AttackLabel;
	[Export] public Label HealthLabel;

	public void Render(CardInstance card)
	{
		// Zabezpieczenie
		if (NameLabel == null || CostLabel == null || AttackLabel == null || HealthLabel == null)
		{
			// GD.PrintErr("CardView: Nie podpięto wszystkich Labeli!");
			return;
		}

		NameLabel.Text = card.Definition.Name;
		CostLabel.Text = card.CurrentStats.BloodCost.ToString();

		// ZMIANA: Przypisujemy wartości osobno
		AttackLabel.Text = card.CurrentStats.Attack.ToString();
		HealthLabel.Text = card.CurrentStats.Health.ToString();
	}
}
