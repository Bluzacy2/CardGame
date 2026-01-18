using Godot;
using System;
using System.IO;
using System.Text.Json;
using CardGame.Core.Decks.Data;
using System.Collections.Generic;
using System.Linq;

public partial class GameDeckSelector : Control
{
    // --- REFERENCJE UI ---
    [Export] public VBoxContainer DeckListContainer;
    [Export] public Button StartGameButton;
    [Export] public Button BackButton;

    [Export] public Label PlayerSelectedLabel;
    [Export] public Label BotSelectedLabel;

    // --- ŚCIEŻKI ---
    private const string MAIN_MENU_PATH = "res://Scenes/MainMenu.tscn";
    private const string GAME_SCENE_PATH = "res://Scenes/Main.tscn";

    // --- STAN ---
    private DeckData _playerChoice;
    private DeckData _botChoice;

    private Dictionary<string, Button> _deckButtons = new();

    // Kolory
    private readonly Color _colPlayer = new Color(0.2f, 0.9f, 0.2f); // Zielony
    private readonly Color _colBot = new Color(0.9f, 0.2f, 0.2f);    // Czerwony
    private readonly Color _colDark = new Color(0.15f, 0.15f, 0.15f); // Tło

    // --- SHADER SDF (Rounded Box + Gradient) ---
    private const string BORDER_SHADER_CODE = @"
        shader_type canvas_item;
        
        uniform vec4 color_left : source_color;
        uniform vec4 color_right : source_color;
        uniform vec4 bg_color : source_color;
        
        uniform vec2 size; // Rozmiar przycisku w pikselach
        uniform float radius = 8.0; // Promień zaokrąglenia
        uniform float width = 4.0;  // Grubość ramki

        // Funkcja obliczająca dystans od zaokrąglonego prostokąta
        float sdRoundedBox(vec2 p, vec2 b, float r) {
            vec2 q = abs(p) - b + r;
            return min(max(q.x, q.y), 0.0) + length(max(q, 0.0)) - r;
        }

        void fragment() {
            vec2 half_size = size * 0.5;
            vec2 pos = (UV * size) - half_size; // Pozycja w pikselach względem środka
            
            // Obliczamy dystans od krawędzi
            float d = sdRoundedBox(pos, half_size, radius);
            
            // --- GRADIENT (LEWO -> PRAWO ze skosem) ---
            // UV.x decyduje o głównej osi (0=Lewo, 1=Prawo).
            // (UV.y - 0.5) * 0.1 dodaje skos.
            // Zielony będzie bardziej z lewej i góry, Czerwony z prawej i dołu.
            float t = UV.x + (UV.y - 0.5) * 0.1;
            
            // Wygładzamy przejście na środku
            t = smoothstep(0.4, 0.6, t);
            
            vec4 border_col = mix(color_left, color_right, t);
            
            if (d > 0.0) {
                // Na zewnątrz zaokrąglenia - przezroczyste (żeby przyciąć rogi)
                COLOR = vec4(0.0);
            } else if (d > -width) {
                // Ramka
                COLOR = border_col;
            } else {
                // Środek
                COLOR = bg_color;
            }
        }
    ";

    private ShaderMaterial _baseShaderMaterial;

