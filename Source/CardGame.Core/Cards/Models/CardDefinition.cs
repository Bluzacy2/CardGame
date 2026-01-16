using CardGame.Core.Cards.Data;
using System.Collections.Generic;

namespace CardGame.Core.Cards.Models
{
    /// <summary>
    /// Represents the static definition of a card, containing all immutable data required to create card instances.
    /// </summary>
    public class CardDefinition
    {
        #region Constructor
        /// <summary>
        /// Initializes a new instance of the CardDefinition class.
        /// </summary>
        /// <param name="id">The unique identifier of the card.</param>
        /// <param name="name">The display name of the card.</param>
        /// <param name="description">The flavor text or ability description of the card.</param>
        /// <param name="type">The type of card (Unit, Spell, etc.).</param>
        /// <param name="subtypes">The subtype classifications of the card.</param>
        /// <param name="baseStats">The base statistics (attack, health, cost) of the card.</param>
        /// <param name="keywords">The keywords associated with the card.</param>
        /// <param name="effects">The effects and abilities of the card.</param>
        public CardDefinition(
            string id,
            string name,
            string description,
            CardType type,
            IEnumerable<string> subtypes,
            CardStats baseStats,
            IEnumerable<Keyword> keywords,
            IEnumerable<EffectData> effects)
        {
            Id = id;
            Name = name;
            Description = description;
            Type = type;
            Subtypes = new List<string>(subtypes);
            BaseStats = baseStats;
            Keywords = new List<Keyword>(keywords);
            Effects = new List<EffectData>(effects);
        }
        #endregion

        #region Properties
        /// <summary>
        /// Gets the unique identifier of the card.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Gets the display name of the card.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the flavor text or ability description of the card.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Gets the type of card (Unit, Spell, etc.).
        /// </summary>
        public CardType Type { get; }

        /// <summary>
        /// Gets the subtype classifications of the card.
        /// </summary>
        public IReadOnlyList<string> Subtypes { get; }

        /// <summary>
        /// Gets the base statistics (attack, health, cost) of the card.
        /// </summary>
        public CardStats BaseStats { get; }

        /// <summary>
        /// Gets the keywords associated with the card.
        /// </summary>
        public IReadOnlyList<Keyword> Keywords { get; }

        /// <summary>
        /// Gets the effects and abilities of the card.
        /// </summary>
        public IReadOnlyList<EffectData> Effects { get; }
        #endregion
    }
}