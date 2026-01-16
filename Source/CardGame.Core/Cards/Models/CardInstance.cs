using CardGame.Core.Cards.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CardGame.Core.Cards.Models
{
    /// <summary>
    /// Represents an instance of a card in the game with mutable state such as damage taken and buffs.
    /// Each card instance is immutable and returns new instances when modified.
    /// </summary>
    public class CardInstance
    {
        #region Properties
        /// <summary>
        /// Gets the unique identifier for this card instance.
        /// </summary>
        public int InstanceId { get; }

        /// <summary>
        /// Gets the ID of the player who owns this card.
        /// </summary>
        public int OwnerPlayerId { get; }

        /// <summary>
        /// Gets the static definition of the card (base stats, effects, etc.).
        /// </summary>
        public CardDefinition Definition { get; }

        /// <summary>
        /// Gets the amount of damage this card has taken.
        /// </summary>
        public int DamageTaken { get; }

        /// <summary>
        /// Gets the permanent stat buffs applied to this card.
        /// </summary>
        public CardStats PermanentBuffs { get; }

        /// <summary>
        /// Gets the keywords applied to this card via auras from other units.
        /// </summary>
        public IReadOnlyList<Keyword> AuraKeywords { get; }

        /// <summary>
        /// Gets the keywords that have been temporarily suppressed on this card.
        /// </summary>
        public IReadOnlyList<Keyword> SuppressedKeywords { get; }

        /// <summary>
        /// Gets a value indicating whether this card is silenced (ignores all buffs and keywords).
        /// </summary>
        public bool IsSilenced { get; }

        /// <summary>
        /// Gets the cost reduction applied to this card (immutable once set).
        /// </summary>
        public int CostReduction { get; }

        /// <summary>
        /// Gets the current calculated stats of the card, accounting for buffs, damage, and silenced state.
        /// </summary>
        public CardStats CurrentStats
        {
            get
            {
                if (IsSilenced)
                {
                    return new CardStats(
                        Definition.BaseStats.Attack,
                        Definition.BaseStats.Health - DamageTaken,
                        Definition.BaseStats.BloodCost,
                        new List<Keyword>(),
                        new Dictionary<Keyword, int>(),
                        Definition.BaseStats.CostType
                    );
                }

                var baseAndPermanent = Definition.BaseStats + PermanentBuffs;
                int finalCost = Math.Max(0, baseAndPermanent.BloodCost - CostReduction);
                var combinedKeywords = baseAndPermanent.Keywords.Concat(AuraKeywords)
                    .Where(keyword => !SuppressedKeywords.Contains(keyword))
                    .Distinct()
                    .ToList();

                return new CardStats(
                    baseAndPermanent.Attack,
                    baseAndPermanent.Health - DamageTaken,
                    finalCost,
                    combinedKeywords,
                    new Dictionary<Keyword, int>((Dictionary<Keyword, int>)baseAndPermanent.KeywordParams),
                    baseAndPermanent.CostType
                );
            }
        }

        /// <summary>
        /// Gets the maximum health of the card (base health + permanent buffs, ignoring damage taken).
        /// </summary>
        public int MaxHealth => IsSilenced ? Definition.BaseStats.Health : (Definition.BaseStats + PermanentBuffs).Health;
        #endregion

        #region Constructors
        /// <summary>
        /// Initializes a new instance of the CardInstance class with default values.
        /// </summary>
        /// <param name="instanceId">The unique identifier for this card instance.</param>
        /// <param name="ownerPlayerId">The ID of the player who owns this card.</param>
        /// <param name="definition">The static definition of the card.</param>
        public CardInstance(int instanceId, int ownerPlayerId, CardDefinition definition)
            : this(
                instanceId,
                ownerPlayerId,
                definition,
                damageTaken: 0,
                permanentBuffs: new CardStats(0, 0, 0),
                auraKeywords: new List<Keyword>(),
                suppressedKeywords: new List<Keyword>(),
                isSilenced: false,
                costReduction: 0)
        { }

        /// <summary>
        /// Private constructor for creating modified instances with all state parameters.
        /// </summary>
        private CardInstance(
            int instanceId,
            int ownerPlayerId,
            CardDefinition definition,
            int damageTaken,
            CardStats permanentBuffs,
            IEnumerable<Keyword> auraKeywords,
            IEnumerable<Keyword> suppressedKeywords,
            bool isSilenced,
            int costReduction)
        {
            InstanceId = instanceId;
            OwnerPlayerId = ownerPlayerId;
            Definition = definition;
            DamageTaken = damageTaken;
            PermanentBuffs = permanentBuffs;
            AuraKeywords = auraKeywords?.ToList() ?? new List<Keyword>();
            SuppressedKeywords = suppressedKeywords?.ToList() ?? new List<Keyword>();
            IsSilenced = isSilenced;
            CostReduction = costReduction;
        }
        #endregion

        #region State Modification Methods
        /// <summary>
        /// Creates a new card instance with all state reset to default (except InstanceId, Owner, and Definition).
        /// Typically used when moving a card between zones.
        /// </summary>
        /// <returns>A new CardInstance with reset state.</returns>
        public CardInstance MoveAndReset() =>
            new CardInstance(
                InstanceId,
                OwnerPlayerId,
                Definition,
                damageTaken: 0,
                permanentBuffs: new CardStats(0, 0, 0),
                auraKeywords: new List<Keyword>(),
                suppressedKeywords: new List<Keyword>(),
                isSilenced: IsSilenced,
                costReduction: 0
            );

        /// <summary>
        /// Creates a new card instance with the specified total damage.
        /// </summary>
        /// <param name="totalDamage">The total damage the card has taken.</param>
        /// <returns>A new CardInstance with the specified damage.</returns>
        public CardInstance WithDamage(int totalDamage) =>
            new CardInstance(
                InstanceId,
                OwnerPlayerId,
                Definition,
                totalDamage,
                PermanentBuffs,
                AuraKeywords,
                SuppressedKeywords,
                IsSilenced,
                CostReduction
            );

        /// <summary>
        /// Applies damage to the card and returns a new instance.
        /// </summary>
        /// <param name="amount">The amount of damage to apply.</param>
        /// <returns>A new CardInstance with increased damage taken.</returns>
        public CardInstance TakeDamage(int amount) => WithDamage(DamageTaken + amount);

        /// <summary>
        /// Heals the card and returns a new instance.
        /// </summary>
        /// <param name="amount">The amount of health to restore.</param>
        /// <returns>A new CardInstance with reduced damage taken.</returns>
        public CardInstance Heal(int amount) => WithDamage(Math.Max(0, DamageTaken - amount));

        /// <summary>
        /// Adds a permanent stat buff to the card and returns a new instance.
        /// Silenced cards cannot receive buffs.
        /// </summary>
        /// <param name="buff">The stat buff to apply.</param>
        /// <returns>A new CardInstance with the buff applied, or the same instance if silenced.</returns>
        public CardInstance AddPermanentBuff(CardStats buff) =>
            IsSilenced ? this : new CardInstance(
                InstanceId,
                OwnerPlayerId,
                Definition,
                DamageTaken,
                PermanentBuffs + buff,
                AuraKeywords,
                SuppressedKeywords,
                IsSilenced,
                CostReduction
            );

        /// <summary>
        /// Updates the aura keywords on the card and returns a new instance.
        /// </summary>
        /// <param name="newAuras">The new aura keywords to apply.</param>
        /// <returns>A new CardInstance with the updated aura keywords.</returns>
        public CardInstance WithAuras(IEnumerable<Keyword> newAuras) =>
            new CardInstance(
                InstanceId,
                OwnerPlayerId,
                Definition,
                DamageTaken,
                PermanentBuffs,
                newAuras,
                SuppressedKeywords,
                IsSilenced,
                CostReduction
            );

        /// <summary>
        /// Updates the cost reduction on the card and returns a new instance.
        /// </summary>
        /// <param name="costReduction">The new cost reduction value.</param>
        /// <returns>A new CardInstance with the updated cost reduction.</returns>
        public CardInstance WithCostReduction(int costReduction) =>
            new CardInstance(
                InstanceId,
                OwnerPlayerId,
                Definition,
                DamageTaken,
                PermanentBuffs,
                AuraKeywords,
                SuppressedKeywords,
                IsSilenced,
                costReduction
            );

        /// <summary>
        /// Temporarily suppresses a keyword on the card and returns a new instance.
        /// </summary>
        /// <param name="keyword">The keyword to suppress.</param>
        /// <returns>A new CardInstance with the keyword added to suppressed keywords.</returns>
        public CardInstance SuppressKeyword(Keyword keyword)
        {
            var newList = new List<Keyword>(SuppressedKeywords);
            if (!newList.Contains(keyword)) newList.Add(keyword);
            return new CardInstance(
                InstanceId,
                OwnerPlayerId,
                Definition,
                DamageTaken,
                PermanentBuffs,
                AuraKeywords,
                newList,
                IsSilenced,
                CostReduction
            );
        }

        /// <summary>
        /// Silences the card, removing all buffs, keywords, and auras, and returns a new instance.
        /// </summary>
        /// <returns>A new silenced CardInstance with all state reset except damage taken.</returns>
        public CardInstance Silence() =>
            new CardInstance(
                InstanceId,
                OwnerPlayerId,
                Definition,
                DamageTaken,
                permanentBuffs: new CardStats(0, 0, 0),
                auraKeywords: new List<Keyword>(),
                suppressedKeywords: new List<Keyword>(),
                isSilenced: true,
                costReduction: 0
            );
        #endregion
    }
}