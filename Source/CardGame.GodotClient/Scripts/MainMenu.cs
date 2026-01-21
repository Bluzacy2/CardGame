using Godot;
using System;

public partial class MainMenu : Control
{
	[Export] public Button PlayButton;
	[Export] public Button DeckEditorButton;
	[Export] public Button QuitButton;
	[Export] public Button MusicButton; 
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
		if (MusicButton != null)
		{
			MusicButton.Pressed += OnMusicButtonPressed;
			UpdateMusicButtonText(); // Ustaw tekst na starcie
		}
	}
	private void OnMusicButtonPressed()
	{
		// Odwołujemy się do Singletona, którego stworzyliśmy w kroku 1
		if (AudioManager.Instance != null)
		{
			AudioManager.Instance.ToggleMusic();
			UpdateMusicButtonText();
		}
	}
	
	private void UpdateMusicButtonText()
	{
		if (AudioManager.Instance != null && MusicButton != null)
		{
			bool isOn = AudioManager.Instance.IsMusicPlaying();
			MusicButton.Text = isOn ? "MUZYKA: ON" : "MUZYKA: OFF";
			// Opcjonalnie: Zmień ikonę lub kolor przycisku
			MusicButton.Modulate = isOn ? Colors.White : Colors.Red;
		}
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
