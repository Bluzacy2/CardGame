using Godot;
using System;
using System.IO;
using System.Text.Json;
using CardGame.Core.Decks.Data;
using System.Collections.Generic;
using System.Linq;

namespace CardGame.GodotClient
{
    /// <summary>
    /// Manages the deck selection screen before a match starts.
    /// Allows the player to choose their own deck (Left Click) and the bot's deck (Right Click).
    /// Features a custom SDF shader for visual feedback on selection states (Player, Bot, or Both).
    /// </summary>
    public partial class GameDeckSelector : Control
    {
        #region Scene References

        /// <summary>Container where deck selection buttons are instantiated.</summary>
        [Export] public VBoxContainer DeckListContainer;

        /// <summary>Button to start the match (enabled only when both decks are selected).</summary>
        [Export] public Button StartGameButton;

        /// <summary>Button to return to the Main Menu.</summary>
        [Export] public Button BackButton;

        /// <summary>Label displaying the name of the deck selected by the player.</summary>
        [Export] public Label PlayerSelectedLabel;

        /// <summary>Label displaying the name of the deck selected for the bot.</summary>
        [Export] public Label BotSelectedLabel;

        #endregion

        #region Constants & Fields

        private const string MAIN_MENU_PATH = "res://Scenes/MainMenu.tscn";
        private const string GAME_SCENE_PATH = "res://Scenes/Main.tscn";

        private DeckData _playerChoice;
        private DeckData _botChoice;

        private Dictionary<string, Button> _deckButtons = new();

        // Colors for visual feedback
        private readonly Color _colPlayer = new Color(0.2f, 0.9f, 0.2f); // Green
        private readonly Color _colBot = new Color(0.9f, 0.2f, 0.2f);    // Red
        private readonly Color _colDark = new Color(0.15f, 0.15f, 0.15f); // Background

        /// <summary>
        /// Shader code for rendering a dynamic, rounded gradient border using Signed Distance Fields (SDF).
        /// Used when both Player and Bot select the same deck to create a split-color effect.
        /// </summary>
        private const string BORDER_SHADER_CODE = @"
            shader_type canvas_item;
            
            uniform vec4 color_left : source_color;
            uniform vec4 color_right : source_color;
            uniform vec4 bg_color : source_color;
            
            uniform vec2 size; // Button size in pixels
            uniform float radius = 8.0; // Corner radius
            uniform float width = 4.0;  // Border thickness

            // SDF function for a rounded box
            float sdRoundedBox(vec2 p, vec2 b, float r) {
                vec2 q = abs(p) - b + r;
                return min(max(q.x, q.y), 0.0) + length(max(q, 0.0)) - r;
            }

            void fragment() {
                vec2 half_size = size * 0.5;
                vec2 pos = (UV * size) - half_size; // Pixel position relative to center
                
                // Calculate distance to edge
                float d = sdRoundedBox(pos, half_size, radius);
                
                // Gradient logic: Left->Right with a slight diagonal slant
                float t = UV.x + (UV.y - 0.5) * 0.1;
                
                // Smooth transition in the middle
                t = smoothstep(0.4, 0.6, t);
                
                vec4 border_col = mix(color_left, color_right, t);
                
                if (d > 0.0) {
                    // Outside rounded corners -> transparent
                    COLOR = vec4(0.0);
                } else if (d > -width) {
                    // Border area
                    COLOR = border_col;
                } else {
                    // Inner content area
                    COLOR = bg_color;
                }
            }
        ";

        private ShaderMaterial _baseShaderMaterial;

        #endregion

        #region Lifecycle Methods

        /// <summary>
        /// Called when the node enters the scene tree.
        /// Initializes the shader material, loads decks from disk, and sets up UI state.
        /// </summary>
        public override void _Ready()
        {
            // Compile shader once at startup
            var shader = new Shader();
            shader.Code = BORDER_SHADER_CODE;
            _baseShaderMaterial = new ShaderMaterial();
            _baseShaderMaterial.Shader = shader;

            // Set default shader parameters
            _baseShaderMaterial.SetShaderParameter("color_left", _colPlayer);
            _baseShaderMaterial.SetShaderParameter("color_right", _colBot);
            _baseShaderMaterial.SetShaderParameter("bg_color", _colDark);

            // Enforce minimum button sizes
            if (StartGameButton != null) StartGameButton.CustomMinimumSize = new Vector2(200, 60);
            if (BackButton != null) BackButton.CustomMinimumSize = new Vector2(200, 60);

            LoadDecks();
            UpdateSelectionInfo();

            if (StartGameButton != null)
            {
                StartGameButton.Pressed += OnStartGamePressed;
                StartGameButton.Disabled = true;
            }

            if (BackButton != null)
                BackButton.Pressed += () => GetTree().ChangeSceneToFile(MAIN_MENU_PATH);
        }

