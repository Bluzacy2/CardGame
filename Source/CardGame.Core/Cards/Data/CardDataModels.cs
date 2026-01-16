using CardGame.Core.Cards.Data;
using System.Collections.Generic;

namespace CardGame.Core.Cards.Data
{
    #region Card Data Models

    /// <summary>
    /// Represents the complete data definition for a card.
    /// </summary>
    public class CardData
    {
        /// <summary>
        /// Gets or sets the unique identifier of the card.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the display name of the card.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the type of card (Unit or Spell).
        /// </summary>
        public CardType Type { get; set; }

        /// <summary>
        /// Gets or sets the list of subtype classifications.
        /// </summary>
        public List<string> Subtypes { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the resource cost to play the card.
        /// </summary>
        public int Cost { get; set; }

        /// <summary>
        /// Gets or sets the type of resource required.
        /// </summary>
        public ResourceType CostType { get; set; } = ResourceType.Blood;

        /// <summary>
        /// Gets or sets the attack value for unit cards.
        /// </summary>
        public int Attack { get; set; }

        /// <summary>
        /// Gets or sets the health value for unit cards.
        /// </summary>
        public int Health { get; set; }

        /// <summary>
        /// Gets or sets the list of keywords/abilities on the card.
        /// </summary>
        public List<Keyword> Keywords { get; set; } = new List<Keyword>();

        /// <summary>
        /// Gets or sets the parameters for specific keywords.
        /// </summary>
        public Dictionary<Keyword, int> KeywordParams { get; set; } = new Dictionary<Keyword, int>();

        /// <summary>
        /// Gets or sets the descriptive text displayed to players.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the list of effects that the card can trigger.
        /// </summary>
        public List<EffectData> Effects { get; set; } = new List<EffectData>();
    }

    /// <summary>
    /// Represents a triggered effect with conditions and actions.
    /// </summary>
    public class EffectData
    {
        /// <summary>
        /// Gets or sets the event that triggers this effect.
        /// </summary>
        public TriggerType Trigger { get; set; }

        /// <summary>
        /// Gets or sets the condition that must be met for the effect to activate.
        /// </summary>
        public ConditionData? Condition { get; set; }

        /// <summary>
        /// Gets or sets the actions performed when the effect triggers.
        /// </summary>
        public List<ActionData> Actions { get; set; } = new List<ActionData>();

        /// <summary>
        /// Gets or sets the targeting rules for the effect.
        /// </summary>
        public TargetType Targeting { get; set; }

        /// <summary>
        /// Gets or sets the labels for player choices in multi-choice effects.
        /// </summary>
        public List<string> ChoiceLabels { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the zone where the effect can be activated from.
        /// </summary>
        public EffectZone Zone { get; set; } = EffectZone.Board;
    }

    /// <summary>
    /// Represents a single game action within an effect.
    /// </summary>
    public class ActionData
    {
        /// <summary>
        /// Gets or sets the type of action to perform.
        /// </summary>
        public ActionType Type { get; set; }

        /// <summary>
        /// Gets or sets the target selection for this action.
        /// </summary>
        public TargetType Target { get; set; }

        /// <summary>
        /// Gets or sets the primary numerical value for the action.
        /// </summary>
        public int Amount { get; set; }

        /// <summary>
        /// Gets or sets the attack buff value for stat modification actions.
        /// </summary>
        public int BuffAtk { get; set; }

        /// <summary>
        /// Gets or sets the health buff value for stat modification actions.
        /// </summary>
        public int BuffHp { get; set; }

        /// <summary>
        /// Gets or sets an additional numerical parameter.
        /// </summary>
        public int ValueParam { get; set; }

        /// <summary>
        /// Gets or sets the keyword to apply for status effects.
        /// </summary>
        public Keyword? StatusKeyword { get; set; }

        /// <summary>
        /// Gets or sets the resource type for resource-related actions.
        /// </summary>
        public ResourceType? Resource { get; set; }

        /// <summary>
        /// Gets or sets a string parameter for card names, descriptions, or other text data.
        /// </summary>
        public string? StringParam { get; set; }
    }

    /// <summary>
    /// Represents a logical condition for effect activation.
    /// </summary>
    public class ConditionData
    {
        /// <summary>
        /// Gets or sets the type of logical condition.
        /// </summary>
        public ConditionType Condition { get; set; }

        /// <summary>
        /// Gets or sets nested sub-conditions for complex logic.
        /// </summary>
        public List<ConditionData> SubConditions { get; set; } = new List<ConditionData>();

        /// <summary>
        /// Gets or sets a string parameter for target specification.
        /// </summary>
        public string? TargetParam { get; set; }

        /// <summary>
        /// Gets or sets a numerical parameter for condition evaluation.
        /// </summary>
        public int ValueParam { get; set; }
    }

    #endregion
}