using Godot;
using System;
using System.Collections.Generic;
using CardGame.Core.Application;
using CardGame.Core.Cards.Models;
using CardGame.Core.State.Models;
using CardGame.Core.Commands.Implementations; // Do komend

public partial class GameBootstrap : Node
{
	[Export] public PackedScene CardSceneTemplate; 
	[Export] public PackedScene LineSceneTemplate; // <--- NOWE: Szablon Linii
	
	[Export] public Node2D HandContainer;
	[Export] public Control BoardContainer; // <--- NOWE: Kontener na planszę (VBoxContainer byłby lepszy niż Node2D)

	private GameEngine _gameEngine;
	private int? _selectedCardId = null; // ID wybranej karty

	public override void _Ready()
	{
		GD.Print("Start...");
		
		// --- SETUP (Kopiuj ten z poprzedniej wersji) ---
		var wolfStats = new CardStats(2, 2, 1);
		var wolfDef = new CardDefinition("wolf", "Krwawy Wilk", wolfStats);
		var wolfCard = new CardInstance(100, 1, wolfDef);
		
		var handA = new List<CardInstance> { wolfCard, wolfCard.WithStats(new CardStats(1,1,1)) };
		var playerA = new PlayerState(1, 20, 1, 1, handA, new List<CardInstance>(), new List<CardInstance>());
		var playerB = PlayerState.Initial(2, new List<CardInstance>());
		var initialState = new GameState(1, CardGame.Core.State.Enums.GamePhase.UnitOnly, 1, BoardState.Empty(), playerA, playerB);
		
		_gameEngine = new GameEngine(initialState);
		
		// --- INICJALIZACJA UI ---
		CreateBoardVisuals(); // Tworzymy puste linie raz
		UpdateUI();           // Rysujemy stan
	}

	// Tworzy 4 puste linie na ekranie
	private void CreateBoardVisuals()
	{
		for (int i = 0; i < 4; i++)
		{
			var lineVisual = LineSceneTemplate.Instantiate<LineView>();
			BoardContainer.AddChild(lineVisual);
			
			// WAŻNE: Podpinamy sygnał kliknięcia linii
			lineVisual.LineClicked += OnLineClicked; 
		}
	}

	// Główna pętla odświeżania wszystkiego
	private void UpdateUI()
	{
		// 1. Rysuj Rękę
		foreach (Node child in HandContainer.GetChildren()) child.QueueFree();
		
		var hand = _gameEngine.CurrentState.PlayerA.Hand;
		int i = 0;
		foreach (var cardData in hand)
		{
			var cardVisual = CardSceneTemplate.Instantiate<CardView>();
			HandContainer.AddChild(cardVisual);
			cardVisual.Position = new Vector2(100 + (i * 160), 0);
			cardVisual.Render(cardData);
			
			// WAŻNE: Podpinamy sygnał kliknięcia karty
			cardVisual.CardClicked += OnCardSelected; 
			
			i++;
		}

		// 2. Rysuj Planszę (Aktualizuj istniejące linie)
		var board = _gameEngine.CurrentState.Board;
		int lineIdx = 0;
		foreach (LineView lineVisual in BoardContainer.GetChildren())
		{
			 // Pobieramy dane dla tej linii z silnika
			 var lineData = board.Lines[lineIdx];
			 lineVisual.Render(lineData, CardSceneTemplate);
			 lineIdx++;
		}
	}

	// --- LOGIKA INTERAKCJI ---

	private void OnCardSelected(CardView visual)
	{
		_selectedCardId = visual.MyCardData.InstanceId;
		GD.Print($"Wybrano kartę: {visual.MyCardData.Definition.Name} (ID: {_selectedCardId})");
		// Tu można dodać podświetlenie (np. visual.Modulate = Colors.Yellow)
	}

	private void OnLineClicked(int lineIndex)
	{
		if (_selectedCardId == null)
		{
			GD.Print("Najpierw wybierz kartę!");
			return;
		}

		GD.Print($"Próba zagrania karty {_selectedCardId} na linię {lineIndex}...");

		try
		{
			// TWORZYMY KOMENDĘ
			var command = new PlayUnitCommand(1, _selectedCardId.Value, lineIndex);
			
			// WYKONUJEMY W SILNIKU
			_gameEngine.ExecuteCommand(command);

			// SUKCES! Czyścimy wybór i odświeżamy ekran
			_selectedCardId = null;
			UpdateUI(); 
			
			GD.Print("Karta zagrana pomyślnie!");
		}
		catch (Exception ex)
		{
			GD.PrintErr($"Błąd ruchu: {ex.Message}");
			// Np. "Brak many" albo "Linia zajęta"
		}
	}
}
