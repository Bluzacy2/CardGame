using Godot;
using System;

public partial class MainMenu : Control
{
    [Export] public Button PlayButton;
    [Export] public Button DeckEditorButton;
    [Export] public Button QuitButton;
    [Export(PropertyHint.File, "*.tscn")]
    public string DeckSelectionEditorPath = "res://Scenes/DeckSelection.tscn";

    [Export(PropertyHint.File, "*.tscn")]
    public string GameSetupPath = "res://Scenes/GameDeckSelector.tscn";
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
        if (string.IsNullOrEmpty(GameSetupPath))
        {
            GD.PrintErr("Brak ścieżki do GameSetup w MainMenu!");
            return;
        }
        // Przekierowanie do wyboru talii (GameDeckSelector), a nie bezpośrednio do gry
        GetTree().ChangeSceneToFile(GameSetupPath);
    }

    private void OnEditorPressed()
    {
        if (string.IsNullOrEmpty(DeckSelectionEditorPath))
        {
            GD.PrintErr("Brak ścieżki do DeckSelectionEditor w MainMenu!");
            return;
        }
        GetTree().ChangeSceneToFile(DeckSelectionEditorPath);
    }

    private void OnQuitPressed()
    {
        GD.Print("Wychodzenie z gry...");
        GetTree().Quit();
    }
}