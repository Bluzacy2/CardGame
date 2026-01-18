using CardGame.Core.Application;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Models;
using System;

namespace CardGame.Core.Cards.Factories
{
    /// <summary>
    /// Factory for creating card instances from definitions.
    /// </summary>
    public class CardFactory
    {
        private readonly CardLibrary _library;
        private readonly DeterministicRng _rng;

        /// <summary>
        /// Initializes a new instance of the CardFactory class.
        /// </summary>
        /// <param name="library">The card library containing definitions.</param>
        /// <param name="rng">The random number generator for instance IDs.</param>
        public CardFactory(CardLibrary library, DeterministicRng rng)
        {
            _library = library;
            _rng = rng;
        }

        #region Factory Methods

        /// <summary>
        /// Creates a new card instance from a definition ID.
        /// </summary>
        /// <param name="definitionId">The ID of the card definition.</param>
        /// <param name="ownerPlayerId">The ID of the player who owns this card.</param>
        /// <returns>A new CardInstance with a unique ID.</returns>
        public CardInstance CreateCard(int definitionId, int ownerPlayerId)
        {
            var definition = _library.CreateDefinition(definitionId);
            int instanceId = _rng.NextId(); // Unique card instance ID
            return new CardInstance(instanceId, ownerPlayerId, definition);
        }

        #endregion
    }
}