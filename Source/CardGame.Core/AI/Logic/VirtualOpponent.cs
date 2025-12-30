using System.Collections.Generic;
using CardGame.Core.Cards.Factories;
using CardGame.Core.Cards.Models;
using CardGame.Core.State.Models;

namespace CardGame.Core.AI.Logic
{
    // CLASS MODIFIED TO PUBLIC
    public class VirtualOpponent
    {
        private readonly CardFactory _factory;

        public VirtualOpponent(CardFactory factory)
        {
            _factory = factory;
        }

        public GameState InjectPhantomHand(GameState state, int enemyPlayerId)
        {
            var phantomCards = new List<CardInstance>();

            try
            {
                phantomCards.Add(_factory.CreateCard(99, enemyPlayerId)); 
                phantomCards.Add(_factory.CreateCard(1, enemyPlayerId)); 
            }
            catch { /* Ignore errors */ }

            var enemy = state.GetPlayer(enemyPlayerId);

            var newEnemyState = enemy;
            foreach (var c in phantomCards)
            {
                newEnemyState = newEnemyState.WithCardAddedToHand(c);
            }

            return state.UpdatePlayer(newEnemyState);
        }
    }
}