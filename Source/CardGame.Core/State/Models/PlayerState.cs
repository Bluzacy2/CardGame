using CardGame.Core.Cards.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CardGame.Core.State.Models
{
    public class PlayerState
    {
        public int PlayerId { get; }
        public int Health { get; }
        public int MaxBlood { get; }
        public int CurrentBlood { get; }

        // 1. Karty w ręce 2. Karty w talii 3. Karty zużyte (odrzucone) || Expectancy value!!
        public IReadOnlyList<CardInstance> Hand { get; }
        public IReadOnlyList<CardInstance> DrawPile { get; }
        public IReadOnlyList<CardInstance> DiscardPile { get; }

        public PlayerState(
            int playerId,
            int health,
            int maxBlood,
            int currentBlood,
            IEnumerable<CardInstance> hand,
            IEnumerable<CardInstance> drawPile,
            IEnumerable<CardInstance> discardPile)
        {
            PlayerId = playerId;
            Health = health;
            MaxBlood = maxBlood;
            CurrentBlood = currentBlood;
            Hand = new List<CardInstance>(hand);
            DrawPile = new List<CardInstance>(drawPile);
            DiscardPile = new List<CardInstance>(discardPile);

        }

        // Tworzenie początkowego stanu gracza z domyślnym zdrowiem, krwią i pustymi stosami kart.
        public static PlayerState Initial(int playerId, List<CardInstance> startinDeck)
        {
            return new PlayerState(
                playerId,
                health: 20,
                maxBlood: 1,
                currentBlood: 1,
                drawPile: startinDeck,
                hand: new List<CardInstance>(),
                discardPile: new List<CardInstance>()
             ); /* Ręka / Pobieranie kart robimy później. */
        }

        /* ----------- Pomocnicze metody dla Immutable State ------- 
         * 1. Czy mamy wystarczająco dużo Blood, aby zagrać kartę */
        public bool CanPlayCard(int cost)
        {
            return CurrentBlood >= cost;
        }

        // 2. Metoda zwracająca nowy stan gracza po wydaniu krwi/many.
        public PlayerState WithBloodSpent(int amount)
        {
            return new PlayerState(
                PlayerId, Health, MaxBlood,
                CurrentBlood - amount, // Odjęcie krwi/many.
                Hand, DrawPile,
                DiscardPile);
        }

        public PlayerState WithCardRemovedFromHand(CardInstance cardToRemove)
        {
            var newHand = new List<CardInstance>(Hand);
            var index = newHand.FindIndex(c => c.InstanceId == cardToRemove.InstanceId);

            if (index != -1)
            {
                newHand.RemoveAt(index);
            }
            return new PlayerState(
                PlayerId,
                Health,
                MaxBlood,
                CurrentBlood,
                newHand,
                DrawPile,
                DiscardPile);
        }

    }
}
