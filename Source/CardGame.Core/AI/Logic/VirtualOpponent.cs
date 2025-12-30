using System.Collections.Generic;
using System.Linq;
using CardGame.Core.Cards.Factories;
using CardGame.Core.Cards.Models;
using CardGame.Core.State.Models;

namespace CardGame.Core.AI.Logic
{
    public class VirtualOpponent
    {
        private readonly CardFactory _factory;

        public VirtualOpponent(CardFactory factory)
        {
            _factory = factory;
        }

        /// <summary>
        /// Tworzy kopiê stanu gry, w której przeciwnik ma w rêce najbardziej prawdopodobne 
        /// i niebezpieczne karty ze swojej talii. Pozwala to AI przewidzieæ "Punish".
        /// </summary>
        public GameState InjectRealisticPhantomHand(GameState state, int enemyPlayerId)
        {
            var enemy = state.GetPlayer(enemyPlayerId);
            var phantomCards = new List<CardInstance>();

            // 1. DECK TRACKING: Analizujemy, co zosta³o w talii przeciwnika.
            // Bot "pamiêta", jakie karty jeszcze nie zosta³y zagrane.
            var availableInDeck = enemy.DrawPile
                .GroupBy(c => c.Definition.Id)
                .Select(g => g.First())
                .OrderByDescending(c => EvaluateDangerLevel(c))
                .Take(2); // Wstrzykujemy 2 najgroŸniejsze potencjalne odpowiedzi

            foreach (var cardDef in availableInDeck)
            {
                try
                {
                    // Tworzymy wirtualn¹ instancjê karty na potrzeby symulacji
                    int id = int.Parse(cardDef.Definition.Id);
                    phantomCards.Add(_factory.CreateCard(id, enemyPlayerId));
                }
                catch { /* Ignoruj b³êdy parsowania ID */ }
            }

            // 2. AKTUALIZACJA STANU SYMULACJI
            var newEnemyState = enemy;
            foreach (var c in phantomCards)
            {
                // Dodajemy karty do rêki przeciwnika w œwiecie wirtualnym
                newEnemyState = newEnemyState.WithCardAddedToHand(c);
            }

            return state.UpdatePlayer(newEnemyState);
        }

        /// <summary>
        /// Ocenia, jak bardzo bot powinien baæ siê konkretnej karty.
        /// </summary>
        private int EvaluateDangerLevel(CardInstance c)
        {
            string id = c.Definition.Id;

            // Priorytet 1: Czary niszcz¹ce/reaktywne (Snajperzy)
            if (id == "7") return 10;  // Glock-17 (Bezpoœrednie obra¿enia)
            if (id == "31") return 9;  // Silence (Niszczy synergie/Unkillable)
            if (id == "16") return 8;  // HellFire (AoE - czyœci stó³)
            if (id == "18") return 7;  // Final Mission (Sacrifice removal)

            // Priorytet 2: Silne jednostki
            if (c.CurrentStats.Attack >= 5) return 6;
            if (c.CurrentStats.Keywords.Contains(CardGame.Core.Cards.Data.Keyword.SplashDamage)) return 5;

            return 1; // Reszta kart jest niskim priorytetem
        }
    }
}   