using Godot;
using System;
using System.Linq;
using CardGame.Core.Cards.Models;
using CardGame.Core.Cards.Data;

public partial class CardView : Control
{
	[ExportGroup("UI Elements")]
	[Export] public Label NameLabel;
	[Export] public Label CostLabel;
	[Export] public Label HpLabel;
	[Export] public Label AtkLabel;
	[Export] public RichTextLabel DescLabel;
	[Export] public ColorRect Background;
	[Export] public Control HighlightBorder;
	[Export] public HBoxContainer KeywordContainer;
	[Export] public Control XOverlay;
	[Export] public Label CountBadge;

	public CardInstance MyCardData { get; private set; }
	// NOWE ZDARZENIE
	public event Action<CardView> OnClicked;
	public override void _Ready()
	{
		// Wymuszamy rozmiar i filtr myszy, żeby Drag&Drop działał
		CustomMinimumSize = new Vector2(140, 190);
		MouseFilter = MouseFilterEnum.Stop;
	}
	public override void _GuiInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
		{
			// Wywołaj zdarzenie, że ktoś kliknął
			OnClicked?.Invoke(this);
		}
	}
	public void Render(CardInstance card)
	{
		MyCardData = card;

		// 1. Podstawowe teksty
		if (NameLabel != null) NameLabel.Text = card.Definition.Name;

		// ZAKOMENTOWANE TYMCZASOWO (Brak pola Description w CardDefinition w Core)
		// if (DescLabel != null) DescLabel.Text = card.Definition.Description;

		// 2. Statystyki z kolorowaniem zmian
		if (CostLabel != null)
		{
			CostLabel.Text = card.CurrentStats.BloodCost.ToString();
			// Zielony jak tańszy, Czerwony jak droższy
			CostLabel.Modulate = card.CurrentStats.BloodCost < card.Definition.BaseStats.BloodCost ? Colors.Green : Colors.White;
		}

		if (card.Definition.Type == CardType.Unit)
		{
			ShowUnitStats(true);
			if (AtkLabel != null)
			{
				AtkLabel.Text = card.CurrentStats.Attack.ToString();
				AtkLabel.Modulate = card.CurrentStats.Attack > card.Definition.BaseStats.Attack ? Colors.Green : Colors.White;
			}
			if (HpLabel != null)
			{
				HpLabel.Text = card.CurrentStats.Health.ToString();
				// Czerwony jak ranny, Zielony jak buffowany
				if (card.DamageTaken > 0) HpLabel.Modulate = Colors.Red;
				else if (card.CurrentStats.Health > card.Definition.BaseStats.Health) HpLabel.Modulate = Colors.Green;
				else HpLabel.Modulate = Colors.White;
			}
			if (Background != null) Background.Color = new Color(0.2f, 0.2f, 0.2f);
		}
		else
		{
			ShowUnitStats(false);
			if (Background != null) Background.Color = new Color(0.1f, 0.1f, 0.4f);
		}

		// 3. Keywords (Wizualizacja Taunt/Shield itp.)
		RenderKeywords(card);
	}

	public void RenderCardBack()
	{
		MyCardData = null; // Blokada interakcji
		ShowUnitStats(false);
		if (NameLabel != null) NameLabel.Visible = false;
		if (DescLabel != null) DescLabel.Visible = false;
		if (CostLabel != null) CostLabel.Visible = false;
		if (Background != null) Background.Color = new Color(0.3f, 0.15f, 0.05f); // Brązowy
	}
	public void SetMulliganSelected(bool selected)
	{
		if (XOverlay != null) XOverlay.Visible = selected;

		// Opcjonalnie: Przyciemnij kartę
		Modulate = selected ? new Color(0.6f, 0.6f, 0.6f) : new Color(1, 1, 1);
	}
	public void SetHighlight(bool active, Color color)
	{
		if (HighlightBorder != null)
		{
			HighlightBorder.Visible = active;
			if (active) HighlightBorder.Modulate = color;
		}
	}

	private void RenderKeywords(CardInstance card)
	{
		// Tutaj w przyszłości dodasz ikonki w KeywordContainer
		// Na razie prosta zmiana ramki dla Taunta (SoulGuard)
		if (card.CurrentStats.Keywords.Contains(Keyword.SoulGuard))
		{
			SetHighlight(true, Colors.Gray); // Szara tarcza
		}
		else if (card.CurrentStats.Keywords.Contains(Keyword.Marked))
		{
			SetHighlight(true, Colors.Red); // Czerwony celownik
		}
		else
		{
			SetHighlight(false, Colors.White);
		}
	}

	private void ShowUnitStats(bool show)
	{
		if (AtkLabel != null) AtkLabel.Visible = show;
		if (HpLabel != null) HpLabel.Visible = show;
	}

	// --- DRAG DATA ---
	public override Variant _GetDragData(Vector2 atPosition)
	{
		if (MyCardData == null) return default;

		// 1. Tworzymy kontener na podgląd
		var preview = new Control();

		// 2. Klonujemy kartę
		var visual = (Control)Duplicate();

		// 3. Dodajemy do drzewa (to uruchomi _Ready klona, który ustawi MouseFilter na Stop)
		preview.AddChild(visual);

		// 4. TERAZ wymuszamy Ignore (nadpisujemy to, co zrobiło _Ready)
		// Dzięki temu podgląd nie będzie blokował upuszczenia karty!
		visual.MouseFilter = MouseFilterEnum.Ignore;

		// 5. Ustawiamy wygląd (wyśrodkowanie pod myszką)
		visual.Position = new Vector2(-70, -95);
		visual.Modulate = new Color(1, 1, 1, 0.8f); // Lekka przezroczystość
		visual.RotationDegrees = 5; // Lekki obrót

		// 6. Ustawiamy jako oficjalny podgląd drag&drop
		SetDragPreview(preview);

		return MyCardData.InstanceId;
	}
	public void UpdateCount(int current, int max)
	{
		if (CountBadge == null) return;

		CountBadge.Visible = true;
		CountBadge.Text = $"{current}/{max}";

		// Kolorowanie: Czerwony jeśli limit osiągnięty
		CountBadge.Modulate = current >= max ? Colors.Red : Colors.White;

		// Opcjonalnie: Przyciemnij kartę jeśli limit osiągnięty
		Modulate = current >= max ? new Color(0.5f, 0.5f, 0.5f) : Colors.White;
	}
}
