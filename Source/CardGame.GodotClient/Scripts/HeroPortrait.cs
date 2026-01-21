using Godot;
using System;
/// <summary>
/// Visual component representing a player's hero, displaying Health, Resources (Blood), and Avatar.
/// Handles interactions like targeting the hero with spells.
/// </summary>
public partial class HeroPortrait : Control
{
    [Export] public Label HpLabel;
    [Export] public Label ManaLabel;
    [Export] public TextureRect Avatar;
    [Export] public Control HighlightBorder;

    /// <summary>
    /// Gets or sets the ID of the player this portrait represents.
    /// </summary>
    public int OwnerId { get; set; }
    /// <summary>
    /// Event triggered when the hero portrait is clicked (e.g., for targeting).
    /// </summary>
    public event Action<int> OnHeroClicked;

    public override void _Ready()
    {
        if (HighlightBorder != null) HighlightBorder.Visible = false;
        this.GuiInput += (eventData) =>
        {
            if (eventData is InputEventMouseButton mb
                && mb.Pressed
                && mb.ButtonIndex == MouseButton.Left)
            {
                OnHeroClicked?.Invoke(OwnerId);
            }
        };
    }
    /// <summary>
    /// Updates the visual statistics of the hero.
    /// </summary>
    /// <param name="hp">Current health points.</param>
    /// <param name="currentMana">Current available blood/mana.</param>
    /// <param name="maxMana">Maximum blood/mana capacity.</param>
    public void UpdateStats(int hp, int currentMana, int maxMana)
    {
        if (HpLabel != null)
        {
            HpLabel.Text = hp.ToString();
            HpLabel.Modulate = hp <= 10 ? Colors.Red : Colors.White;
        }

        if (ManaLabel != null)
        {
            ManaLabel.Text = $"{currentMana}/{maxMana}";
        }
    }
    /// <summary>
    /// Toggles the targeting highlight effect on the portrait.
    /// </summary>
    /// <param name="active">True to show highlight, false to hide.</param>
    /// <param name="color">The color of the highlight border.</param>
    public void SetHighlight(bool active, Color color)
    {
        if (HighlightBorder != null)
        {
            HighlightBorder.Visible = active;
            HighlightBorder.Modulate = color;
        }
    }
    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        return data.VariantType == Variant.Type.Int; 
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        int cardId = (int)data;
        GD.Print($"[UI] Upuszczono kartę {cardId} na bohatera {OwnerId}");
    }
}