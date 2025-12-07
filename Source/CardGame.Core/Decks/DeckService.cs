using System;

using CardGame.Core.Decks.Data;
using System.Collections.Generic;
using CardGame.Core.Cards.Models;
using CardGame.Core.Cards.Factories;

namespace CardGame.Core.Decks
{
    public class DeckService
    {
        private readonly DeckRepository _repository;
        private readonly CardFactory _cardFactory;

        public DeckService(DeckRepository repository, CardFactory cardFactory)
        {
            _repository = repository;
            _cardFactory = cardFactory;
        }

        // Główna metoda: Daj mi ID talii i gracza, a dam Ci gotową listę kart
        public List<CardInstance> CreateDeckFromId(string deckId, int ownerId)
        {
            var deckData = _repository.GetDeck(deckId);
            var cardInstances = new List<CardInstance>();

            foreach (int cardDefId in deckData.CardIds)
            {
                // Tu używamy naszej fabryki, która nadaje unikalne InstanceID!
                var card = _cardFactory.CreateCard(cardDefId, ownerId);
                cardInstances.Add(card);
            }

            return cardInstances;
        }
    }
}

