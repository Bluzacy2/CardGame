using Godot;
using System;
using CardGame.Core.State.Models;
using CardGame.Core.Cards.Models;

public partial class LineView : Control
{
	[Export] public Label InfoLabel;
	[Export] public Control UnitsContainer;
	[Export] public Button ClickArea; // Nasz przycisk tła

	// Sygnał: "Ktoś kliknął w tę linię!" (wysyła ID linii)
	[Signal] public delegate void LineClickedEventHandler(int lineIndex);

	private int _myIndex;

	public override void _Ready()
	{
		// Podpinamy sygnał kliknięcia przycisku do naszego sygnału
		ClickArea.Pressed += () => EmitSignal(SignalName.LineClicked, _myIndex);
	}

	public void Render(Line lineData, PackedScene cardScene)
	{
		_myIndex = lineData.Index;
		InfoLabel.Text = $"Linia {_myIndex}";

		// 1. Wyczyść stare jednostki
		foreach (Node child in UnitsContainer.GetChildren())
		{
			child.QueueFree();
		}

		// 2. Rysuj jednostkę Gracza A (jeśli jest)
		if (lineData.Player1Unit != null)
		{
			AddUnitVisual(lineData.Player1Unit, cardScene);
		}
		
		// (Tu w przyszłości dodasz jednostkę Gracza B po drugiej stronie)
	}

	private void AddUnitVisual(CardInstance unit, PackedScene cardScene)
	{
		// Używamy tego samego widoku co w ręce, ale może mniejszego?
		var unitView = cardScene.Instantiate<CardView>();
		UnitsContainer.AddChild(unitView);
		unitView.Render(unit);
		// Opcjonalnie: Zmniejsz trochę skalę, żeby się mieściły
		unitView.Scale = new Vector2(0.8f, 0.8f);
	}
}
