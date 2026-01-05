using Godot;
using System;
using System.IO;
using CardGame.Core.Decks.Data;
using System.Text.Json;

public partial class DeckSelection : Control
{
    [Export] public Control DeckListContainer;
    [Export] public Button NewDeckButton;
    [Export] public Button BackButton;

    [Export] public PackedScene DeckEditorScene;

    // Ścieżka do Menu Głównego
    private const string MAIN_MENU_PATH = "res://Scenes/MainMenu.tscn";

    public override void _Ready()
    {
        if (DeckEditorScene == null)
        {
            GD.PrintErr("CRITICAL: Nie przypisano DeckEditorScene w DeckSelection!");
            return;
        }

        // Najpierw ładujemy bibliotekę kart (może być potrzebna do metadanych)
        LoadCardLibrary();

        LoadDecks();

        if (NewDeckButton != null)
            NewDeckButton.Pressed += () => LoadEditor(null);

        if (BackButton != null)
            BackButton.Pressed += () => GetTree().ChangeSceneToFile(MAIN_MENU_PATH);
    }

    private void LoadCardLibrary()
    {
        string jsonPath = ProjectSettings.GlobalizePath("res://Data/Cards/cards.json");
        try { CardGame.Core.Cards.Data.CardLibrary.Instance.LoadFromJson(jsonPath); }
        catch { }
    }

    private void LoadDecks()
    {
        if (DeckListContainer == null) return;

        // Czyścimy starą listę
        foreach (Node child in DeckListContainer.GetChildren()) child.QueueFree();

        // --- ZMIANA ŚCIEŻKI NA PROJEKTOWĄ (res://Data/Decks/) ---
        string path = ProjectSettings.GlobalizePath("res://Data/Decks/");

        // Upewnij się, że katalog istnieje
        if (!System.IO.Directory.Exists(path))
            System.IO.Directory.CreateDirectory(path);

        var files = System.IO.Directory.GetFiles(path, "*.json");

        if (files.Length == 0)
        {
            // Opcjonalnie: Label "Brak talii"
        }

        foreach (var file in files)
        {
            try
            {
                string json = System.IO.File.ReadAllText(file);
                var deckData = JsonSerializer.Deserialize<DeckData>(json);

                if (deckData != null)
                {
                    // Przekazujemy ścieżkę pliku, aby móc go usunąć
                    CreateDeckRow(deckData, file);
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr($"Błąd odczytu pliku {file}: {ex.Message}");
            }
        }
    }

    private void CreateDeckRow(DeckData data, string filePath)
    {
        // 1. Kontener na wiersz (HBox)
        var row = new HBoxContainer();
        row.CustomMinimumSize = new Vector2(0, 60);
        row.AddThemeConstantOverride("separation", 10);

        // 2. Przycisk Wyboru Talii (Duży, po lewej)
        var selectBtn = new Button();
        selectBtn.Text = $"{(string.IsNullOrEmpty(data.Name) ? "Bez Nazwy" : data.Name)} ({data.CardIds.Count} kart)";
        // Ważne: Rozciągamy go na całą dostępną szerokość
        selectBtn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        selectBtn.Alignment = HorizontalAlignment.Left;

        // Obsługa Dwukliku (Edycja)
        selectBtn.GuiInput += (eventData) =>
        {
            if (eventData is InputEventMouseButton mb
                && mb.ButtonIndex == MouseButton.Left
                && mb.DoubleClick)
            {
                LoadEditor(data);
            }
        };
        // Zwykłe kliknięcie też ładuje
        selectBtn.Pressed += () => LoadEditor(data);

        // 3. Przycisk Usuwania (Mały, po prawej)
        var deleteBtn = new Button();
        deleteBtn.Text = "USUŃ";
        deleteBtn.CustomMinimumSize = new Vector2(80, 0);
        deleteBtn.Modulate = new Color(1, 0.4f, 0.4f); // Czerwony kolor ostrzegawczy

        // Logika usuwania
        deleteBtn.Pressed += () => DeleteDeck(filePath);

        // Składanie wiersza
        row.AddChild(selectBtn);
        row.AddChild(deleteBtn);

        // Dodanie do listy głównej
        DeckListContainer.AddChild(row);
    }

    private void DeleteDeck(string filePath)
    {
        try
        {
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
                GD.Print($"Usunięto talię: {filePath}");

                // Odświeżamy listę natychmiast
                LoadDecks();
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Nie udało się usunąć pliku: {ex.Message}");
        }
    }

    private void LoadEditor(DeckData data)
    {
        var editor = DeckEditorScene.Instantiate<DeckEditor>();

        if (data != null)
            editor.PreloadedDeck = data;

        GetTree().Root.AddChild(editor);
        GetTree().CurrentScene.QueueFree();
        GetTree().CurrentScene = editor;
    }
}