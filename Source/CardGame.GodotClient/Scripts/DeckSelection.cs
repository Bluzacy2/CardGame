using Godot;
using System;
using System.IO;
using CardGame.Core.Decks.Data;
using System.Text.Json;

/// <summary>
/// Manages the Deck Selection screen where players can view, create, edit, and delete their custom decks.
/// Handles scanning the file system for deck files and dynamically generating the UI list.
/// </summary>
public partial class DeckSelection : Control
{
    #region Scene References

    /// <summary>The container where deck entry rows will be instantiated.</summary>
    [Export] public Control DeckListContainer;
    /// <summary>Button to create a new, empty deck.</summary>
    [Export] public Button NewDeckButton;
    /// <summary>Button to return to the Main Menu.</summary>
    [Export] public Button BackButton;

    /// <summary>The scene resource for the Deck Editor to switch to when editing/creating.</summary>
    [Export] public PackedScene DeckEditorScene;

    #endregion

    // Path to the Main Menu scene
    private const string MAIN_MENU_PATH = "res://Scenes/MainMenu.tscn";

    #region Lifecycle Methods

    /// <summary>
    /// Called when the node enters the scene tree.
    /// Initializes the card library, loads existing decks from disk, and connects button signals.
    /// </summary>
    public override void _Ready()
    {
        if (DeckEditorScene == null)
        {
            GD.PrintErr("CRITICAL: DeckEditorScene not assigned in DeckSelection!");
            return;
        }

        // Load card definitions first (needed for metadata like card names)
        LoadCardLibrary();

        LoadDecks();

        if (NewDeckButton != null)
            NewDeckButton.Pressed += () => LoadEditor(null);

        if (BackButton != null)
            BackButton.Pressed += () => GetTree().ChangeSceneToFile(MAIN_MENU_PATH);
    }

    #endregion

    #region Data Loading

    /// <summary>
    /// Loads the card database from the JSON file into the Core singleton.
    /// </summary>
    private void LoadCardLibrary()
    {
        string jsonPath = ProjectSettings.GlobalizePath("res://Data/Cards/cards.json");
        try { CardGame.Core.Cards.Data.CardLibrary.Instance.LoadFromJson(jsonPath); }
        catch { }
    }

