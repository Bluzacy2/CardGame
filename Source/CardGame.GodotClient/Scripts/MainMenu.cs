using Godot;
using System;

public partial class MainMenu : Control
{
    [Export] public Button PlayButton;
    [Export] public Button DeckEditorButton;
    [Export] public Button QuitButton;
    [Export] public PackedScene DeckSelectionScene;

    // Referencja do sceny gry (żebyśmy mogli ją wczytać)
    [Export] public PackedScene GameScene;

    // Opcjonalnie: Scena edytora (na przyszłość)
    // [Export] public PackedScene EditorScene;

    public override void _Ready()
    {
        if (PlayButton != null)
            PlayButton.Pressed += OnPlayPressed;

        if (DeckEditorButton != null)
            DeckEditorButton.Pressed += OnEditorPressed;

        if (QuitButton != null)
            QuitButton.Pressed += OnQuitPressed;
    }

    private void OnPlayPressed()
    {
        if (GameScene == null)
        {
            GD.PrintErr("BŁĄD: Nie przypisano sceny gry w Inspektorze Menu!");
            return;
        }

        // Zmiana sceny na grę
        GetTree().ChangeSceneToPacked(GameScene);
    }

    private void OnEditorPressed()
    {
        if (DeckSelectionScene != null)
            GetTree().ChangeSceneToPacked(DeckSelectionScene);
    }

    private void OnQuitPressed()
    {
        GD.Print("Wychodzenie z gry...");
        GetTree().Quit();
    }
        
}