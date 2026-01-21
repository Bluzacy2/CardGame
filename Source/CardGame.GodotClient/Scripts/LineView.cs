using Godot;
using System;
using CardGame.Core.State.Models;
using CardGame.Core.Cards.Models;

namespace CardGame.GodotClient
{
    /// <summary>
    /// Visual controller for a single board lane (Line).
    /// Manages the placement of Enemy units (Top) and Player units (Bottom).
    /// </summary>
    public partial class LineView : Control
    {
        #region Scene References
        /// <summary>Label displaying the line number (e.g., I, II, III, IV).</summary>
        [Export] public Label InfoLabel;
        /// <summary>Container for the enemy's unit (Top slot).</summary>
        [Export] public Control EnemySlot;
        /// <summary>Container for the player's unit (Bottom slot).</summary>
        [Export] public Control PlayerSlot;
        #endregion

        /// <summary>
        /// Signal emitted when a card is dropped onto this line.
        /// </summary>
        /// <param name="cardInstanceId">The ID of the dropped card instance.</param>
        /// <param name="lineIndex">The index of this line.</param>
        [Signal] public delegate void CardDroppedOnLineEventHandler(int cardInstanceId, int lineIndex);

        private int _myIndex;
        private readonly string[] _romans = { "I", "II", "III", "IV" };

        public override void _Ready()
        {
            MouseFilter = MouseFilterEnum.Stop; // Needed to receive Drop events
        }

        // --- ZMIANA: Dodano parametr onClickHandler ---
        /// <summary>
        /// Renders the current state of the line, updating unit visuals for both slots.
        /// </summary>
        /// <param name="lineData">The data model representing this line.</param>
        /// <param name="cardScene">The template scene for creating unit visuals.</param>
        /// <param name="onClickHandler">Callback action for unit click events (targeting).</param>
        public void Render(Line lineData, PackedScene cardScene, Action<CardView> onClickHandler)
        {
            _myIndex = lineData.Index;

            if (InfoLabel != null)
            {
                InfoLabel.Text = _romans[_myIndex];
                InfoLabel.HorizontalAlignment = HorizontalAlignment.Center;
                InfoLabel.VerticalAlignment = VerticalAlignment.Center;
                InfoLabel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
                InfoLabel.Modulate = new Color(1, 1, 1, 0.2f);
            }

            ClearSlot(EnemySlot);
            ClearSlot(PlayerSlot);

            // 2. Enemies (Top)
            if (lineData.Player2Unit != null)
            {
                var botUnit = cardScene.Instantiate<CardView>();
                EnemySlot.AddChild(botUnit);
                botUnit.Render(lineData.Player2Unit);

                botUnit.MouseFilter = MouseFilterEnum.Stop; // Must be Stop to receive clicks
                botUnit.Modulate = new Color(1, 0.8f, 0.8f);

                // Connect click handler
                if (onClickHandler != null) botUnit.OnClicked += onClickHandler;
            }

            // 3. Player (Bottom)
            if (lineData.Player1Unit != null)
            {
                var myUnit = cardScene.Instantiate<CardView>();
                PlayerSlot.AddChild(myUnit);
                myUnit.Render(lineData.Player1Unit);

                myUnit.MouseFilter = MouseFilterEnum.Stop;

                // Connect click handler
                if (onClickHandler != null) myUnit.OnClicked += onClickHandler;
            }
        }

        /// <summary>
        /// Clears all children from a specific slot container, preserving Ghost units.
        /// </summary>
        private void ClearSlot(Control slot)
        {
            if (slot == null) return;
            foreach (Node child in slot.GetChildren())
            {
                // Skip the Ghost Unit (identified by transparency) to prevent flickering during targeting
                if (child is CardView cv && cv.Modulate.A < 0.9f) continue;
                child.QueueFree();
            }
        }

        /// <summary>
        /// Checks if drag data is valid for dropping on this line (expects Card ID int).
        /// </summary>
        public override bool _CanDropData(Vector2 atPosition, Variant data)
        {
            return data.VariantType == Variant.Type.Int;
        }

        /// <summary>
        /// Handles the drop event, emitting a signal to the GameBootstrap/InputController.
        /// </summary>
        public override void _DropData(Vector2 atPosition, Variant data)
        {
            int cardId = (int)data;
            EmitSignal(SignalName.CardDroppedOnLine, cardId, _myIndex);
        }
    }
}