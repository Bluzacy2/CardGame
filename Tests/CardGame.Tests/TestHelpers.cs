using CardGame.Core.Application;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Factories;
using CardGame.Core.Cards.Models;
using CardGame.Core.State.Enums;
using CardGame.Core.State.Models;
using System.Collections.Generic;
using System.IO;

namespace CardGame.Tests
{
    public static class TestHelpers
    {
        // Tworzy silnik z załadowanymi kartami z JSON stringa
        public static GameEngine CreateEngineWithCards(string jsonContent, int seed = 1)
        {
            // 1. Zapisz JSON do pliku tymczasowego (CardLibrary wymaga pliku)
            string tempFile = Path.GetTempFileName();
            File.WriteAllText(tempFile, jsonContent);

            try
            {
                CardLibrary.Instance.LoadFromJson(tempFile);
            }
            finally
            {
                // File.Delete(tempFile); // Można usunąć, ale Windows czasem blokuje plik
            }

            // 2. Setup
            var rng = new DeterministicRng(seed);
            var factory = new CardFactory(CardLibrary.Instance, rng);

            // 3. Pusty stan początkowy
            var pA = PlayerState.Initial(1, new List<CardInstance>())
                .With(maxBlood: 10, currentBlood: 10, health: 30);
            var pB = PlayerState.Initial(2, new List<CardInstance>())
                .With(maxBlood: 10, currentBlood: 10, health: 30);

            var board = BoardState.Empty();
            var state = new GameState(1, GamePhase.UnitOnly, 1, board, pA, pB);

            return new GameEngine(state, seed);
        }

        public static CardInstance CreateCard(GameEngine engine, int id, int ownerId)
        {
            return engine.Factory.CreateCard(id, ownerId);
        }
    }
}