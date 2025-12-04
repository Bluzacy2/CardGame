using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CardGame.Core.Cards.Data
{
    public class CardData
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public CardType Type { get; set; }
        public List<string> Subtypes { get; set; } = new();
        public int Cost { get; set; }
        public int Attack { get; set; }
        public int Health { get; set; }
        public List<Keyword> Keywords { get; set; } = new();
        public string Description { get; set; } = string.Empty;

        public List<EffectData> Effects { get; set; } = new();
    }

    public class EffectData
    {
        public TriggerType Trigger { get; set; }
        public ConditionData? Condition { get; set; }
        public List<ActionData> Actions { get; set; } = new();
        public TargetType Targeting { get; set; }
    }

    public class ActionData
    {
        public ActionType Type { get; set; }
        public TargetType Target { get; set; }
        public int Amount { get; set; }
        public int BuffAtk { get; set; }
        public int BuffHp { get; set; }

        public int ValueParam { get; set; }
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

