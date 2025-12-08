using CardGame.Core.Cards.Data;
using System.Collections.Generic;

namespace CardGame.Core.Cards.Models
{
    public class CardInstance
    {
        public int InstanceId { get; }
        public int OwnerPlayerId { get; }
        public CardDefinition Definition { get; }

        public CardStats PermanentStats { get; }
        public IReadOnlyList<Keyword> AuraKeywords { get; }
        public CardStats CurrentStats
        {
            get
            {
                if (AuraKeywords.Count == 0) return PermanentStats;

               
                var mergedKeywords = new List<Keyword>(PermanentStats.Keywords);
                mergedKeywords.AddRange(AuraKeywords);

              
                return new CardStats(
                    PermanentStats.Attack,
                    PermanentStats.Health,
                    PermanentStats.BloodCost,
                    mergedKeywords.Distinct() 
                );
            }
        }

        public CardInstance(int instanceId, int playerOwnerId, CardDefinition definition)
            : this(instanceId, playerOwnerId, definition, definition.BaseStats, new List<Keyword>())
        {
        }


        private CardInstance(
           int instanceId,
           int playerOwnerId,
           CardDefinition definition,
           CardStats permanentStats,
           IEnumerable<Keyword> auraKeywords)
        {
            InstanceId = instanceId;
            OwnerPlayerId = playerOwnerId;
            Definition = definition;
            PermanentStats = permanentStats;
            AuraKeywords = auraKeywords != null ? new List<Keyword>(auraKeywords) : new List<Keyword>();
        }


        public CardInstance WithStats(CardStats newStats)
        {
            
            return new CardInstance(InstanceId, OwnerPlayerId, Definition, newStats, AuraKeywords);
        }
        public CardInstance WithKeywordAdded(Keyword keyword)
        {
           
            var newStats = PermanentStats.WithKeyword(keyword);
            return new CardInstance(InstanceId, OwnerPlayerId, Definition, newStats, AuraKeywords);
        }
        public CardInstance TakeDamage(int amount)
        {
            var newStats = new CardStats(
                PermanentStats.Attack,
                PermanentStats.Health - amount,
                PermanentStats.BloodCost,
                PermanentStats.Keywords
            );
            return new CardInstance(InstanceId, OwnerPlayerId, Definition, newStats, AuraKeywords);
        }

        public CardInstance WithAuras(IEnumerable<Keyword> newAuraKeywords)
        {
            return new CardInstance(InstanceId, OwnerPlayerId, Definition, PermanentStats, newAuraKeywords);
        }

    }
}