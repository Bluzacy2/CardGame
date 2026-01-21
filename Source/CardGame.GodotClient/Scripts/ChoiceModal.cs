using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using CardGame.Core.Cards.Models; 
/// <summary>
/// UI Component responsible for displaying modal choices to the player.
/// Supports both text-based options (buttons) and card selection grids (tutor).
/// </summary>
public partial class ChoiceModal : Control
{
	[Export] public Control ButtonContainer;
	[Export] public Label TitleLabel;
	[Export] public ScrollContainer CardGridScroll;
	[Export] public Control CardGrid;

	private Action<int> _currentCallback;

	public override void _Ready()
	{
		Visible = false;
		SetAnchorsPreset(LayoutPreset.FullRect);
		MouseFilter = MouseFilterEnum.Stop;
	}
    /// <summary>
    /// Sets the callback function to be executed when an option is selected.
    /// </summary>
    /// <param name="callback">Action receiving the selected index.</param>
    public void SetCallback(Action<int> callback)
	{
		_currentCallback = callback;
	}
    /// <summary>
    /// Displays a list of text-based options as buttons (e.g., for modal spells like 'Expectancy').
    /// </summary>
    /// <param name="options">List of option text labels.</param>
    /// <param name="enabledStates">Optional list of booleans enabling/disabling specific options.</param>
    public void ShowOptions(IEnumerable<string> options, List<bool> enabledStates = null)
	{
		SetupView(mode: 0); // 0 = Buttons
		foreach (Node child in CardGrid.GetChildren()) child.QueueFree();
		Visible = true;
		MoveToFront();
		if (ButtonContainer == null) return;
		foreach (Node child in ButtonContainer.GetChildren()) child.QueueFree();

		int index = 0;
		var list = options.ToList();
		foreach (var txt in list)
		{
			var btn = new Button();
			btn.Text = txt;
			btn.CustomMinimumSize = new Vector2(300, 60);

			bool isEnabled = (enabledStates == null) || (index >= enabledStates.Count) || enabledStates[index];
			btn.Disabled = !isEnabled;

			int capture = index;
			btn.Pressed += () => OptionClicked(capture);
			ButtonContainer.AddChild(btn);
			index++;
		}
	}

    /// <summary>
    /// Displays a grid of cards for selection (e.g., for Tutor effects like 'Critical Thinking').
    /// </summary>
    /// <param name="cards">List of card instances to display.</param>
    /// <param name="cardTemplate">The scene template used to instantiate card visuals.</param>
    public void ShowCardGrid(List<CardInstance> cards, PackedScene cardTemplate)
	{
		SetupView(mode: 1); 


		if (CardGrid == null)
		{
			GD.PrintErr("CRITICAL: CardGrid is null! Przypisz go w Inspektorze w ChoiceModal.tscn");
			return;
		}
		if (cardTemplate == null)
		{
			GD.PrintErr("CRITICAL: cardTemplate is null! UIManager nie przekazał szablonu karty.");
			return;
		}

		GD.Print($"[MODAL] Wyświetlam Grid. Liczba kart: {cards.Count}");

		foreach (Node child in CardGrid.GetChildren())
			child.QueueFree();

		int index = 0;
		foreach (var card in cards)
		{
			try
			{
				var node = cardTemplate.Instantiate();
				var cardView = node as CardView;

				if (cardView == null)
				{
					GD.PrintErr($"[MODAL] Błąd: Scena karty nie zawiera skryptu CardView! Node type: {node.GetType().Name}");
					continue;
				}

				CardGrid.AddChild(cardView);

				cardView.Render(card);
				cardView.CustomMinimumSize = new Vector2(140, 190);

				cardView.MouseFilter = MouseFilterEnum.Stop;

				int capturedIndex = index;
				cardView.OnClicked += (cv) => OptionClicked(capturedIndex);

				index++;
			}
			catch (Exception e)
			{
				GD.PrintErr($"[MODAL] Wyjątek przy tworzeniu karty: {e.Message}");
			}
		}
		if (CardGrid is Container c) c.QueueSort();
	}
    /// <summary>
    /// Switches visibility between Button mode and Grid mode.
    /// </summary>
    private void SetupView(int mode)
	{
		Visible = true;
		MoveToFront();

		if (mode == 0) // Buttons
		{
			if (ButtonContainer != null) ButtonContainer.Visible = true;
			if (CardGridScroll != null) CardGridScroll.Visible = false;
			TitleLabel.Text = "WYBIERZ OPCJĘ";
		}
		else // Grid
		{
			if (ButtonContainer != null) ButtonContainer.Visible = false;
			if (CardGridScroll != null) CardGridScroll.Visible = true;
			TitleLabel.Text = "WYBIERZ KARTĘ Z TALII";
		}
	}

	private void OptionClicked(int index)
	{
		Visible = false;
		_currentCallback?.Invoke(index);
	}

	public void HideModal()
	{
		Visible = false;
	}
}
