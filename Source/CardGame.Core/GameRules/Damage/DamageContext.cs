using CardGame.Core.Cards.Models;
using System;

namespace CardGame.Core.GameRules.Damage
{
    /// <summary>
    /// Contains contextual information about a damage event, including source, target, amount, and type.
    /// </summary>
    public class DamageContext
    {
        #region Constructor
        /// <summary>
        /// Initializes a new instance of the DamageContext class.
        /// </summary>
        /// <param name="source">The card instance that is the source of the damage, or null if there is no specific source.</param>
        /// <param name="target">The card instance that is the target of the damage.</param>
        /// <param name="rawAmount">The base amount of damage before any modifications.</param>
        /// <param name="type">The type of damage being dealt.</param>
        public DamageContext(CardInstance? source, CardInstance target, int rawAmount, DamageType type)
        {
            Source = source;
            Target = target;
            RawAmount = rawAmount;
            Type = type;
        }
        #endregion

        #region Properties
        /// <summary>
        /// Gets the card instance that is the source of the damage, or null if there is no specific source.
        /// </summary>
        public CardInstance? Source { get; }

        /// <summary>
        /// Gets the card instance that is the target of the damage.
        /// </summary>
        public CardInstance Target { get; }

        /// <summary>
        /// Gets the base amount of damage before any modifications (armor, keywords, etc.).
        /// </summary>
        public int RawAmount { get; }

        /// <summary>
        /// Gets the type of damage being dealt.
        /// </summary>
        public DamageType Type { get; }
        #endregion
    }
}