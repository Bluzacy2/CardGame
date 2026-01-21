using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using CardGame.Core.Cards.Models; // Potrzebne do CardInstance

namespace CardGame.GodotClient
{
    /// <summary>
    /// UI Component responsible for displaying modal choices to the player.
    /// Supports both text-based options (buttons) and card selection grids (tutor).
    /// </summary>
    public partial class ChoiceModal : Control
    {
        #region Scene References

        /// <summary>Container for standard text-based option buttons.</summary>
        [Export] public Control ButtonContainer;
        /// <summary>Label displaying the title of the modal (e.g., "Choose an Option").</summary>
        [Export] public Label TitleLabel;

        /// <summary>Scroll container wrapping the card grid for tutor effects.</summary>
        [Export] public ScrollContainer CardGridScroll;
        /// <summary>Container for displaying cards in a grid layout.</summary>
        [Export] public Control CardGrid;

        #endregion

        private Action<int> _currentCallback;

        #region Lifecycle

        /// <summary>
        /// Initializes the modal, hiding it by default and setting up input blocking.
        /// </summary>
        public override void _Ready()
        {
            Visible = false;
            SetAnchorsPreset(LayoutPreset.FullRect);
            MouseFilter = MouseFilterEnum.Stop; // Blocks input to underlying game board
        }

        #endregion

        #region Configuration

        /// <summary>
        /// Sets the callback function to be executed when an option is selected.
        /// </summary>
        /// <param name="callback">Action receiving the selected index.</param>
        public void SetCallback(Action<int> callback)
        {
            _currentCallback = callback;
        }

        #endregion

        #region Button Mode (Text Options)

        /// <summary>
        /// Displays a list of text-based options as buttons (e.g., for modal spells like 'Expectancy').
        /// </summary>
        /// <param name="options">List of option text labels.</param>
        /// <param name="enabledStates">Optional list of booleans enabling/disabling specific options.</param>
        public void ShowOptions(IEnumerable<string> options, List<bool> enabledStates = null)
        {
            SetupView(mode: 0); // 0 = Buttons Mode

            // Clear existing children from both containers to prevent conflicts
            foreach (Node child in CardGrid.GetChildren()) child.QueueFree();

            Visible = true;
            MoveToFront();

            if (ButtonContainer == null) return;

            foreach (Node child in ButtonContainer.GetChildren()) child.QueueFree();

            int index = 0;
            var list = options.ToList();
            foreach (var txt in list)
            {
                var btn = new Button();
                btn.Text = txt;
                btn.CustomMinimumSize = new Vector2(300, 60);

                // Validation logic (disable button if action is invalid)
                bool isEnabled = (enabledStates == null) || (index >= enabledStates.Count) || enabledStates[index];
                btn.Disabled = !isEnabled;

                int capture = index;
                btn.Pressed += () => OptionClicked(capture);
                ButtonContainer.AddChild(btn);
                index++;
            }
        }

        #endregion

        #region Grid Mode (Card Selection)

        /// <summary>
        /// Displays a grid of cards for selection (e.g., for Tutor effects like 'Critical Thinking').
        /// </summary>
        /// <param name="cards">List of card instances to display.</param>
        /// <param name="cardTemplate">The scene template used to instantiate card visuals.</param>
        public void ShowCardGrid(List<CardInstance> cards, PackedScene cardTemplate)
        {
            SetupView(mode: 1); // 1 = Grid Mode

            if (CardGrid == null)
            {
                GD.PrintErr("CRITICAL: CardGrid is null! Assign it in Inspector.");
                return;
            }
            if (cardTemplate == null)
            {
                GD.PrintErr("CRITICAL: cardTemplate is null! UIManager did not provide template.");
                return;
            }

            GD.Print($"[MODAL] Displaying Grid. Card count: {cards.Count}");

            foreach (Node child in CardGrid.GetChildren())
                child.QueueFree();

            int index = 0;
            foreach (var card in cards)
            {
                try
                {
                    var node = cardTemplate.Instantiate();
                    var cardView = node as CardView;

                    if (cardView == null)
                    {
                        GD.PrintErr($"[MODAL] Error: Card scene does not contain CardView script! Node type: {node.GetType().Name}");
                        continue;
                    }

                    CardGrid.AddChild(cardView);
                    cardView.Render(card);
                    cardView.CustomMinimumSize = new Vector2(140, 190);
                    cardView.MouseFilter = MouseFilterEnum.Stop;

                    int capturedIndex = index;
                    cardView.OnClicked += (cv) => OptionClicked(capturedIndex);

                    index++;
                }
                catch (Exception e)
                {
                    GD.PrintErr($"[MODAL] Exception creating card: {e.Message}");
                }
            }

            // Force layout update
            if (CardGrid is Container c) c.QueueSort();
        }

        #endregion

        #region Internal Helpers

        /// <summary>
        /// Switches visibility between Button mode and Grid mode.
        /// </summary>
        private void SetupView(int mode)
        {
            Visible = true;
            MoveToFront();

            if (mode == 0) // Buttons
            {
                if (ButtonContainer != null) ButtonContainer.Visible = true;
                if (CardGridScroll != null) CardGridScroll.Visible = false;
                TitleLabel.Text = "WYBIERZ OPCJĘ";
            }
            else // Grid
            {
                if (ButtonContainer != null) ButtonContainer.Visible = false;
                if (CardGridScroll != null) CardGridScroll.Visible = true;
                TitleLabel.Text = "WYBIERZ KARTĘ Z TALII";
            }
        }

        private void OptionClicked(int index)
        {
            Visible = false;
            _currentCallback?.Invoke(index);
        }

        /// <summary>
        /// Hides the modal window.
        /// </summary>
        public void HideModal()
        {
            Visible = false;
        }

        #endregion
    }
}