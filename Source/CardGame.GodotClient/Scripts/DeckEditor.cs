using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Factories;
using CardGame.Core.Cards.Models;
using CardGame.Core.Decks.Data;
using System.Text.Json;

public partial class DeckEditor : Control
{
	[ExportGroup("Kontenery")]
	[Export] public Control LibraryGrid;
	[Export] public Control DeckSlotsGrid;

	[ExportGroup("UI Elementy")]
	[Export] public LineEdit DeckNameInput;
	[Export] public Label CardCountLabel;
	[Export] public Button SaveButton;
	[Export] public Button ExitButton;

	[ExportGroup("Szablony")]
	[Export] public PackedScene CardScene;
	[Export] public PackedScene SlotScene;
	[Export] public PackedScene LibraryEntryScene;

	private const string DECK_SELECTION_PATH = "res://Scenes/DeckSelection.tscn";

	public DeckData PreloadedDeck { get; set; }

	private List<int> _currentDeckList = new List<int>();
	private const int MAX_CARDS = 30;
	private const int MAX_COPIES = 3;

	private List<CardSlot> _slots = new List<CardSlot>();

	public override void _Ready()
	{
		string jsonPath = ProjectSettings.GlobalizePath("res://Data/Cards/cards.json");
		try { CardLibrary.Instance.LoadFromJson(jsonPath); } catch { }

		InitializeSlots();
		LoadLibrary();

		if (PreloadedDeck != null)
		{
			_currentDeckList = new List<int>(PreloadedDeck.CardIds);
			if (DeckNameInput != null) DeckNameInput.Text = PreloadedDeck.Name;
		}
		else
		{
			if (DeckNameInput != null) DeckNameInput.Text = "Nowa Talia";
		}

		UpdateUI();

		if (SaveButton != null) SaveButton.Pressed += SaveDeck;
		if (ExitButton != null)
			ExitButton.Pressed += () => GetTree().ChangeSceneToFile(DECK_SELECTION_PATH);
	}

	private void InitializeSlots()
	{
		if (DeckSlotsGrid == null || SlotScene == null) return;
		foreach (Node child in DeckSlotsGrid.GetChildren()) child.QueueFree();

		for (int i = 0; i < MAX_CARDS; i++)
		{
			var slot = SlotScene.Instantiate<CardSlot>();
			DeckSlotsGrid.AddChild(slot);
			_slots.Add(slot);

			// Podpinamy TYLKO dodawanie (Drop)
			slot.CardDroppedInSlot += OnCardAddedToDeck;

			// USUNIĘTO BŁĘDNĄ LINIĘ: slot.CardRemovedFromSlot...
			// Usuwanie odbywa się teraz przyciskiem "-" w bibliotece
		}
	}

	private void LoadLibrary()
	{
		if (LibraryGrid == null || CardScene == null || LibraryEntryScene == null) return;

		foreach (Node child in LibraryGrid.GetChildren()) child.QueueFree();

		var allIds = CardLibrary.Instance.GetAllIds()
			.Where(id => id < 900)
			.OrderBy(id => CardLibrary.Instance.GetCard(id).Cost) 
			.ThenBy(id => CardLibrary.Instance.GetCard(id).Name);

		foreach (var id in allIds)
		{
			var dummyInstance = new CardInstance(id, 1, CardLibrary.Instance.CreateDefinition(id));
			var entry = LibraryEntryScene.Instantiate<LibraryCardEntry>();
			LibraryGrid.AddChild(entry);

			entry.Setup(dummyInstance, CardScene);
			entry.SetMeta("CardId", id);

			// Podpinamy przyciski z biblioteki
			entry.OnAddRequest += AddCardById;
			entry.OnRemoveRequest += RemoveCardById;
		}
	}

	// --- LOGIKA EDYCJI ---

	private void AddCardById(int id)
	{
		if (_currentDeckList.Count >= MAX_CARDS) return;
		if (_currentDeckList.Count(x => x == id) >= MAX_COPIES) return;

		_currentDeckList.Add(id);
		_currentDeckList.Sort();
		UpdateUI();
	}

	private void RemoveCardById(int id)
	{
		if (_currentDeckList.Contains(id))
		{
			_currentDeckList.Remove(id);
			_currentDeckList.Sort();
			UpdateUI();
		}
	}

	// Obsługa Drag & Drop (Drop na slot wywołuje dodanie)
	private void OnCardAddedToDeck(int cardId, CardSlot targetSlot)
	{
		AddCardById(cardId);
	}

	// --- AKTUALIZACJA UI ---

	private void UpdateUI()
	{
		// 1. Sloty
		for (int i = 0; i < MAX_CARDS; i++)
		{
			if (i < _currentDeckList.Count)
			{
				int id = _currentDeckList[i];
				var dummyInstance = new CardInstance(id, 1, CardLibrary.Instance.CreateDefinition(id));

				var cv = CardScene.Instantiate<CardView>();
				cv.Render(dummyInstance);
				cv.MouseFilter = Control.MouseFilterEnum.Ignore;

				_slots[i].PlaceCard(cv, id);
			}
			else
			{
				_slots[i].ClearSlot();
			}
		}

		// 2. Biblioteka
		foreach (Node node in LibraryGrid.GetChildren())
		{
			if (node is LibraryCardEntry entry)
			{
				if (entry.HasMeta("CardId"))
				{
					int id = (int)entry.GetMeta("CardId");
					int currentCount = _currentDeckList.Count(x => x == id);
					entry.UpdateCounter(currentCount, MAX_COPIES);
				}
			}
		}

		// 3. Statystyki
		if (CardCountLabel != null)
		{
			CardCountLabel.Text = $"Karty: {_currentDeckList.Count} / {MAX_CARDS}";
			CardCountLabel.Modulate = _currentDeckList.Count == MAX_CARDS ? Colors.Green : Colors.White;
		}
	}

	// --- ZAPIS ---
	private void SaveDeck()
	{
		string name = DeckNameInput.Text;
		if (string.IsNullOrWhiteSpace(name)) name = "Nowa Talia";

		string deckId = (PreloadedDeck != null && !string.IsNullOrEmpty(PreloadedDeck.Id))
						? PreloadedDeck.Id
						: Guid.NewGuid().ToString();

		var deckData = new DeckData
		{
			Id = deckId,
			Name = name,
			CardIds = new List<int>(_currentDeckList)
		};

		var options = new JsonSerializerOptions { WriteIndented = true };
		string json = JsonSerializer.Serialize(deckData, options);

		string decksDir = ProjectSettings.GlobalizePath("res://Data/Decks/");
		if (!System.IO.Directory.Exists(decksDir))
			System.IO.Directory.CreateDirectory(decksDir);

		string safeName = string.Join("_", name.Split(System.IO.Path.GetInvalidFileNameChars()));
		string fullPath = System.IO.Path.Combine(decksDir, $"{safeName}.json");

		try
		{
			System.IO.File.WriteAllText(fullPath, json);
			GD.Print($"Zapisano talię pomyślnie: {fullPath}");
		}
		catch (Exception ex)
		{
			GD.PrintErr($"Błąd zapisu: {ex.Message}");
		}
	}
}
