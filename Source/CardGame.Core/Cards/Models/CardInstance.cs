using CardGame.Core.Cards.Data; // Dla Keyword
using System.Collections.Generic;

namespace CardGame.Core.Cards.Models
{
    public class CardInstance
    {
        public int InstanceId { get; }
        public int OwnerPlayerId { get; }
        public CardDefinition Definition { get; }

        // Statystyki muszą być read-only properties
        public CardStats CurrentStats { get; }

        // Konstruktor publiczny (tworzy nową kartę z bazy)
        public CardInstance(int instanceId, int OwnerPlayerId, CardDefinition definition)
            : this(instanceId, OwnerPlayerId, definition, definition.BaseStats)
        {
        }

        // --- TU BYŁ BŁĄD ---
        // Konstruktor prywatny (tworzy kopię ze zmodyfikowanymi statystykami)
        private CardInstance(int instanceId, int ownerPlayerId, CardDefinition definition, CardStats currentStats)
        {
            InstanceId = instanceId;
            OwnerPlayerId = ownerPlayerId;
            Definition = definition;

            // WAŻNE: Musimy przypisać currentStats (te przekazane), a NIE definition.BaseStats!
            // Jeśli tu miałeś definition.BaseStats, to kasowałeś wszystkie zmiany (obrażenia, keywordy).
            CurrentStats = currentStats;
        }

        // --- Metody Immutable ---

        public CardInstance WithStats(CardStats newStats)
        {
            return new CardInstance(InstanceId, OwnerPlayerId, Definition, newStats);
        }

        // Dodawanie Keyworda (np. Marked)
        public CardInstance WithKeywordAdded(Keyword keyword)
        {
            var newStats = CurrentStats.WithKeyword(keyword);
            return WithStats(newStats);
        }

        // Otrzymywanie obrażeń
        public CardInstance TakeDamage(int amount)
        {
            var newStats = new CardStats(
                CurrentStats.Attack,
                CurrentStats.Health - amount,
                CurrentStats.BloodCost,
                CurrentStats.Keywords // Pamiętaj, żeby przenieść też keywordy przy zmianie HP!
            );
            return new CardInstance(InstanceId, OwnerPlayerId, Definition, newStats);
        }
    }
}