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
        var mainFont = GD.Load<Font>("res://Assets/Fonts/Ari-CBold.ttf");

        // --- STYLE DLA PRZYCISKU WYBORU (SELECT) ---
        var styleNormal = new StyleBoxFlat();
        styleNormal.BgColor = Color.FromHtml("#000000");
        styleNormal.BorderColor = Color.FromHtml("#404040");
        styleNormal.SetBorderWidthAll(2);
        styleNormal.ContentMarginLeft = 20;

        var styleHover = new StyleBoxFlat();
        styleHover.BgColor = Color.FromHtml("#cccccc");
        styleHover.SetBorderWidthAll(0);
        styleHover.ContentMarginLeft = 20;

        // --- STYLE DLA PRZYCISKU USUŃ (DELETE) ---
        var styleDeleteNormal = new StyleBoxFlat();
        styleDeleteNormal.BgColor = Color.FromHtml("#880000");
        styleDeleteNormal.BorderColor = Color.FromHtml("#404040");
        styleDeleteNormal.SetBorderWidthAll(2);

        var styleDeleteHover = new StyleBoxFlat();
        styleDeleteHover.BgColor = Color.FromHtml("#ff3333");
        styleDeleteHover.SetBorderWidthAll(0);

        // Pusty styl dla fokusu (żeby nie było niebieskiej ramki i znikania tekstu)
        var styleEmpty = new StyleBoxEmpty();

        var row = new HBoxContainer();
        row.CustomMinimumSize = new Vector2(0, 60);
        row.AddThemeConstantOverride("separation", 10);

        // --- KONFIGURACJA PRZYCISKU WYBORU ---
        var selectBtn = new Button();
        selectBtn.Text = $"{data.Name} ({data.CardIds.Count} KART)";
        selectBtn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        selectBtn.Alignment = HorizontalAlignment.Left;

        // Przypisanie stylów graficznych
        selectBtn.AddThemeStyleboxOverride("normal", styleNormal);
        selectBtn.AddThemeStyleboxOverride("hover", styleHover);
        selectBtn.AddThemeStyleboxOverride("pressed", styleHover);
        selectBtn.AddThemeStyleboxOverride("focus", styleEmpty); // WYŁĄCZENIE RAMKI FOKUSU

        // PEŁNE NADPISANIE KOLORÓW CZCIONKI (wszystkie stany)
        selectBtn.AddThemeColorOverride("font_color", Colors.White);           // Normalny
        selectBtn.AddThemeColorOverride("font_hover_color", Colors.Black);     // Najechanie
        selectBtn.AddThemeColorOverride("font_pressed_color", Colors.Black);   // Kliknięcie
        selectBtn.AddThemeColorOverride("font_focus_color", Colors.Black);     // Zostanie po kliknięciu
        selectBtn.AddThemeColorOverride("font_hover_pressed_color", Colors.Black); // Ważne!

        if (mainFont != null) selectBtn.AddThemeFontOverride("font", mainFont);

        // --- KONFIGURACJA PRZYCISKU USUŃ ---
        var deleteBtn = new Button();
        deleteBtn.Text = "USUŃ";
        deleteBtn.CustomMinimumSize = new Vector2(100, 0);

        deleteBtn.AddThemeStyleboxOverride("normal", styleDeleteNormal);
        deleteBtn.AddThemeStyleboxOverride("hover", styleDeleteHover);
        deleteBtn.AddThemeStyleboxOverride("pressed", styleDeleteHover);
        deleteBtn.AddThemeStyleboxOverride("focus", styleEmpty); // WYŁĄCZENIE RAMKI FOKUSU

        deleteBtn.AddThemeColorOverride("font_color", Colors.White);
        deleteBtn.AddThemeColorOverride("font_hover_color", Colors.White);
        deleteBtn.AddThemeColorOverride("font_pressed_color", Colors.Black);
        deleteBtn.AddThemeColorOverride("font_focus_color", Colors.White);
        deleteBtn.AddThemeColorOverride("font_hover_pressed_color", Colors.Black);

        if (mainFont != null) deleteBtn.AddThemeFontOverride("font", mainFont);

        // --- LOGIKA I SKŁADANIE ---
        selectBtn.Pressed += () => LoadEditor(data);
        deleteBtn.Pressed += () => DeleteDeck(filePath);

        row.AddChild(selectBtn);
        row.AddChild(deleteBtn);
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