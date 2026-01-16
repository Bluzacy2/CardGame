using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Factories;
using CardGame.Core.Cards.Models;
using CardGame.Core.State.Models;
using System.Collections.Generic;
using System.Linq;

namespace CardGame.Core.AI.Logic
{
    /// <summary>
    /// Simulates a realistic opponent for AI decision making by injecting likely dangerous cards into the opponent's hand.
    /// </summary>
    public class VirtualOpponent
    {
        private readonly CardFactory _factory;

        /// <summary>
        /// Initializes a new instance of the VirtualOpponent class.
        /// </summary>
        /// <param name="factory">The card factory for creating card instances.</param>
        public VirtualOpponent(CardFactory factory)
        {
            _factory = factory;
        }

        #region Public Methods

        /// <summary>
        /// Creates a copy of the game state where the opponent has the most probable and dangerous cards
        /// from their deck in hand, allowing the AI to anticipate potential counterplays.
        /// </summary>
        /// <param name="state">The current game state to modify.</param>
        /// <param name="enemyPlayerId">The ID of the opponent player.</param>
        /// <returns>A modified game state with injected phantom cards in the opponent's hand.</returns>
        public GameState InjectRealisticPhantomHand(GameState state, int enemyPlayerId)
        {
            var enemy = state.GetPlayer(enemyPlayerId);
            var phantomCards = new List<CardInstance>();

            // 1. DECK TRACKING: Analyze what remains in the opponent's deck.
            // The bot "remembers" which cards have not been played yet.
            var availableInDeck = enemy.DrawPile
                .GroupBy(c => c.Definition.Id)
                .Select(g => g.First())
                .OrderByDescending(c => EvaluateDangerLevel(c))
                .Take(2); // Inject the 2 most dangerous potential responses

            foreach (var cardDef in availableInDeck)
            {
                try
                {
                    // Create a virtual card instance for simulation purposes
                    int id = int.Parse(cardDef.Definition.Id);
                    phantomCards.Add(_factory.CreateCard(id, enemyPlayerId));
                }
                catch
                {
                    // Ignore ID parsing errors 
                }
            }

            // 2. SIMULATION STATE UPDATE
            var newEnemyState = enemy;
            foreach (var card in phantomCards)
            {
                // Add cards to the opponent's hand in the virtual world
                newEnemyState = newEnemyState.WithCardAddedToHand(card);
            }

            return state.UpdatePlayer(newEnemyState);
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Evaluates how much the bot should fear a specific card.
        /// </summary>
        /// <param name="c">The card instance to evaluate.</param>
        /// <returns>A danger level score (higher = more dangerous).</returns>
        private int EvaluateDangerLevel(CardInstance c)
        {
            string id = c.Definition.Id;

            // Priority 1: Destroying/reactive spells (Snipers)
            if (id == "7") return 10;  // Glock-17 (Direct damage)
            if (id == "31") return 9;  // Silence (Destroys synergies/Unkillable)
            if (id == "16") return 8;  // HellFire (AoE - clears the board)
            if (id == "18") return 7;  // Final Mission (Sacrifice removal)

            // Priority 2: Strong units
            if (c.CurrentStats.Attack >= 5) return 6;
            if (c.CurrentStats.Keywords.Contains(Keyword.SplashDamage)) return 5;

            return 1; // Other cards are low priority
        }

        #endregion
    }
}