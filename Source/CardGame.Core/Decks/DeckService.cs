using CardGame.Core.Cards.Factories;
using CardGame.Core.Cards.Models;
using CardGame.Core.Decks.Data;
using System;
using System.Collections.Generic;

namespace CardGame.Core.Decks
{
    /// <summary>
    /// Service for creating decks of card instances from deck data definitions.
    /// </summary>
    public class DeckService
    {
        private readonly DeckRepository _repository;
        private readonly CardFactory _cardFactory;

        /// <summary>
        /// Initializes a new instance of the DeckService class.
        /// </summary>
        /// <param name="repository">The repository providing deck data definitions.</param>
        /// <param name="cardFactory">The factory for creating card instances.</param>
        public DeckService(DeckRepository repository, CardFactory cardFactory)
        {
            _repository = repository;
            _cardFactory = cardFactory;
        }

        #region Public Methods

        /// <summary>
        /// Creates a list of card instances from a deck definition ID for a specific owner.
        /// </summary>
        /// <param name="deckId">The unique identifier of the deck definition.</param>
        /// <param name="ownerId">The ID of the player who will own these cards.</param>
        /// <returns>A list of card instances representing the complete deck.</returns>
        public List<CardInstance> CreateDeckFromId(string deckId, int ownerId)
        {
            var deckData = _repository.GetDeck(deckId);
            var cardInstances = new List<CardInstance>();

            foreach (int cardDefId in deckData.CardIds)
            {
                var card = _cardFactory.CreateCard(cardDefId, ownerId);
                cardInstances.Add(card);
            }

            return cardInstances;
        }

        #endregion
    }
}