    /// <summary>
    /// Scans the persistent data directory for deck JSON files and populates the UI list.
    /// </summary>
    private void LoadDecks()
    {
        if (DeckListContainer == null) return;

        // Clear existing list
        foreach (Node child in DeckListContainer.GetChildren()) child.QueueFree();

        // Path: res://Data/Decks/
        string path = ProjectSettings.GlobalizePath("res://Data/Decks/");

        // Ensure directory exists
        if (!System.IO.Directory.Exists(path))
            System.IO.Directory.CreateDirectory(path);

        var files = System.IO.Directory.GetFiles(path, "*.json");

        if (files.Length == 0)
        {
            // Optional: Show "No decks found" label
        }

        foreach (var file in files)
        {
            try
            {
                string json = System.IO.File.ReadAllText(file);
                var deckData = JsonSerializer.Deserialize<DeckData>(json);

                if (deckData != null)
                {
                    // Pass file path to allow deletion
                    CreateDeckRow(deckData, file);
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr($"Error reading deck file {file}: {ex.Message}");
            }
        }
    }

    #endregion

    #region UI Generation

    /// <summary>
    /// Dynamically creates a UI row for a specific deck, including Select (Edit) and Delete buttons.
    /// Applies custom styling programmatically.
    /// </summary>
    /// <param name="data">The deck data object.</param>
    /// <param name="filePath">The full file path to the deck JSON.</param>
    private void CreateDeckRow(DeckData data, string filePath)
    {
        var mainFont = GD.Load<Font>("res://Assets/Fonts/Ari-CBold.ttf");

        // --- STYLES FOR SELECT BUTTON ---
        var styleNormal = new StyleBoxFlat();
        styleNormal.BgColor = Color.FromHtml("#000000");
        styleNormal.BorderColor = Color.FromHtml("#404040");
        styleNormal.SetBorderWidthAll(2);
        styleNormal.ContentMarginLeft = 20;

        var styleHover = new StyleBoxFlat();
        styleHover.BgColor = Color.FromHtml("#cccccc");
        styleHover.SetBorderWidthAll(0);
        styleHover.ContentMarginLeft = 20;

        // --- STYLES FOR DELETE BUTTON ---
        var styleDeleteNormal = new StyleBoxFlat();
        styleDeleteNormal.BgColor = Color.FromHtml("#880000");
        styleDeleteNormal.BorderColor = Color.FromHtml("#404040");
        styleDeleteNormal.SetBorderWidthAll(2);

        var styleDeleteHover = new StyleBoxFlat();
        styleDeleteHover.BgColor = Color.FromHtml("#ff3333");
        styleDeleteHover.SetBorderWidthAll(0);

        // Empty style for focus (removes blue border)
        var styleEmpty = new StyleBoxEmpty();

        var row = new HBoxContainer();
        row.CustomMinimumSize = new Vector2(0, 60);
        row.AddThemeConstantOverride("separation", 10);

        // --- CONFIGURE SELECT BUTTON ---
        var selectBtn = new Button();
        selectBtn.Text = $"{data.Name} ({data.CardIds.Count} KART)";
        selectBtn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        selectBtn.Alignment = HorizontalAlignment.Left;

        // Apply styles
        selectBtn.AddThemeStyleboxOverride("normal", styleNormal);
        selectBtn.AddThemeStyleboxOverride("hover", styleHover);
        selectBtn.AddThemeStyleboxOverride("pressed", styleHover);
        selectBtn.AddThemeStyleboxOverride("focus", styleEmpty);

        // Override font colors for all states
        selectBtn.AddThemeColorOverride("font_color", Colors.White);
        selectBtn.AddThemeColorOverride("font_hover_color", Colors.Black);
        selectBtn.AddThemeColorOverride("font_pressed_color", Colors.Black);
        selectBtn.AddThemeColorOverride("font_focus_color", Colors.Black);
        selectBtn.AddThemeColorOverride("font_hover_pressed_color", Colors.Black);

        if (mainFont != null) selectBtn.AddThemeFontOverride("font", mainFont);

        // --- CONFIGURE DELETE BUTTON ---
        var deleteBtn = new Button();
        deleteBtn.Text = "USUŃ";
        deleteBtn.CustomMinimumSize = new Vector2(100, 0);

        deleteBtn.AddThemeStyleboxOverride("normal", styleDeleteNormal);
        deleteBtn.AddThemeStyleboxOverride("hover", styleDeleteHover);
        deleteBtn.AddThemeStyleboxOverride("pressed", styleDeleteHover);
        deleteBtn.AddThemeStyleboxOverride("focus", styleEmpty);

        deleteBtn.AddThemeColorOverride("font_color", Colors.White);
        deleteBtn.AddThemeColorOverride("font_hover_color", Colors.White);
        deleteBtn.AddThemeColorOverride("font_pressed_color", Colors.Black);
        deleteBtn.AddThemeColorOverride("font_focus_color", Colors.White);
        deleteBtn.AddThemeColorOverride("font_hover_pressed_color", Colors.Black);

        if (mainFont != null) deleteBtn.AddThemeFontOverride("font", mainFont);

        // --- LOGIC BINDING ---
        selectBtn.Pressed += () => LoadEditor(data);
        deleteBtn.Pressed += () => DeleteDeck(filePath);

        row.AddChild(selectBtn);
        row.AddChild(deleteBtn);
        DeckListContainer.AddChild(row);
    }

    #endregion

    #region Deck Operations

    /// <summary>
    /// Deletes the specified deck file from disk and refreshes the UI list.
    /// </summary>
    /// <param name="filePath">The full path to the deck file.</param>
    private void DeleteDeck(string filePath)
    {
        try
        {
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
                GD.Print($"Deleted deck: {filePath}");

                // Refresh list immediately
                LoadDecks();
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Failed to delete file: {ex.Message}");
        }
    }

    /// <summary>
    /// Switches the scene to the Deck Editor.
    /// </summary>
    /// <param name="data">The deck data to edit, or null to create a new deck.</param>
    private void LoadEditor(DeckData data)
    {
        var editor = DeckEditorScene.Instantiate<DeckEditor>();

        if (data != null)
            editor.PreloadedDeck = data;

        GetTree().Root.AddChild(editor);
        GetTree().CurrentScene.QueueFree();
        GetTree().CurrentScene = editor;
    }

    #endregion
}