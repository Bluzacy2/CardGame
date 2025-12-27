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
        public static GameEngine CreateEngineWithCards(string jsonContent, int seed = 1)
        {
            string tempFile = Path.GetTempFileName();
            File.WriteAllText(tempFile, jsonContent);

            try
            {
                CardLibrary.Instance.Clear();
                CardLibrary.Instance.LoadFromJson(tempFile);
            }
            finally { }

            var rng = new DeterministicRng(seed);
            var factory = new CardFactory(CardLibrary.Instance, rng);

            // Ustawienie początkowe zasobów (Krew)
            var pA = PlayerState.Initial(1, new List<CardInstance>())
                .WithResourceChanged(ResourceType.Blood, 10, 10)
                .With(health: 30);

            var pB = PlayerState.Initial(2, new List<CardInstance>())
                .WithResourceChanged(ResourceType.Blood, 10, 10)
                .With(health: 30);

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