    public override void _Ready()
    {
        // Kompilacja Shadera raz na starcie
        var shader = new Shader();
        shader.Code = BORDER_SHADER_CODE;
        _baseShaderMaterial = new ShaderMaterial();
        _baseShaderMaterial.Shader = shader;

        // Ustawiamy stałe kolory
        _baseShaderMaterial.SetShaderParameter("color_left", _colPlayer);
        _baseShaderMaterial.SetShaderParameter("color_right", _colBot);
        _baseShaderMaterial.SetShaderParameter("bg_color", _colDark);

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

    private void CreateDeckButton(DeckData deck)
    {
        var mainFont = GD.Load<Font>("res://Assets/Fonts/Ari-CBold.ttf");

        var btn = new Button();
        btn.Text = $"{deck.Name.ToUpper()} ({deck.CardIds.Count} KART)";
        btn.CustomMinimumSize = new Vector2(0, 60);
        btn.Alignment = HorizontalAlignment.Left;
        btn.ActionMode = BaseButton.ActionModeEnum.Press;
        btn.SizeFlagsHorizontal = SizeFlags.ExpandFill;

        // --- CHILD COLOR RECT (Twoja specjalna obramówka z Shaderem) ---
        var gradientRect = new ColorRect();
        gradientRect.Name = "GradientBorder";
        gradientRect.MouseFilter = MouseFilterEnum.Ignore;
        gradientRect.SetAnchorsPreset(LayoutPreset.FullRect);
        gradientRect.ShowBehindParent = true;
        gradientRect.Visible = false;
        gradientRect.Material = (Material)_baseShaderMaterial.Duplicate();

        btn.AddChild(gradientRect);

        // Czcionka
        if (mainFont != null)
        {
            btn.AddThemeFontOverride("font", mainFont);
            btn.AddThemeFontSizeOverride("font_size", 22);
        }

        // Obsługa sygnałów
        btn.Resized += () => UpdateShaderSize(btn, gradientRect);
        btn.GuiInput += (inputEvent) => OnDeckRowClicked(inputEvent, deck);

        _deckButtons[deck.Id] = btn;
        DeckListContainer.AddChild(btn);

        ApplyButtonStyle(btn, false, false);
    }

    private void UpdateShaderSize(Button btn, ColorRect rect)
    {
        // Przekazujemy aktualny rozmiar przycisku do shadera, żeby zaokrąglenia były ładne
        if (rect.Material is ShaderMaterial mat)
        {
            mat.SetShaderParameter("size", btn.Size);
        }
    }

    private void OnDeckRowClicked(InputEvent inputEvent, DeckData deck)
    {
        if (inputEvent is InputEventMouseButton mb && mb.Pressed)
        {
            if (mb.ButtonIndex == MouseButton.Left)
            {
                _playerChoice = deck;
                if (_botChoice == null) _botChoice = deck;
            }
            else if (mb.ButtonIndex == MouseButton.Right)
            {
                _botChoice = deck;
            }

            UpdateAllButtonsVisuals();
            UpdateSelectionInfo();
        }
    }

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

    private void ApplyButtonStyle(Button btn, bool isPlayer, bool isBot)
    {
        var gradientRect = btn.GetNode<ColorRect>("GradientBorder");
        btn.RemoveThemeColorOverride("font_color");

        // Bazowy styl (Flat) dla pojedynczych wyborów
        var style = new StyleBoxFlat();
        style.CornerRadiusTopLeft = 8; style.CornerRadiusTopRight = 8;
        style.CornerRadiusBottomRight = 8; style.CornerRadiusBottomLeft = 8;
        style.ContentMarginLeft = 15;

        style.BorderWidthBottom = 2; style.BorderWidthTop = 2;
        style.BorderWidthLeft = 2; style.BorderWidthRight = 2;

        if (isPlayer && isBot)
        {
            // --- OBOJE: Włączamy Shader ---
            gradientRect.Visible = true;
            UpdateShaderSize(btn, gradientRect); // Upewniamy się, że rozmiar jest aktualny

            style.BgColor = new Color(0, 0, 0, 0); // Przezroczyste tło przycisku
            style.BorderWidthBottom = 0; style.BorderWidthTop = 0; // Wyłączamy ramkę StyleBoxa
            style.BorderWidthLeft = 0; style.BorderWidthRight = 0;

            btn.AddThemeColorOverride("font_color", new Color(1, 1, 0.6f));
        }
        else
        {
            // --- POJEDYNCZY: Wyłączamy Shader, używamy StyleBox ---
            gradientRect.Visible = false;
            style.BgColor = _colDark;

            if (isPlayer)
            {
                style.BorderColor = _colPlayer;
                btn.AddThemeColorOverride("font_color", _colPlayer);
                btn.AddThemeColorOverride("font_color", _colPlayer);
                btn.AddThemeColorOverride("font_focus_color", _colPlayer);
            }
            else if (isBot)
            {
                style.BorderColor = _colBot;
                btn.AddThemeColorOverride("font_color", _colBot);
                btn.AddThemeColorOverride("font_color", _colBot);
                btn.AddThemeColorOverride("font_focus_color", _colBot);
            }
            else
            {
                // NIKT
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
}