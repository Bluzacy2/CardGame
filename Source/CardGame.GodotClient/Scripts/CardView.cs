using Godot;
using System;
using System.Linq;
using CardGame.Core.Cards.Models;
using CardGame.Core.Cards.Data;
using System.Text.RegularExpressions;

/// <summary>
/// Controls the visual representation of a single card in the game UI.
/// Handles rendering card data (stats, text, art), input interactions (hover, click),
/// and visual states (highlighting, selection).
/// </summary>
public partial class CardView : Control
{
    #region UI Elements (Exports)
    [ExportGroup("UI Elements")]
    /// <summary>Label displaying the card's name.</summary>
    [Export] public Label NameLabel;
    /// <summary>Label displaying the mana/blood cost.</summary>
    [Export] public Label CostLabel;
    /// <summary>Label displaying current health.</summary>
    [Export] public Label HpLabel;
    /// <summary>Label displaying current attack power.</summary>
    [Export] public Label AtkLabel;
    /// <summary>Label for card description/flavor text.</summary>
    [Export] public RichTextLabel DescLabel;
    /// <summary>Label for card subtypes (e.g. "Monster Animal").</summary>
    [Export] public RichTextLabel SubtypeLabel;
    /// <summary>Label for displaying keywords (e.g. "Flying, Armored").</summary>
    [Export] public RichTextLabel KeywordsLabel;

    /// <summary>The main background texture or frame of the card.</summary>
    [Export] public TextureRect Background;
    /// <summary>The texture displaying the card's illustration/art.</summary>
    [Export] public TextureRect Illustration;
    /// <summary>Icon/Frame for the attack value.</summary>
    [Export] public TextureRect AttackSquare;
    /// <summary>Icon/Frame for the health value.</summary>
    [Export] public TextureRect HealthSquare;

    /// <summary>Border control used for targeting highlights.</summary>
    [Export] public Control HighlightBorder;
    /// <summary>Container for keyword icons (optional/future use).</summary>
    [Export] public HBoxContainer KeywordContainer;
    /// <summary>Overlay used to indicate selection (e.g. during Mulligan).</summary>
    [Export] public Control XOverlay;
    /// <summary>Badge displaying card count (used in Deck Editor).</summary>
    [Export] public Label CountBadge;
    #endregion

    #region Properties & Events
    /// <summary>
    /// Gets the data model associated with this card view.
    /// </summary>
    public CardInstance MyCardData { get; private set; }

    /// <summary>
    /// Event triggered when the card is clicked.
    /// </summary>
    public event Action<CardView> OnClicked;

    private Vector2 _baseScale = Vector2.One;
    #endregion

    #region Lifecycle Methods

    /// <summary>
    /// Initializes the card view, setting default size, mouse filter, and visual properties.
    /// Configures child controls to scale properly.
    /// </summary>
    public override void _Ready()
    {
        // 1. Basic size setup (200x280 for Grid performance)
        CustomMinimumSize = new Vector2(200, 280);
        MouseFilter = MouseFilterEnum.Stop; // Allows catching input events
        ClipContents = true; // Clips children (like art) to card boundaries

        // 2. Background setup
        if (Background != null)
        {
            Background.SetAnchorsPreset(LayoutPreset.FullRect);
            Background.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            Background.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        }

        // 3. Setup hover events
        MouseEntered += OnHoverEnter;
        MouseExited += OnHoverExit;

        // 4. Auto-configure children layout
        foreach (var child in GetChildren())
        {
            if (child is Control control && child != Background)
            {
                // Allow containers (e.g., Grid) to manage card size
                control.SizeFlagsHorizontal = SizeFlags.Expand | SizeFlags.Fill;
                control.SizeFlagsVertical = SizeFlags.Expand | SizeFlags.Fill;
            }
        }
    }

