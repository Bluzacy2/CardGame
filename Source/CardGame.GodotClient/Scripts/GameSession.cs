using Godot;
using CardGame.Core.Decks.Data;

namespace CardGame.GodotClient
{
    /// <summary>
    /// A persistent Singleton (Autoload) that stores global game data across scene transitions.
    /// Used to pass selected deck configurations from the Menu scenes to the Gameplay scene.
    /// This node persists throughout the application lifecycle.
    /// </summary>
    public partial class GameSession : Node
    {
        /// <summary>
        /// The global singleton instance accessible from any script.
        /// </summary>
        public static GameSession Instance { get; private set; }

        /// <summary>
        /// The deck data selected by the human player for the upcoming match.
        /// Used by GameBootstrap to initialize the player's hand and deck.
        /// </summary>
        public DeckData SelectedPlayerDeck { get; set; }

        /// <summary>
        /// The deck data selected for the AI opponent for the upcoming match.
        /// Used by GameBootstrap to initialize the opponent's hand and deck.
        /// </summary>
        public DeckData SelectedBotDeck { get; set; }

        /// <summary>
        /// Initializes the singleton instance when the node enters the scene tree.
        /// </summary>
        public override void _Ready()
        {
            Instance = this;
        }

        /// <summary>
        /// Clears the stored session data (e.g., when returning to the main menu).
        /// Ensures subsequent games don't accidentally use stale deck selections.
        /// </summary>
        public void Reset()
        {
            SelectedPlayerDeck = null;
            SelectedBotDeck = null;
        }
    }
}