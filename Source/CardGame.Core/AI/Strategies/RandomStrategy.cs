using System;
using CardGame.Core.AI.Interfaces;
using CardGame.Core.State.Models;

namespace CardGame.Core.AI.Strategies
{
    public class RandomStrategy : IAIStrategy
    {
        private readonly Random _rand = new Random();

        public float Evaluate(GameState state, int botPlayerId)
        {
            // Zwraca losową wartość - bot będzie nieprzewidywalny i chaotyczny
            return (float)_rand.NextDouble() * 100f;
        }
    }
}