using Godot;
using System;
using System.Collections.Generic;
using CardGame.Core.Application;
using CardGame.Core.Cards.Models;
using CardGame.Core.State.Models;

public partial class GameBootstrap : Node
{
	[Export] public PackedScene CardSceneTemplate; // Tu wrzucimy Card.tscn
	[Export] public Node2D HandContainer;          // Tu będą lądować karty

	private GameEngine _gameEngine;

	public override void _Ready()
	{
		GD.Print("Inicjalizacja Gry...");

		// 1. SETUP DANYCH (To samo co w konsoli)
		// Stwórzmy kartę testową dla Gracza A
		var wolfStats = new CardStats(2, 2, 1);
		var wolfDef = new CardDefinition("wolf", "Krwawy Wilk", wolfStats);
		var wolfCard = new CardInstance(100, 1, wolfDef);

		var deckA = new List<CardInstance>();
		// Dajemy graczowi kartę do ręki (obejście Initial, bo normalnie jest pusta)
		// W prawdziwej grze użylibyśmy: PlayerState.Initial(...).WithCardAddedToHand(...)
		// Ale tutaj zrobimy szybki hack na potrzeby testu UI:

		// Tworzymy stan ręcznie jak w konsoli:
		var handA = new List<CardInstance> { wolfCard, wolfCard.WithStats(new CardStats(1, 1, 1)) }; // Dwa wilki

		var playerA = new PlayerState(1, 20, 1, 1, handA, deckA, new List<CardInstance>());
		var playerB = PlayerState.Initial(2, new List<CardInstance>());

		var initialState = new GameState(1, CardGame.Core.State.Enums.GamePhase.UnitOnly, 1, BoardState.Empty(), playerA, playerB);

		// 2. START SILNIKA
		_gameEngine = new GameEngine(initialState);

		// 3. WIZUALIZACJA RĘKI
		UpdateHandVisuals();
	}

	private void UpdateHandVisuals()
	{
		// Czyścimy stare karty (jeśli są)
		foreach (Node child in HandContainer.GetChildren())
		{
			child.QueueFree();
		}

		// Pobieramy rękę gracza A z silnika
		var hand = _gameEngine.CurrentState.PlayerA.Hand;

		int i = 0;
		foreach (var cardData in hand)
		{
			// Tworzymy wizualną kartę z szablonu (Instantiate)
			CardView cardVisual = CardSceneTemplate.Instantiate<CardView>();

			// Dodajemy do sceny
			HandContainer.AddChild(cardVisual);

			// Ustawiamy pozycję (żeby nie były jedna na drugiej)
			cardVisual.Position = new Vector2(100 + (i * 160), 400);

			// Wypełniamy danymi!
			cardVisual.Render(cardData);

			i++;
		}
	}
}
