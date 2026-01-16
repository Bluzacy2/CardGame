using CardGame.Core.Application;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Models;
using CardGame.Core.Events;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CardGame.Core.State.Models
{
    /// <summary>
    /// Represents the state of a single player in the game, including resources, cards, and buffs.
    /// </summary>
    public class PlayerState
    {
        #region Constants
        /// <summary>
        /// Maximum number of cards allowed in a player's hand.
        /// </summary>
        public const int MaxHandSize = 8;
        #endregion

        #region Properties
        /// <summary>
        /// Gets the unique identifier of the player (1 or 2).
        /// </summary>
        public int PlayerId { get; }

        /// <summary>
        /// Gets the current health of the player.
        /// </summary>
        public int Health { get; }

        /// <summary>
        /// Gets the maximum blood resource available to the player.
        /// </summary>
        public int MaxBlood { get; }

        /// <summary>
        /// Gets the current available blood resource.
        /// </summary>
        public int CurrentBlood { get; }

        /// <summary>
        /// Gets the cards currently in the player's hand.
        /// </summary>
        public IReadOnlyList<CardInstance> Hand { get; }

        /// <summary>
        /// Gets the player's draw pile (deck).
        /// </summary>
        public IReadOnlyList<CardInstance> DrawPile { get; }

        /// <summary>
        /// Gets the player's discard pile (graveyard).
        /// </summary>
        public IReadOnlyList<CardInstance> DiscardPile { get; }

        /// <summary>
        /// Gets the global stat buffs applied to all units controlled by this player.
        /// </summary>
        public CardStats GlobalUnitBuffs { get; }
        #endregion

        #region Constructor
        /// <summary>
        /// Initializes a new instance of the PlayerState class.
        /// </summary>
        /// <param name="playerId">The unique identifier of the player.</param>
        /// <param name="health">The current health of the player.</param>
        /// <param name="maxBlood">The maximum blood resource available.</param>
        /// <param name="currentBlood">The current available blood resource.</param>
        /// <param name="hand">The cards in the player's hand.</param>
        /// <param name="drawPile">The player's draw pile.</param>
        /// <param name="discardPile">The player's discard pile.</param>
        /// <param name="globalUnitBuffs">Optional global unit buffs.</param>
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
            Hand = hand.ToList();
            DrawPile = drawPile.ToList();
            DiscardPile = discardPile.ToList();
            GlobalUnitBuffs = globalUnitBuffs ?? new CardStats(0, 0, 0);
        }
        #endregion

        #region Static Factory Methods
        /// <summary>
        /// Creates an initial player state for the start of a game.
        /// </summary>
        /// <param name="playerId">The unique identifier of the player.</param>
        /// <param name="startingDeck">The player's starting deck.</param>
        /// <returns>A new PlayerState representing the initial state.</returns>
        public static PlayerState Initial(int playerId, List<CardInstance> startingDeck) =>
            new PlayerState(playerId, 20, 1, 1, new List<CardInstance>(), startingDeck, new List<CardInstance>());
        #endregion

        #region Immutable State Modification Methods
        /// <summary>
        /// Creates a new PlayerState with the specified properties modified.
        /// </summary>
        /// <param name="health">Optional new health value.</param>
        /// <param name="maxBlood">Optional new maximum blood value.</param>
        /// <param name="currentBlood">Optional new current blood value.</param>
        /// <param name="hand">Optional new hand collection.</param>
        /// <param name="drawPile">Optional new draw pile collection.</param>
        /// <param name="discardPile">Optional new discard pile collection.</param>
        /// <param name="globalUnitBuffs">Optional new global unit buffs.</param>
        /// <returns>A new PlayerState with the specified modifications.</returns>
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
                globalUnitBuffs ?? GlobalUnitBuffs);
        }

        /// <summary>
        /// Updates the player's resource values.
        /// </summary>
        /// <param name="type">The type of resource to update (currently only Blood).</param>
        /// <param name="current">The new current value of the resource.</param>
        /// <param name="max">The new maximum value of the resource.</param>
        /// <returns>A new PlayerState with updated resource values.</returns>
        public PlayerState WithResourceChanged(ResourceType type, int current, int max) =>
            type == ResourceType.Blood ? With(currentBlood: current, maxBlood: max) : this;

        /// <summary>
        /// Spends a specified amount of blood resource.
        /// </summary>
        /// <param name="amount">The amount of blood to spend.</param>
        /// <returns>A new PlayerState with reduced current blood.</returns>
        public PlayerState WithBloodSpent(int amount) => With(currentBlood: CurrentBlood - amount);

        /// <summary>
        /// Removes a card from the player's hand.
        /// </summary>
        /// <param name="card">The card to remove.</param>
        /// <returns>A new PlayerState with the card removed from hand.</returns>
        public PlayerState WithCardRemovedFromHand(CardInstance card) => 
            With(hand: Hand.Where(c => c.InstanceId != card.InstanceId));

        /// <summary>
        /// Draws a card from the draw pile into the hand.
        /// </summary>
        /// <param name="events">Optional event bus for publishing draw events.</param>
        /// <returns>A new PlayerState with a card drawn, or the same state if drawing is not possible.</returns>
        public PlayerState WithCardDrawn(EventBus? events = null)
        {
            // If the draw pile is empty OR the hand has reached maximum size - we stop
            if (DrawPile.Count == 0 || Hand.Count >= MaxHandSize)
                return this;

            var card = DrawPile[0];
            events?.Publish(new CardDrawnEvent(PlayerId));
            events?.Publish(new CardMovedEvent(card.InstanceId, PlayerId, CardZone.Deck, CardZone.Hand));

            return With(
                hand: Hand.Append(card),
                drawPile: DrawPile.Skip(1)
            );
        }

        /// <summary>
        /// Draws cards from the discard pile into the hand.
        /// </summary>
        /// <param name="count">The number of cards to draw.</param>
        /// <param name="events">Optional event bus for publishing card movement events.</param>
        /// <param name="excludeId">Optional card instance ID to exclude from drawing.</param>
        /// <returns>A new PlayerState with cards drawn from discard, or the same state if drawing is not possible.</returns>
        public PlayerState WithCardsDrawnFromDiscard(int count, EventBus? events = null, int? excludeId = null)
        {
            var validCards = DiscardPile.Where(card => card.InstanceId != excludeId).ToList();
            if (validCards.Count == 0 || Hand.Count >= MaxHandSize) 
                return this;

            int spaceInHand = MaxHandSize - Hand.Count;
            int actualToDraw = Math.Min(count, spaceInHand);

            if (actualToDraw <= 0) 
                return this;

            var cardsToDraw = validCards.TakeLast(actualToDraw).ToList();
            if (events != null)
            {
                foreach (var card in cardsToDraw)
                {
                    events.Publish(new CardMovedEvent(card.InstanceId, PlayerId, CardZone.Graveyard, CardZone.Hand));
                }
            }
            
            return With(
                hand: Hand.Concat(cardsToDraw),
                discardPile: DiscardPile.Where(card => !cardsToDraw.Any(drawn => drawn.InstanceId == card.InstanceId))
            );
        }

        /// <summary>
        /// Updates blood resources at the start of a turn.
        /// </summary>
        /// <param name="manaLimit">The maximum mana (blood) limit for the turn.</param>
        /// <param name="refill">Whether to refill the current blood to the maximum.</param>
        /// <returns>A new PlayerState with updated blood resources.</returns>
        public PlayerState WithTurnStartBlood(int manaLimit, bool refill)
        {
            int newMax = Math.Clamp(manaLimit, 1, 10);
            return With(maxBlood: newMax, currentBlood: refill ? newMax : CurrentBlood);
        }

        /// <summary>
        /// Adds a card to the player's hand.
        /// </summary>
        /// <param name="card">The card to add.</param>
        /// <returns>A new PlayerState with the card added to hand, or the same state if hand is full.</returns>
        public PlayerState WithCardAddedToHand(CardInstance card) =>
            Hand.Count >= MaxHandSize ? this : With(hand: Hand.Append(card));

        /// <summary>
        /// Adds a card to the player's discard pile.
        /// </summary>
        /// <param name="card">The card to add.</param>
        /// <returns>A new PlayerState with the card added to the discard pile.</returns>
        public PlayerState WithCardAddedToDiscard(CardInstance card) => With(discardPile: DiscardPile.Append(card));

        /// <summary>
        /// Restores health to the player.
        /// </summary>
        /// <param name="amount">The amount of health to restore.</param>
        /// <returns>A new PlayerState with increased health.</returns>
        public PlayerState WithHealthRestored(int amount) => With(health: Health + amount);

        /// <summary>
        /// Applies damage to the player.
        /// </summary>
        /// <param name="amount">The amount of damage to apply.</param>
        /// <returns>A new PlayerState with reduced health.</returns>
        public PlayerState WithDamageTaken(int amount) => With(health: Health - amount);

        /// <summary>
        /// Shuffles the player's draw pile.
        /// </summary>
        /// <param name="rng">The random number generator to use for shuffling.</param>
        /// <returns>A new PlayerState with a shuffled draw pile.</returns>
        public PlayerState WithShuffledDeck(DeterministicRng rng)
        {
            var list = DrawPile.ToList();
            int n = list.Count;
            while (n > 1) 
            { 
                n--; 
                int k = rng.Next(0, n + 1); 
                (list[k], list[n]) = (list[n], list[k]); 
            }
            return With(drawPile: list);
        }

        /// <summary>
        /// Modifies the global unit buffs for this player.
        /// </summary>
        /// <param name="attack">The attack value to add to global buffs.</param>
        /// <param name="health">The health value to add to global buffs.</param>
        /// <returns>A new PlayerState with updated global unit buffs.</returns>
        public PlayerState WithGlobalBuffModifier(int attack, int health) => 
            With(globalUnitBuffs: GlobalUnitBuffs + new CardStats(attack, health, 0));

        /// <summary>
        /// Removes a card from the player's draw pile.
        /// </summary>
        /// <param name="card">The card to remove.</param>
        /// <returns>A new PlayerState with the card removed from the draw pile.</returns>
        public PlayerState WithCardRemovedFromDeck(CardInstance card) => 
            With(drawPile: DrawPile.Where(c => c.InstanceId != card.InstanceId));

        /// <summary>
        /// Performs a mulligan operation, replacing specified cards in hand with new ones from the deck.
        /// </summary>
        /// <param name="cardIds">The instance IDs of cards to replace.</param>
        /// <returns>A new PlayerState after performing the mulligan.</returns>
        public PlayerState WithMulliganPerformed(List<int> cardIds)
        {
            var cardsToReplace = Hand.Where(card => cardIds.Contains(card.InstanceId)).ToList();
            var newHand = Hand.Where(card => !cardIds.Contains(card.InstanceId)).ToList();
            var newDeck = DrawPile.ToList();
            
            foreach (var card in cardsToReplace)
            {
                if (newDeck.Count > 0)
                {
                    newHand.Add(newDeck[0]);
                    newDeck.RemoveAt(0);
                }
                newDeck.Add(card);
            }
            
            return With(hand: newHand, drawPile: newDeck);
        }
        #endregion

        #region Helper Methods
        /// <summary>
        /// Determines whether the player can play a card with the specified cost.
        /// </summary>
        /// <param name="cost">The blood cost of the card.</param>
        /// <returns>True if the player has enough blood to play the card, otherwise false.</returns>
        public bool CanPlayCard(int cost) => CurrentBlood >= cost;
        #endregion
    }
}