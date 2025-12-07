using CardGame.Core.Application;
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

        // Globalne buffy/modyfikatory dla jednostek tego gracza. - B.
        public CardStats GlobalUnitBuffs { get; }

        public PlayerState(
            int playerId,
            int health,
            int maxBlood,
            int currentBlood,
            IEnumerable<CardInstance> hand,
            IEnumerable<CardInstance> drawPile,
            IEnumerable<CardInstance> discardPile,
            CardStats? globalUnitBuffs = null)
        {
            PlayerId = playerId;
            Health = health;
            MaxBlood = maxBlood;
            CurrentBlood = currentBlood;
            Hand = new List<CardInstance>(hand);
            DrawPile = new List<CardInstance>(drawPile);
            DiscardPile = new List<CardInstance>(discardPile);
            GlobalUnitBuffs = globalUnitBuffs ?? new CardStats(0, 0, 0);

        }

        // Tworzenie początkowego stanu gracza z domyślnym zdrowiem, krwią i pustymi stosami kart.
        public static PlayerState Initial(int playerId, List<CardInstance> startingDeck)
        {
            return new PlayerState(
                playerId,
                health: 20,
                maxBlood: 1,
                currentBlood: 1,
                hand: new List<CardInstance>(),
                drawPile: startingDeck,
                discardPile: new List<CardInstance>(),
                new CardStats(0, 0, 0)
             ); /* Ręka / Pobieranie kart robimy później. */
        }

        public PlayerState With(
            int? health = null,
            int? maxBlood = null,
            int? currentBlood = null,
            IEnumerable<CardInstance>? hand = null,
            IEnumerable<CardInstance>? drawPile = null,
            IEnumerable<CardInstance>? discardPile = null,
            CardStats? globalUnitBuffs = null)
        {
            return new PlayerState(
                PlayerId,
                health ?? Health,
                maxBlood ?? MaxBlood,
                currentBlood ?? CurrentBlood,
                hand ?? Hand,
                drawPile ?? DrawPile,
                discardPile ?? DiscardPile,
                globalUnitBuffs ?? GlobalUnitBuffs
            );
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

        public PlayerState WithCardDrawn()
        {
            if (DrawPile.Count == 0)
            {
                return this;
            }
            var cardToDraw = DrawPile[0];
            var newDrawPile = new List<CardInstance>(DrawPile);
            newDrawPile.RemoveAt(0);

            var newHand = new List<CardInstance>(Hand);
            newHand.Add(cardToDraw);

            return new PlayerState(
                PlayerId, Health, MaxBlood, CurrentBlood, 
                newHand, newDrawPile, DiscardPile);
        }

        public PlayerState WithTurnStartBlood(int turnNumber)
        {
            int newMaxBlood = Math.Clamp(turnNumber, 1, 10);
            return new PlayerState(
                PlayerId, Health, newMaxBlood, newMaxBlood,
                Hand, DrawPile, DiscardPile);
        }

        public PlayerState WithCardAddedToHand(CardInstance card)
        {
            var newHand = new List<CardInstance>(Hand);
            newHand.Add(card);

            return new PlayerState(
                PlayerId,
                Health,
                MaxBlood,
                CurrentBlood,
                newHand, 
                DrawPile,
                DiscardPile
            );
        }

        public PlayerState WithCardAddedToDiscard(CardInstance card)
        {
            var newDiscardPile = new List<CardInstance>(DiscardPile);
            newDiscardPile.Add(card);

            return new PlayerState(
                PlayerId,
                Health,
                MaxBlood,
                CurrentBlood,
                Hand,
                DrawPile,
                newDiscardPile);
        }

        public PlayerState WithHealthRestored(int amount)
        {
            int newHealth = Math.Min(20, Health + amount);
            return new PlayerState(
                PlayerId,
                newHealth,
                MaxBlood,
                CurrentBlood,
                Hand,
                DrawPile,
                DiscardPile);
        }

        public PlayerState WithDamageTaken(int amount)
        {
            int newHealth = Health - amount;
            return new PlayerState(
                PlayerId,
                newHealth,
                MaxBlood,
                CurrentBlood,
                Hand,
                DrawPile,
                DiscardPile);
        }
        public PlayerState WithShuffledDeck(DeterministicRng rng)
        {
            var deckList = new List<CardInstance>(DrawPile);
            int n = deckList.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(0, n + 1);
                (deckList[k], deckList[n]) = (deckList[n], deckList[k]);
            }

            return With(drawPile: deckList);
        }
        public PlayerState WithGlobalBuffModifier(int atk, int hp)
        {
            var buffToAdd = new CardStats(atk, hp, 0);
            var newBuffs = GlobalUnitBuffs + buffToAdd; // Używa operatora +
            return With(globalUnitBuffs: newBuffs);
        }

        public PlayerState WithMulliganPerformed(List<int> cardInstanceIdsToReplace)
        {
            // Jeśli gracz nic nie wymienia, zwracamy stan bez zmian (ewentualnie logika przetasowania?)
            // Ale musimy pamiętać, że karty wracają na dno.
            if (cardInstanceIdsToReplace == null || cardInstanceIdsToReplace.Count == 0)
            {
                return this;
            }

            var currentHand = new List<CardInstance>(Hand);
            var currentDeck = new List<CardInstance>(DrawPile);
            int cardsToDraw = 0;

            // 1. Usuń karty z ręki i odłóż je "na bok" (żeby nie dobrać ich od razu, jeśli talia mała)
            // W Twoim przypadku: "Trafiają na dno".
            // Czyli najpierw dobieramy, a potem wkładamy stare na dno?
            // Zazwyczaj w karciankach: Wkładasz do talii -> Tasujesz -> Dobierasz.
            // Twoja zasada: "Trafiają na samo dno".
            // Więc: Usuń z ręki -> Dobierz z góry -> Dodaj usunięte na dół.

            var cardsToReturnToDeck = new List<CardInstance>();

            foreach (int idToRemove in cardInstanceIdsToReplace)
            {
                var card = currentHand.FirstOrDefault(c => c.InstanceId == idToRemove);
                if (card != null)
                {
                    currentHand.Remove(card);
                    cardsToReturnToDeck.Add(card); // Zapamiętujemy
                    cardsToDraw++;
                }
            }

            // 2. Dobierz nowe karty z góry talii
            for (int i = 0; i < cardsToDraw; i++)
            {
                if (currentDeck.Count > 0)
                {
                    var newCard = currentDeck[0]; // Bierzemy z góry (index 0)
                    currentDeck.RemoveAt(0);
                    currentHand.Add(newCard);
                }
            }

            // 3. Włóż stare karty na DNO talii (na koniec listy)
            foreach (var oldCard in cardsToReturnToDeck)
            {
                currentDeck.Add(oldCard);
            }

            return With(hand: currentHand, drawPile: currentDeck);
        }

    }
}
