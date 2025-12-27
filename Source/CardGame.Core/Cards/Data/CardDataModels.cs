using CardGame.Core.Cards.Data;
using System;
using System.Collections.Generic;
using System.Security.AccessControl;

namespace CardGame.Core.Cards.Data
{
    public class CardData
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public CardType Type { get; set; }
        public List<string> Subtypes { get; set; } = new();
        public int Cost { get; set; }
        public ResourceType CostType { get; set; } = ResourceType.Blood; 
        public int Attack { get; set; }
        public int Health { get; set; }
        public List<Keyword> Keywords { get; set; } = new();
        public Dictionary<Keyword, int> KeywordParams { get; set; } = new();
        public string Description { get; set; } = string.Empty;

        public List<EffectData> Effects { get; set; } = new();
    }

    public class EffectData
    {
        public TriggerType Trigger { get; set; }
        public ConditionData? Condition { get; set; }
        public List<ActionData> Actions { get; set; } = new();
        public TargetType Targeting { get; set; }
        public List<string> ChoiceLabels { get; set; } = new();
        public EffectZone Zone { get; set; } = EffectZone.Board;
    }

    public class ActionData
    {
        public ActionType Type { get; set; }
        public TargetType Target { get; set; }
        public int Amount { get; set; }
        public int BuffAtk { get; set; }
        public int BuffHp { get; set; }

        public int ValueParam { get; set; }

     
        public Keyword? StatusKeyword { get; set; }
        public ResourceType? Resource { get; set; }

     
        public string? StringParam { get; set; }
    }

    public class ConditionData
    {
        public ConditionType Condition { get; set; }
        public List<ConditionData> SubConditions { get; set; } = new();
        public string? TargetParam { get; set; }
        public int ValueParam { get; set; }
    }
}