        #endregion

        #region Deck Loading

        /// <summary>
        /// Scans the 'res://Data/Decks/' directory for JSON deck files and populates the list.
        /// </summary>
        private void LoadDecks()
        {
            if (DeckListContainer == null) return;

            foreach (Node child in DeckListContainer.GetChildren()) child.QueueFree();
            _deckButtons.Clear();

            string path = ProjectSettings.GlobalizePath("res://Data/Decks/");
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);

            var files = Directory.GetFiles(path, "*.json");

            foreach (var file in files)
            {
                try
                {
                    string json = File.ReadAllText(file);
                    var deckData = JsonSerializer.Deserialize<DeckData>(json);
                    if (deckData != null)
                    {
                        CreateDeckButton(deckData);
                    }
                }
                catch { }
            }
        }

        /// <summary>
        /// Creates a UI button for a deck entry.
        /// Adds a hidden ColorRect child to handle the custom shader rendering when selected by both players.
        /// </summary>
        /// <param name="deck">The deck data to represent.</param>
        private void CreateDeckButton(DeckData deck)
        {
            var mainFont = GD.Load<Font>("res://Assets/Fonts/Ari-CBold.ttf");

            var btn = new Button();
            btn.Text = $"{deck.Name.ToUpper()} ({deck.CardIds.Count} KART)";
            btn.CustomMinimumSize = new Vector2(0, 60);
            btn.Alignment = HorizontalAlignment.Left;
            btn.ActionMode = BaseButton.ActionModeEnum.Press;
            btn.SizeFlagsHorizontal = SizeFlags.ExpandFill;

            // --- CHILD COLOR RECT (Custom Shader Border) ---
            var gradientRect = new ColorRect();
            gradientRect.Name = "GradientBorder";
            gradientRect.MouseFilter = MouseFilterEnum.Ignore;
            gradientRect.SetAnchorsPreset(LayoutPreset.FullRect);
            gradientRect.ShowBehindParent = true;
            gradientRect.Visible = false;

            // Clone material for each button to handle different sizes
            gradientRect.Material = (Material)_baseShaderMaterial.Duplicate();

            btn.AddChild(gradientRect);

            // Font configuration
            if (mainFont != null)
            {
                btn.AddThemeFontOverride("font", mainFont);
                btn.AddThemeFontSizeOverride("font_size", 22);
            }

            // Signal connections
            btn.Resized += () => UpdateShaderSize(btn, gradientRect);
            btn.GuiInput += (inputEvent) => OnDeckRowClicked(inputEvent, deck);

            _deckButtons[deck.Id] = btn;
            DeckListContainer.AddChild(btn);

            ApplyButtonStyle(btn, false, false);
        }

        /// <summary>
        /// Updates the 'size' uniform in the shader to ensure corners remain circular regardless of button aspect ratio.
        /// </summary>
        private void UpdateShaderSize(Button btn, ColorRect rect)
        {
            if (rect.Material is ShaderMaterial mat)
            {
                mat.SetShaderParameter("size", btn.Size);
            }
        }

        #endregion

        #region Interaction Handling

        /// <summary>
        /// Handles input events on deck buttons.
        /// Left Click = Select for Player.
        /// Right Click = Select for Bot.
        /// </summary>
        private void OnDeckRowClicked(InputEvent inputEvent, DeckData deck)
        {
            if (inputEvent is InputEventMouseButton mb && mb.Pressed)
            {
                if (mb.ButtonIndex == MouseButton.Left)
                {
                    _playerChoice = deck;
                    if (_botChoice == null) _botChoice = deck; // Auto-pick for bot if empty
                }
                else if (mb.ButtonIndex == MouseButton.Right)
                {
                    _botChoice = deck;
                }

                UpdateAllButtonsVisuals();
                UpdateSelectionInfo();
            }
        }

        #endregion

        #region Visual Updates

