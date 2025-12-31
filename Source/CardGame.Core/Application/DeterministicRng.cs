using System;

namespace CardGame.Core.Application
{
    public class DeterministicRng
    {
        private readonly Random _random;
        public int Seed { get; }

        public DeterministicRng(int seed)
        {
            Seed = seed;
            _random = new Random(seed);
        }
        public int Next(int minValue, int maxValue)
        {
            return _random.Next(minValue, maxValue);
        }
        public int NextMAX(int maxValue)
        {
            return _random.Next(maxValue);
        }


        public int NextId()
        {
            return _random.Next(100000, 999999);
        }
    }
}
