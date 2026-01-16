using CardGame.Core.Application;
using System;

namespace CardGame.Core.GameRules.Damage
{
    /// <summary>
    /// Calculates final damage values by applying keyword and effect modifiers to raw damage amounts.
    /// </summary>
    public class DamageCalculator
    {
        #region Private Fields
        private readonly GameContext _context;
        #endregion

        #region Constructor
        /// <summary>
        /// Initializes a new instance of the DamageCalculator class.
        /// </summary>
        /// <param name="context">The game context providing access to keyword processors and other services.</param>
        public DamageCalculator(GameContext context)
        {
            _context = context;
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Calculates the final damage amount after applying all modifiers from keywords and effects.
        /// </summary>
        /// <param name="context">The damage context containing source, target, raw amount, and damage type.</param>
        /// <returns>The final damage amount after processing, guaranteed to be non-negative.</returns>
        public int CalculateFinalDamage(DamageContext context)
        {
            int finalDamage = context.RawAmount;

            finalDamage = _context.Keywords.ProcessDamageTaken(finalDamage, context);

            return Math.Max(0, finalDamage);
        }
        #endregion
    }
}