        /// <summary>
        /// Iterates through all deck buttons and updates their visual style based on current selection.
        /// </summary>
        private void UpdateAllButtonsVisuals()
        {
            foreach (var kvp in _deckButtons)
            {
                string deckId = kvp.Key;
                Button btn = kvp.Value;

                bool isPlayer = _playerChoice != null && _playerChoice.Id == deckId;
                bool isBot = _botChoice != null && _botChoice.Id == deckId;

                ApplyButtonStyle(btn, isPlayer, isBot);
            }
        }

        /// <summary>
        /// Applies the appropriate style to a button based on selection state.
        /// Handles switching between standard StyleBoxFlat (single selection) and Shader (dual selection).
        /// </summary>
        private void ApplyButtonStyle(Button btn, bool isPlayer, bool isBot)
        {
            var gradientRect = btn.GetNode<ColorRect>("GradientBorder");
            btn.RemoveThemeColorOverride("font_color");

            // Base style for single selections
            var style = new StyleBoxFlat();
            style.CornerRadiusTopLeft = 8; style.CornerRadiusTopRight = 8;
            style.CornerRadiusBottomRight = 8; style.CornerRadiusBottomLeft = 8;
            style.ContentMarginLeft = 15;

            style.BorderWidthBottom = 2; style.BorderWidthTop = 2;
            style.BorderWidthLeft = 2; style.BorderWidthRight = 2;

            if (isPlayer && isBot)
            {
                // BOTH SELECTED: Enable Shader
                gradientRect.Visible = true;
                UpdateShaderSize(btn, gradientRect);

                style.BgColor = new Color(0, 0, 0, 0); // Transparent button bg to show shader behind
                style.BorderWidthBottom = 0; style.BorderWidthTop = 0;
                style.BorderWidthLeft = 0; style.BorderWidthRight = 0;

                btn.AddThemeColorOverride("font_color", new Color(1, 1, 0.6f));
            }
            else
            {
                // SINGLE SELECTION: Standard StyleBox
                gradientRect.Visible = false;
                style.BgColor = _colDark;

                if (isPlayer)
                {
                    style.BorderColor = _colPlayer;
                    btn.AddThemeColorOverride("font_color", _colPlayer);
                    btn.AddThemeColorOverride("font_focus_color", _colPlayer);
                }
                else if (isBot)
                {
                    style.BorderColor = _colBot;
                    btn.AddThemeColorOverride("font_color", _colBot);
                    btn.AddThemeColorOverride("font_focus_color", _colBot);
                }
                else
                {
                    // NONE
                    style.BorderColor = new Color(0.3f, 0.3f, 0.3f);
                    style.BorderWidthBottom = 1; style.BorderWidthTop = 1;
                    style.BorderWidthLeft = 1; style.BorderWidthRight = 1;
                    btn.AddThemeColorOverride("font_color", new Color(1, 1, 0.6f));
                    btn.AddThemeColorOverride("font_focus_color", new Color(1, 1, 0.6f));
                }
            }

            btn.AddThemeStyleboxOverride("normal", style);
            btn.AddThemeStyleboxOverride("hover", style);
            btn.AddThemeStyleboxOverride("pressed", style);
            btn.AddThemeStyleboxOverride("focus", style);
        }

        /// <summary>
        /// Updates the info labels at the top and toggles the Start button state.
        /// </summary>
        private void UpdateSelectionInfo()
        {
            if (PlayerSelectedLabel != null)
            {
                PlayerSelectedLabel.Text = _playerChoice != null ? _playerChoice.Name : "-";
                PlayerSelectedLabel.Modulate = _playerChoice != null ? _colPlayer : Colors.Gray;
            }

            if (BotSelectedLabel != null)
            {
                BotSelectedLabel.Text = _botChoice != null ? _botChoice.Name : "-";
                BotSelectedLabel.Modulate = _botChoice != null ? _colBot : Colors.Gray;
            }

            if (StartGameButton != null)
            {
                bool ready = _playerChoice != null && _botChoice != null;
                StartGameButton.Disabled = !ready;
                StartGameButton.Modulate = ready ? Colors.White : new Color(1, 1, 1, 0.5f);
            }
        }

        /// <summary>
        /// Saves selections to GameSession and transitions to the gameplay scene.
        /// </summary>
        private void OnStartGamePressed()
        {
            if (_playerChoice == null || _botChoice == null) return;

            if (GameSession.Instance != null)
            {
                GameSession.Instance.SelectedPlayerDeck = _playerChoice;
                GameSession.Instance.SelectedBotDeck = _botChoice;
            }

            GetTree().ChangeSceneToFile(GAME_SCENE_PATH);
        }

        #endregion
    }
}