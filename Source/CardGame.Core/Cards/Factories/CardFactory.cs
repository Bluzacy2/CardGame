using System;

using CardGame.Core.Cards.Data;
using CardGame.Core.Application;
using CardGame.Core.Cards.Models;
namespace CardGame.Core.Cards.Factories
{
    public class CardFactory
    {
        private readonly CardLibrary _library;
        private readonly DeterministicRng _rng;

        public CardFactory(CardLibrary library, DeterministicRng rng)
        {
            _library = library;
            _rng = rng;
        }

        /* -------------- FACTORY METHOD -------------- */
        public CardInstance CreateCard(int definitionId, int ownerPlayerId)
        {
            var definition = _library.CreateDefinition(definitionId);
            int instanceId = _rng.NextId(); // Unikalne ID instancji karty
            return new CardInstance(instanceId, ownerPlayerId, definition);
        }
    }
}
