using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Factories;
using CardGame.Core.Cards.Models;
using CardGame.Core.Decks.Data;
using System.Text.Json;

/// <summary>
/// Manages the deck building interface, allowing players to create, edit, and save card decks.
/// Handles the visualization of the card library, the current deck slots, and file I/O operations.
/// </summary>
public partial class DeckEditor : Control
{
    #region Scene References

    [ExportGroup("Kontenery")]
    /// <summary>Container for displaying the list of available cards (Library).</summary>
    [Export] public Control LibraryGrid;
    /// <summary>Container for displaying the cards currently in the deck.</summary>
    [Export] public Control DeckSlotsGrid;

    [ExportGroup("UI Elementy")]
    /// <summary>Input field for the deck name.</summary>
    [Export] public LineEdit DeckNameInput;
    /// <summary>Label displaying the current card count (e.g., "15/30").</summary>
    [Export] public Label CardCountLabel;
    /// <summary>Button to save the current deck to a JSON file.</summary>
    [Export] public Button SaveButton;
    /// <summary>Button to exit the editor and return to deck selection.</summary>
    [Export] public Button ExitButton;

    [ExportGroup("Szablony")]
    /// <summary>Template scene for instantiating visual card representations.</summary>
    [Export] public PackedScene CardScene;
    /// <summary>Template scene for instantiating empty card slots in the deck grid.</summary>
    [Export] public PackedScene SlotScene;
    /// <summary>Template scene for instantiating library entries.</summary>
    [Export] public PackedScene LibraryEntryScene;

    #endregion

    #region Constants and Fields

    private const string DECK_SELECTION_PATH = "res://Scenes/DeckSelection.tscn";

    /// <summary>
    /// Optional deck data loaded when opening the editor to edit an existing deck.
    /// If null, a new deck is created.
    /// </summary>
    public DeckData PreloadedDeck { get; set; }

    private List<int> _currentDeckList = new List<int>();
    private const int MAX_CARDS = 30;
    private const int MAX_COPIES = 3;

    private List<CardSlot> _slots = new List<CardSlot>();

    #endregion

    #region Initialization

    /// <summary>
    /// Initializes the editor, loads the card library, sets up slots, and populates UI if a deck is preloaded.
    /// </summary>
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

    /// <summary>
    /// Creates the empty slots in the deck grid based on MAX_CARDS constant.
    /// </summary>
    private void InitializeSlots()
    {
        if (DeckSlotsGrid == null || SlotScene == null) return;
        foreach (Node child in DeckSlotsGrid.GetChildren()) child.QueueFree();

        for (int i = 0; i < MAX_CARDS; i++)
        {
            var slot = SlotScene.Instantiate<CardSlot>();
            DeckSlotsGrid.AddChild(slot);
            _slots.Add(slot);

            // Bind drag & drop event
            slot.CardDroppedInSlot += OnCardAddedToDeck;

            // REMOVED: slot.CardRemovedFromSlot...
            // Removal is handled via the "-" button in the library entry
        }
    }

    /// <summary>
    /// Populates the library grid with all available collectible cards, sorted by cost and name.
    /// </summary>
    private void LoadLibrary()
    {
        if (LibraryGrid == null || CardScene == null || LibraryEntryScene == null) return;

        foreach (Node child in LibraryGrid.GetChildren()) child.QueueFree();

        var allIds = CardLibrary.Instance.GetAllIds()
            .Where(id => id < 900) // Exclude non-collectible tokens
            .OrderBy(id => CardLibrary.Instance.GetCard(id).Cost)
            .ThenBy(id => CardLibrary.Instance.GetCard(id).Name);

        foreach (var id in allIds)
        {
            var dummyInstance = new CardInstance(id, 1, CardLibrary.Instance.CreateDefinition(id));
            var entry = LibraryEntryScene.Instantiate<LibraryCardEntry>();
            LibraryGrid.AddChild(entry);

            entry.Setup(dummyInstance, CardScene);
            entry.SetMeta("CardId", id);

            // Bind library buttons
            entry.OnAddRequest += AddCardById;
            entry.OnRemoveRequest += RemoveCardById;
        }
    }

    #endregion

    #region Deck Management Logic

    /// <summary>
    /// Adds a card to the deck list if limits (max size, max copies) allow.
    /// </summary>
    /// <param name="id">The card definition ID.</param>
    private void AddCardById(int id)
    {
        if (_currentDeckList.Count >= MAX_CARDS) return;
        if (_currentDeckList.Count(x => x == id) >= MAX_COPIES) return;

        _currentDeckList.Add(id);
        _currentDeckList.Sort();
        UpdateUI();
    }

    /// <summary>
    /// Removes a card from the deck list if it exists.
    /// </summary>
    /// <param name="id">The card definition ID.</param>
    private void RemoveCardById(int id)
    {
        if (_currentDeckList.Contains(id))
        {
            _currentDeckList.Remove(id);
            _currentDeckList.Sort();
            UpdateUI();
        }
    }

    /// <summary>
    /// Handler for drag-and-drop events dropping a card into a slot.
    /// </summary>
    /// <param name="cardId">The dropped card ID.</param>
    /// <param name="targetSlot">The target slot (unused in logic, just for signature).</param>
    private void OnCardAddedToDeck(int cardId, CardSlot targetSlot)
    {
        AddCardById(cardId);
    }

    #endregion

    #region UI Updates

    /// <summary>
    /// Refreshes the deck slots, library counters, and status labels based on the current deck state.
    /// </summary>
    private void UpdateUI()
    {
        // 1. Update Slots
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

        // 2. Update Library Counters
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

        // 3. Update Statistics Labels
        if (CardCountLabel != null)
        {
            CardCountLabel.Text = $"Karty: {_currentDeckList.Count} / {MAX_CARDS}";
            CardCountLabel.Modulate = _currentDeckList.Count == MAX_CARDS ? Colors.Green : Colors.White;
        }
    }

    #endregion

    #region IO Operations

    /// <summary>
    /// Saves the current deck configuration to a JSON file.
    /// </summary>
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

    #endregion
}