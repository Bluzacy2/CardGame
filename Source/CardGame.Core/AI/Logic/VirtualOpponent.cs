using System.Collections.Generic;
using CardGame.Core.Cards.Factories;
using CardGame.Core.Cards.Models;
using CardGame.Core.State.Models;

namespace CardGame.Core.AI.Logic
{
    internal class VirtualOpponent
    {
        private readonly CardFactory _factory;

        public VirtualOpponent(CardFactory factory)
        {
            _factory = factory;
        }

        public GameState InjectPhantomHand(GameState state, int enemyPlayerId)
        {
            // Tworzymy kopię stanu, w której przeciwnik ma "groźne" karty
            // To pozwala botowi przewidzieć: "Jeśli zagram X, on może użyć Y"

            var phantomCards = new List<CardInstance>();

            // Zakładamy, że wróg ma "Nuke" (ID 99 z Twojego kodu testowego)
            try
            {
                phantomCards.Add(_factory.CreateCard(99, enemyPlayerId));
                // Zakładamy, że ma standardową jednostkę
                phantomCards.Add(_factory.CreateCard(1, enemyPlayerId));
            }
            catch { /* Ignoruj błędy jeśli ID nie istnieją */ }

            var enemy = state.GetPlayer(enemyPlayerId);

            // Dodajemy te karty do jego ręki w symulacji (zachowując te co ma, jeśli symulator je widzi)
            // W prawdziwej grze nie widzimy ręki, więc w symulacji zastępujemy nieznane karty tymi fantomami.
            // Tutaj prosta implementacja: Dodajemy do istniejącej ręki.
            var newEnemyState = enemy;
            foreach (var c in phantomCards)
            {
                newEnemyState = newEnemyState.WithCardAddedToHand(c);
            }

            return state.UpdatePlayer(newEnemyState);
        }
    }
}