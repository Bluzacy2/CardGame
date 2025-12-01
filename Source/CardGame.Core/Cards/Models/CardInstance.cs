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

        /* Zamianka z podpisywania na tylko odczyt: Aby zachować pełny stan Immmutability dla kart - B. */
        public CardStats CurrentStats { get;  }
        public CardInstance(int instanceId, int ownerId, CardDefinition definition)
        {
            InstanceId = instanceId;
            OwnerPlayerId = ownerId;
            Definition = definition;
            CurrentStats = definition.BaseStats;
        }
        // Prywatny konstruktor do tworzenia kopii ze zmienionymi statystykami
        private CardInstance(int instanceId, int ownerId, CardDefinition definition, CardStats currentStats)
        {
            InstanceId = instanceId;
            OwnerPlayerId = ownerId;
            Definition = definition;
            CurrentStats = currentStats;
        }

        /* ---------------- Metody dla IMMUTABILITY -------------------
         * WithStats - zwraca kopię karty z nowymi jej statystykami 
         * TakeDamage - zwraca kopię karty po utrzymaniu obrażeń
         * (mam nadzieje, że bez błędów kompilacji tym razem proszę) - B.*/

        public CardInstance WithStats(CardStats newStats) {
            return new
                CardInstance(InstanceId, OwnerPlayerId, Definition, newStats);
        }

        public CardInstance TakeDamage(int amount) {
            var newStats = new CardStats(
                CurrentStats.Attack,
                CurrentStats.Health - amount,
                CurrentStats.BloodCost);

            return new
                CardInstance(InstanceId, OwnerPlayerId, Definition, newStats);
        }

    }
}
