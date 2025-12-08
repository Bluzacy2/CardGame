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

        public CardStats CurrentStats
        {
            get
            {
            
                var stats = Definition.BaseStats + PermanentBuffs;

                int currentHp = stats.Health - DamageTaken;

                var combinedKeywords = new List<Keyword>(stats.Keywords);
                if (AuraKeywords != null) combinedKeywords.AddRange(AuraKeywords);

                return new CardStats(
                    stats.Attack,
                    currentHp, 
                    stats.BloodCost,
                    combinedKeywords.Distinct()
                );
            }
        }

        public int MaxHealth => (Definition.BaseStats + PermanentBuffs).Health;


        public CardInstance(int instanceId, int playerOwnerId, CardDefinition definition)
            : this(instanceId, playerOwnerId, definition, 0, new CardStats(0, 0, 0), new List<Keyword>())
        {
        }

        private CardInstance(
           int instanceId,
           int playerOwnerId,
           CardDefinition definition,
           int damageTaken,
           CardStats permanentBuffs,
           IEnumerable<Keyword> auraKeywords)
        {
            InstanceId = instanceId;
            OwnerPlayerId = playerOwnerId;
            Definition = definition;
            DamageTaken = damageTaken;
            PermanentBuffs = permanentBuffs;
            AuraKeywords = auraKeywords != null ? new List<Keyword>(auraKeywords) : new List<Keyword>();
        }
        public CardInstance WithDamage(int totalDamage)
        {
            return new CardInstance(InstanceId, OwnerPlayerId, Definition, totalDamage, PermanentBuffs, AuraKeywords);
        }

        public CardInstance TakeDamage(int amount)
        {
            return WithDamage(DamageTaken + amount);
        }

        public CardInstance Heal(int amount)
        {
            int newDamage = System.Math.Max(0, DamageTaken - amount);
            return WithDamage(newDamage);
        }

        public CardInstance AddPermanentBuff(CardStats buff)
        {
            return new CardInstance(InstanceId, OwnerPlayerId, Definition, DamageTaken, PermanentBuffs + buff, AuraKeywords);
        }

        public CardInstance WithAuras(IEnumerable<Keyword> newAuras)
        {
            return new CardInstance(InstanceId, OwnerPlayerId, Definition, DamageTaken, PermanentBuffs, newAuras);
        }

        public CardInstance Silence()
        {
            return new CardInstance(InstanceId, OwnerPlayerId, Definition, DamageTaken, new CardStats(0, 0, 0), new List<Keyword>());
        }
    }
}