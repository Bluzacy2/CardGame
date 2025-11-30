using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CardGame.Core.Cards.Models
{
    public class CardInstance
    {
        public int InstanceId { get; }
        public int OwnerPlayerId { get; }
        public CardDefinition Definition { get; }

        public CardStats CurrentStats { get; private set; }
        public CardInstance(int instanceId, int ownerPlayerId, CardDefinition definition)
        {
            InstanceId = instanceId;
            OwnerPlayerId = ownerPlayerId;
            Definition = definition;
            CurrentStats = definition.BaseStats;
        }

        public void TakeDamge(int amount)
        {
           var newStats = new CardStats(
                CurrentStats.Attack,
                CurrentStats.Health - amount,
                CurrentStats.BloodCost
            );
            CurrentStats = newStats;
        }
    }
}
