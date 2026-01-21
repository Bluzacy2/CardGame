using Godot;
using System;

/// <summary>
/// Controls the Main Menu scene, handling navigation to gameplay, deck editing,
/// and providing global settings controls (like music).
/// </summary>
public partial class MainMenu : Control
{
    #region UI Elements

    /// <summary>Button to navigate to the game setup screen (deck selection for battle).</summary>
    [Export] public Button PlayButton;

    /// <summary>Button to navigate to the deck editor selection screen.</summary>
    [Export] public Button DeckEditorButton;

    /// <summary>Button to exit the application.</summary>
    [Export] public Button QuitButton;

    /// <summary>Button to toggle background music on/off.</summary>
    [Export] public Button MusicButton;

    #endregion

    #region Scene Paths

    /// <summary>File path to the Deck Selection scene (used for editing decks).</summary>
    [Export(PropertyHint.File, "*.tscn")]
    public string DeckSelectionEditorPath = "res://Scenes/DeckSelection.tscn";

    /// <summary>File path to the Game Setup scene (used for starting a match).</summary>
    [Export(PropertyHint.File, "*.tscn")]
    public string GameSetupPath = "res://Scenes/GameDeckSelector.tscn";

    #endregion

    #region Lifecycle Methods

    /// <summary>
    /// Called when the node enters the scene tree.
    /// Connects button signals and initializes UI states.
    /// </summary>
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
            UpdateMusicButtonText(); // Set initial text
        }
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Toggles the background music via the global AudioManager and updates the button visual.
    /// </summary>
    private void OnMusicButtonPressed()
    {
        // Reference the Singleton created in previous steps
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.ToggleMusic();
            UpdateMusicButtonText();
        }
    }

    /// <summary>
    /// Updates the music button text and color based on the current audio state.
    /// </summary>
    private void UpdateMusicButtonText()
    {
        if (AudioManager.Instance != null && MusicButton != null)
        {
            bool isOn = AudioManager.Instance.IsMusicPlaying();
            MusicButton.Text = isOn ? "MUZYKA: ON" : "MUZYKA: OFF";
            // Optional: Change icon or color
            MusicButton.Modulate = isOn ? Colors.White : Colors.Red;
        }
    }

    /// <summary>
    /// Changes the scene to the Game Setup (GameDeckSelector).
    /// </summary>
    private void OnPlayPressed()
    {
        if (string.IsNullOrEmpty(GameSetupPath))
        {
            GD.PrintErr("Missing GameSetupPath in MainMenu!");
            return;
        }
        // Navigate to deck selection for battle, not directly to the game
        GetTree().ChangeSceneToFile(GameSetupPath);
    }

    /// <summary>
    /// Changes the scene to the Deck Editor selection screen.
    /// </summary>
    private void OnEditorPressed()
    {
        if (string.IsNullOrEmpty(DeckSelectionEditorPath))
        {
            GD.PrintErr("Missing DeckSelectionEditorPath in MainMenu!");
            return;
        }
        GetTree().ChangeSceneToFile(DeckSelectionEditorPath);
    }

    /// <summary>
    /// Terminates the application.
    /// </summary>
    private void OnQuitPressed()
    {
        GD.Print("Exiting game...");
        GetTree().Quit();
    }

    #endregion
}