using System;

namespace CardGame.Core.Application
{
    /// <summary>
    /// Provides deterministic random number generation for game simulation.
    /// </summary>
    public class DeterministicRng
    {
        private readonly Random _random;

        /// <summary>
        /// Gets the seed value used to initialize the random number generator.
        /// </summary>
        public int Seed { get; }

        /// <summary>
        /// Initializes a new instance of the DeterministicRng class with a specific seed.
        /// </summary>
        /// <param name="seed">The seed value for reproducible random sequences.</param>
        public DeterministicRng(int seed)
        {
            Seed = seed;
            _random = new Random(seed);
        }

        #region Random Number Generation

        /// <summary>
        /// Returns a random integer within the specified range.
        /// </summary>
        /// <param name="minValue">The inclusive lower bound of the random number returned.</param>
        /// <param name="maxValue">The exclusive upper bound of the random number returned.</param>
        /// <returns>A random integer between minValue (inclusive) and maxValue (exclusive).</returns>
        public int Next(int minValue, int maxValue) =>
            _random.Next(minValue, maxValue);

        /// <summary>
        /// Returns a non-negative random integer less than the specified maximum.
        /// </summary>
        /// <param name="maxValue">The exclusive upper bound of the random number to be generated.</param>
        /// <returns>A random integer between 0 (inclusive) and maxValue (exclusive).</returns>
        public int NextMax(int maxValue) =>
            _random.Next(maxValue);

        /// <summary>
        /// Generates a unique identifier within a specific range.
        /// </summary>
        /// <returns>A random integer between 100,000 and 999,999 inclusive.</returns>
        public int NextId() =>
            _random.Next(100000, 1000000);

        #endregion
    }
}