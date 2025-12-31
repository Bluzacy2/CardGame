using CardGame.Core.Cards.Data;
using System.Collections.Generic;
using System.Linq;

namespace CardGame.Core.Cards.Models
{
    public class CardInstance
    {
        public int InstanceId { get; }
        public int OwnerPlayerId { get; }
        public CardDefinition Definition { get; }
        public int DamageTaken { get; }
        public CardStats PermanentBuffs { get; }
        public IReadOnlyList<Keyword> AuraKeywords { get; }
        public IReadOnlyList<Keyword> SuppressedKeywords { get; }
        public bool IsSilenced { get; }
        public int CostReduction { get; set; } = 0; //

        public CardStats CurrentStats
        {
            get
            {
                if (IsSilenced)
                {
                    return new CardStats(Definition.BaseStats.Attack, Definition.BaseStats.Health - DamageTaken,
                        Definition.BaseStats.BloodCost, new List<Keyword>(), new Dictionary<Keyword, int>(), Definition.BaseStats.CostType);
                }
                var baseAndPerm = Definition.BaseStats + PermanentBuffs;
                int finalCost = Math.Max(0, baseAndPerm.BloodCost - CostReduction); // 
                var combinedKeywords = baseAndPerm.Keywords.Concat(AuraKeywords)
                    .Where(k => !SuppressedKeywords.Contains(k)).Distinct().ToList();
                return new CardStats(baseAndPerm.Attack, baseAndPerm.Health - DamageTaken, finalCost, // baseAndPerm.BloodCost
                    combinedKeywords, new Dictionary<Keyword, int>((Dictionary<Keyword, int>)baseAndPerm.KeywordParams), baseAndPerm.CostType);
            }
        }

        public int MaxHealth => IsSilenced ? Definition.BaseStats.Health : (Definition.BaseStats + PermanentBuffs).Health;

        public CardInstance(int instanceId, int playerOwnerId, CardDefinition definition)
            : this(instanceId, playerOwnerId, definition, 0, new CardStats(0, 0, 0), new List<Keyword>(), new List<Keyword>(), false) { }

        private CardInstance(int instanceId, int playerOwnerId, CardDefinition definition, int damageTaken,
            CardStats permanentBuffs, IEnumerable<Keyword> auraKeywords, IEnumerable<Keyword> suppressedKeywords, bool isSilenced)
        {
            InstanceId = instanceId; OwnerPlayerId = playerOwnerId; Definition = definition; DamageTaken = damageTaken;
            PermanentBuffs = permanentBuffs; AuraKeywords = auraKeywords?.ToList() ?? new List<Keyword>();
            SuppressedKeywords = suppressedKeywords?.ToList() ?? new List<Keyword>(); IsSilenced = isSilenced;
        }

        public CardInstance MoveAndReset() =>
            new CardInstance(InstanceId, OwnerPlayerId, Definition, 0, new CardStats(0, 0, 0), new List<Keyword>(), new List<Keyword>(), IsSilenced) { CostReduction = 0 };

        public CardInstance WithDamage(int totalDamage) =>
            new CardInstance(InstanceId, OwnerPlayerId, Definition, totalDamage, PermanentBuffs, AuraKeywords, SuppressedKeywords, IsSilenced);

        public CardInstance TakeDamage(int amount) => WithDamage(DamageTaken + amount);
        public CardInstance Heal(int amount) => WithDamage(System.Math.Max(0, DamageTaken - amount));

        public CardInstance AddPermanentBuff(CardStats buff) =>
            IsSilenced ? this : new CardInstance(InstanceId, OwnerPlayerId, Definition, DamageTaken, PermanentBuffs + buff, AuraKeywords, SuppressedKeywords, IsSilenced);

        public CardInstance WithAuras(IEnumerable<Keyword> newAuras) =>
            new CardInstance(InstanceId, OwnerPlayerId, Definition, DamageTaken, PermanentBuffs, newAuras, SuppressedKeywords, IsSilenced);

        public CardInstance SuppressKeyword(Keyword keyword)
        {
            var newList = new List<Keyword>(SuppressedKeywords);
            if (!newList.Contains(keyword)) newList.Add(keyword);
            return new CardInstance(InstanceId, OwnerPlayerId, Definition, DamageTaken, PermanentBuffs, AuraKeywords, newList, IsSilenced);
        }

        public CardInstance Silence() =>
            new CardInstance(InstanceId, OwnerPlayerId, Definition, DamageTaken, new CardStats(0, 0, 0), new List<Keyword>(), new List<Keyword>(), true);
    }
}