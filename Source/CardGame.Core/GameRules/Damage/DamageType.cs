using System;

namespace CardGame.Core.GameRules.Damage
{
    /// <summary>
    /// Represents the different types of damage that can be dealt in the game.
    /// </summary>
    public enum DamageType
    {
        /// <summary>
        /// Damage dealt through combat between units.
        /// </summary>
        Combat,

        /// <summary>
        /// Damage dealt through action cards (spells).
        /// </summary>
        Action,

        /// <summary>
        /// Damage dealt through passive effects or abilities.
        /// </summary>
        Effect
    }
}