    /// <summary>
    /// Handles GUI input events directly on the card control.
    /// Detects left clicks to trigger interaction.
    /// </summary>
    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
        {
            OnClicked?.Invoke(this);
        }
    }

    #endregion

    #region Rendering Logic

    /// <summary>
    /// Updates the card's visual state based on the provided card data.
    /// Sets text, stats, colors, and art.
    /// </summary>
    /// <param name="card">The card data instance to display.</param>
    public void Render(CardInstance card)
    {
        if (card == null) return;
        MyCardData = card;

        if (Background != null) Background.Visible = true;

        // 1. Basic Text
        if (NameLabel != null) NameLabel.Text = card.Definition.Name.ToUpper();
        if (DescLabel != null) DescLabel.Text = card.Definition.Description;

        if (SubtypeLabel != null)
        {
            SubtypeLabel.BbcodeEnabled = true;
            string subtypeText = "";

            if (card.Definition.Type == CardType.Spell)
            {
                subtypeText = "Action";
            }
            else if (card.Definition.Subtypes != null && card.Definition.Subtypes.Any())
            {
                // Join subtypes with spaces, formatted to Title Case
                var formattedSubtypes = card.Definition.Subtypes.Select(s => ToTitleCase(s));
                subtypeText = string.Join(" ", formattedSubtypes);
            }

            SubtypeLabel.Text = !string.IsNullOrEmpty(subtypeText) ? $"[center]- {subtypeText} -[/center]" : "";
        }

        // --- KEYWORDS ---
        if (KeywordsLabel != null)
        {
            KeywordsLabel.BbcodeEnabled = true;

            var keywords = card.CurrentStats.Keywords
                .Where(k => k != Keyword.None && k != Keyword.SoulGuardDepleted && k != Keyword.BurnSource)
                .Select(k => GetKeywordDisplayText(k, card))
                .ToList();

            if (keywords.Count > 0)
            {
                string kwRaw = string.Join(". ", keywords) + ".";
                ApplySmartFontSize(KeywordsLabel, kwRaw, 15);
                KeywordsLabel.Text = $"[center][b]{kwRaw}[/b][/center]";
                KeywordsLabel.Visible = true;
            }
            else KeywordsLabel.Visible = false;
        }

        // 2. Cost and Roman Numeral Logic
        if (CostLabel != null)
        {
            CostLabel.Text = IntToRoman(card.CurrentStats.BloodCost);

            // Color logic: Green if cheaper, Red if more expensive or base
            if (card.CurrentStats.BloodCost < card.Definition.BaseStats.BloodCost)
            {
                CostLabel.Modulate = Colors.Green;
            }
            else
            {
                CostLabel.Modulate = Colors.Red;
            }
        }

        // 3. Unit Statistics
        if (card.Definition.Type == CardType.Unit)
        {
            ShowUnitStats(true);
            if (AtkLabel != null)
            {
                AtkLabel.Text = card.CurrentStats.Attack.ToString();
                AtkLabel.Modulate = card.CurrentStats.Attack > card.Definition.BaseStats.Attack ? Colors.Green : Colors.White;
            }
            if (HpLabel != null)
            {
                HpLabel.Text = card.CurrentStats.Health.ToString();
                if (card.DamageTaken > 0) HpLabel.Modulate = Colors.Red;
                else if (card.CurrentStats.Health > card.Definition.BaseStats.Health) HpLabel.Modulate = Colors.Green;
                else HpLabel.Modulate = Colors.White;
            }

            if (Background != null) Background.SelfModulate = Colors.White;
        }
        else
        {
            ShowUnitStats(false);
            if (Background != null) Background.SelfModulate = new Color(0.6f, 0.6f, 1.0f); // Blue tint for Spells
        }

        // 4. Artwork Loading
        if (Illustration != null)
        {
            string imgPath = $"res://Assets/Cards/Cards/CardImages/{card.Definition.Id}.png";
            if (FileAccess.FileExists(imgPath))
                Illustration.Texture = GD.Load<Texture2D>(imgPath);
            else
                Illustration.Texture = GD.Load<Texture2D>("res://Assets/Cards/Cards/missing_texture.png");
        }

        RenderKeywords(card);
    }

    /// <summary>
    /// Renders the card back (face down state).
    /// Hides all stats and text, shows a card back texture or color.
    /// </summary>
    public void RenderCardBack()
    {
        MyCardData = null;
        ShowUnitStats(false);

        // Hide all text elements
        if (NameLabel != null) { NameLabel.Text = ""; NameLabel.Visible = false; }
        if (DescLabel != null) { DescLabel.Text = ""; DescLabel.Visible = false; }
        if (CostLabel != null) { CostLabel.Visible = false; }
        if (SubtypeLabel != null) SubtypeLabel.Visible = false;
        if (KeywordsLabel != null) KeywordsLabel.Visible = false;
        if (AttackSquare != null) AttackSquare.Visible = false;
        if (HealthSquare != null) HealthSquare.Visible = false;

        if (Background != null) Background.Visible = false;

        // Use Illustration as Card Back
        if (Illustration != null)
        {
            Illustration.Visible = true;

            // Create a 1x1 Black texture
            var image = Image.Create(1, 1, false, Image.Format.Rgba8);
            image.Fill(Colors.Black);
            var texture = ImageTexture.CreateFromImage(image);

            Illustration.Texture = texture;
            Illustration.SelfModulate = Colors.White;
            Illustration.Modulate = Colors.White;

            // Stretch to fill card
            Illustration.SetAnchorsPreset(LayoutPreset.FullRect);
            Illustration.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            Illustration.StretchMode = TextureRect.StretchModeEnum.Scale;
        }
    }

    #endregion

    #region Visual Effects & Interaction

    private void OnHoverEnter()
    {
        // Enlarge only cards in the player's hand
        if (GetParent() != null && GetParent().Name == "HandContainer")
        {
            ZIndex = 100;
            var tween = CreateTween();
            tween.SetParallel(true);
            tween.TweenProperty(this, "scale", new Vector2(1.2f, 1.2f), 0.1f);
            tween.TweenProperty(this, "position:y", -100, 0.1f);
        }
    }

    private void OnHoverExit()
    {
        if (GetParent() != null && GetParent().Name == "HandContainer")
        {
            ZIndex = 0;
            var tween = CreateTween();
            tween.SetParallel(true);
            tween.TweenProperty(this, "scale", Vector2.One, 0.1f);
            tween.TweenProperty(this, "position:y", 0, 0.1f);
        }
    }

    /// <summary>
    /// Toggles the visual highlight effect on the card border.
    /// </summary>
    /// <param name="active">True to show highlight, false to hide.</param>
    /// <param name="color">The color of the highlight.</param>
    public void SetHighlight(bool active, Color color)
    {
        if (HighlightBorder != null)
        {
            HighlightBorder.Visible = active;
            HighlightBorder.Modulate = color;
        }
    }

    /// <summary>
    /// Sets the visual state for Mulligan selection (e.g., overlay X).
    /// </summary>
    public void SetMulliganSelected(bool selected)
    {
        if (XOverlay != null) XOverlay.Visible = selected;
        Modulate = selected ? new Color(0.6f, 0.6f, 0.6f) : new Color(1, 1, 1);
    }

    /// <summary>
    /// Updates the visual count badge (used in Deck Editor).
    /// </summary>
    public void UpdateCount(int current, int max)
    {
        if (CountBadge == null) return;
        CountBadge.Visible = true;
        CountBadge.Text = $"{current}/{max}";
        CountBadge.Modulate = current >= max ? Colors.Red : Colors.White;
        Modulate = current >= max ? new Color(0.5f, 0.5f, 0.5f) : Colors.White;
    }

    #endregion

    #region Drag & Drop

    /// <summary>
    /// Generates drag data when the user starts dragging the card.
    /// Creates a visual preview of the card under the cursor.
    /// </summary>
    /// <returns>The InstanceID of the card data.</returns>
    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (MyCardData == null) return default;

        var preview = new Control();
        var visual = (Control)Duplicate();
        preview.AddChild(visual);

        visual.MouseFilter = MouseFilterEnum.Ignore;
        visual.Position = new Vector2(-100, -140);
        visual.Modulate = new Color(1, 1, 1, 0.8f);
        visual.RotationDegrees = 5;

        SetDragPreview(preview);
        return MyCardData.InstanceId;
    }

    #endregion

    #region Private Helper Methods

    private void ShowUnitStats(bool show)
    {
        if (AtkLabel != null) AtkLabel.Visible = show;
        if (HpLabel != null) HpLabel.Visible = show;
        if (AttackSquare != null) AttackSquare.Visible = show;
        if (HealthSquare != null) HealthSquare.Visible = show;
    }

    private void RenderKeywords(CardInstance card)
    {
        if (card.CurrentStats.Keywords.Contains(Keyword.SoulGuard)) SetHighlight(true, Colors.Gray);
        else if (card.CurrentStats.Keywords.Contains(Keyword.Marked)) SetHighlight(true, Colors.Red);
        else SetHighlight(false, Colors.White);
    }

    private string IntToRoman(int n)
    {
        string[] romans = { "0", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X" };
        return (n >= 0 && n < romans.Length) ? romans[n] : n.ToString();
    }

    private string ToTitleCase(string input)
    {
        if (string.IsNullOrEmpty(input)) return "";
        return char.ToUpper(input[0]) + input.Substring(1).ToLower();
    }

    private string FormatText(string input)
    {
        if (string.IsNullOrEmpty(input)) return "";
        string spaced = Regex.Replace(input, "([a-z])([A-Z])", "$1 $2");
        return char.ToUpper(spaced[0]) + spaced.Substring(1).ToLower();
    }

    private string GetKeywordDisplayText(Keyword k, CardInstance card)
    {
        string text = FormatText(k.ToString());
        if (card.CurrentStats.KeywordParams.TryGetValue(k, out int value))
        {
            return $"{text} {value}";
        }
        return text;
    }

    private void ApplySmartFontSize(RichTextLabel label, string text, int baseSize)
    {
        if (label == null) return;

        int length = text.Length;
        int fontSize = baseSize;

        if (length > 60) fontSize = (int)(baseSize * 0.5f);
        else if (length > 45) fontSize = (int)(baseSize * 0.65f);
        else if (length > 30) fontSize = (int)(baseSize * 0.8f);

        label.AddThemeFontSizeOverride("normal_font_size", fontSize);
        label.AddThemeFontSizeOverride("bold_font_size", fontSize);
        label.AddThemeFontSizeOverride("italics_font_size", fontSize);
    }

    #endregion
}