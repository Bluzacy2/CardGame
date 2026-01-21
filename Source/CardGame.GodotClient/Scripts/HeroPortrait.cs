using Godot;
using System;

namespace CardGame.GodotClient
{
    /// <summary>
    /// Visual component representing a player's hero.
    /// Displays Health, Resources (Blood), and the Avatar image.
    /// Handles interactions like targeting the hero with spells and receiving dropped cards.
    /// </summary>
    public partial class HeroPortrait : Control
    {
        #region UI Elements

        /// <summary>Label displaying the hero's current health.</summary>
        [Export] public Label HpLabel;

        /// <summary>Label displaying the hero's current/max resources (Blood).</summary>
        [Export] public Label ManaLabel;

        /// <summary>TextureRect displaying the hero's avatar image.</summary>
        [Export] public TextureRect Avatar;

        /// <summary>Control used to highlight the portrait during targeting sequences.</summary>
        [Export] public Control HighlightBorder;

        #endregion

        #region Properties & Events

        /// <summary>
        /// Gets or sets the ID of the player this portrait represents (1 or 2).
        /// </summary>
        public int OwnerId { get; set; }

        /// <summary>
        /// Event triggered when the hero portrait is clicked (e.g., when selected as a spell target).
        /// Sends the OwnerId of the clicked hero.
        /// </summary>
        public event Action<int> OnHeroClicked;

        #endregion

        #region Lifecycle Methods

        /// <summary>
        /// Called when the node enters the scene tree.
        /// Initializes visibility states and sets up input handling for clicks.
        /// </summary>
        public override void _Ready()
        {
            if (HighlightBorder != null) HighlightBorder.Visible = false;

            // Setup input handling directly on the Control
            // Requires MouseFilter to be set to Stop (default for Control)
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

        #endregion

        #region Public Methods

        /// <summary>
        /// Updates the visual statistics of the hero (Health and Mana/Blood).
        /// </summary>
        /// <param name="hp">Current health points.</param>
        /// <param name="currentMana">Current available resources.</param>
        /// <param name="maxMana">Maximum resource capacity.</param>
        public void UpdateStats(int hp, int currentMana, int maxMana)
        {
            if (HpLabel != null)
            {
                HpLabel.Text = hp.ToString();
                // Visual feedback: Red text when HP is critical (<= 10)
                HpLabel.Modulate = hp <= 10 ? Colors.Red : Colors.White;
            }

            if (ManaLabel != null)
            {
                ManaLabel.Text = $"{currentMana}/{maxMana}";
            }
        }

        /// <summary>
        /// Toggles the targeting highlight border on the portrait.
        /// </summary>
        /// <param name="active">If set to <c>true</c>, the highlight is shown.</param>
        /// <param name="color">The color of the highlight border.</param>
        public void SetHighlight(bool active, Color color)
        {
            if (HighlightBorder != null)
            {
                HighlightBorder.Visible = active;
                HighlightBorder.Modulate = color;
            }
        }

        #endregion

        #region Drag & Drop Support

        /// <summary>
        /// Determines whether the dragged data can be dropped onto the hero portrait.
        /// Accepts data of type <see cref="int"/> (representing a Card Instance ID).
        /// </summary>
        /// <param name="atPosition">The local position of the drag.</param>
        /// <param name="data">The data being dragged.</param>
        /// <returns><c>true</c> if the data is a card ID; otherwise, <c>false</c>.</returns>
        public override bool _CanDropData(Vector2 atPosition, Variant data)
        {
            return data.VariantType == Variant.Type.Int;
        }

        /// <summary>
        /// Handles the drop event when a card is released over the hero portrait.
        /// Useful for playing cards directly on the hero (e.g., direct damage spells or buffs).
        /// </summary>
        /// <param name="atPosition">The local position of the drop.</param>
        /// <param name="data">The dropped data (Card ID).</param>
        public override void _DropData(Vector2 atPosition, Variant data)
        {
            int cardId = (int)data;
            // In the future, this can emit a signal or call InputController to play the card on this target.
            GD.Print($"[UI] Dropped card {cardId} on Hero {OwnerId}");
        }

        #endregion
    }
}