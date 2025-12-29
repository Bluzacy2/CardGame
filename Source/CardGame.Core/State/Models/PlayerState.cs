using CardGame.Core.Application;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Models;
using CardGame.Core.Events;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CardGame.Core.State.Models
{
    public class PlayerState
    {
        public int PlayerId { get; }
        public int Health { get; }
        public int MaxBlood { get; }
        public int CurrentBlood { get; }
        public IReadOnlyList<CardInstance> Hand { get; }
        public IReadOnlyList<CardInstance> DrawPile { get; }
        public IReadOnlyList<CardInstance> DiscardPile { get; }
        public CardStats GlobalUnitBuffs { get; }

        public PlayerState(int playerId, int health, int maxBlood, int currentBlood,
            IEnumerable<CardInstance> hand, IEnumerable<CardInstance> drawPile,
            IEnumerable<CardInstance> discardPile, CardStats? globalUnitBuffs = null)
        {
            PlayerId = playerId; Health = health; MaxBlood = maxBlood; CurrentBlood = currentBlood;
            Hand = hand.ToList(); DrawPile = drawPile.ToList(); DiscardPile = discardPile.ToList();
            GlobalUnitBuffs = globalUnitBuffs ?? new CardStats(0, 0, 0);
        }

        public static PlayerState Initial(int playerId, List<CardInstance> startingDeck) =>
            new PlayerState(playerId, 20, 1, 1, new List<CardInstance>(), startingDeck, new List<CardInstance>());

        public PlayerState With(int? health = null, int? maxBlood = null, int? currentBlood = null,
            IEnumerable<CardInstance>? hand = null, IEnumerable<CardInstance>? drawPile = null,
            IEnumerable<CardInstance>? discardPile = null, CardStats? globalUnitBuffs = null)
        {
            return new PlayerState(PlayerId, health ?? Health, maxBlood ?? MaxBlood, currentBlood ?? CurrentBlood,
                hand ?? Hand, drawPile ?? DrawPile, discardPile ?? DiscardPile, globalUnitBuffs ?? GlobalUnitBuffs);
        }

        public PlayerState WithResourceChanged(ResourceType type, int current, int max)
        {
            if (type == ResourceType.Blood) return this.With(currentBlood: current, maxBlood: max);
            return this;
        }

        public bool CanPlayCard(int cost) => CurrentBlood >= cost;
        public PlayerState WithBloodSpent(int amount) => this.With(currentBlood: CurrentBlood - amount);
        public PlayerState WithCardRemovedFromHand(CardInstance card) => this.With(hand: Hand.Where(c => c.InstanceId != card.InstanceId));

        public PlayerState WithCardDrawn(EventBus? events = null)
        {
            if (DrawPile.Count == 0) return this;
            events?.Publish(new CardDrawnEvent(PlayerId));
            return this.With(hand: Hand.Append(DrawPile[0]), drawPile: DrawPile.Skip(1));
        }

        public PlayerState WithCardsDrawnFromDiscard(int count, int? excludeId = null)
        {
            var valid = DiscardPile.Where(c => c.InstanceId != excludeId).ToList();
            if (valid.Count == 0) return this;
            var toDraw = valid.TakeLast(Math.Min(count, valid.Count)).ToList();
            return this.With(hand: Hand.Concat(toDraw), discardPile: DiscardPile.Where(c => !toDraw.Any(d => d.InstanceId == c.InstanceId)));
        }

        public PlayerState WithTurnStartBlood(int manaLimit, bool refill)
        {
            int newMax = Math.Clamp(manaLimit, 1, 10);
            return this.With(maxBlood: newMax, currentBlood: refill ? newMax : CurrentBlood);
        }
        public PlayerState WithCardAddedToHand(CardInstance card) => this.With(hand: Hand.Append(card));
        public PlayerState WithCardAddedToDiscard(CardInstance card) => this.With(discardPile: DiscardPile.Append(card));
        public PlayerState WithHealthRestored(int amount) => this.With(health: Health + amount);
        public PlayerState WithDamageTaken(int amount) => this.With(health: Health - amount);
        public PlayerState WithShuffledDeck(DeterministicRng rng)
        {
            var list = DrawPile.ToList();
            int n = list.Count;
            while (n > 1) { n--; int k = rng.Next(0, n + 1); (list[k], list[n]) = (list[n], list[k]); }
            return this.With(drawPile: list);
        }
        public PlayerState WithGlobalBuffModifier(int atk, int hp) => this.With(globalUnitBuffs: GlobalUnitBuffs + new CardStats(atk, hp, 0));
        public PlayerState WithCardRemovedFromDeck(CardInstance card) => this.With(drawPile: DrawPile.Where(c => c.InstanceId != card.InstanceId));
        public PlayerState WithMulliganPerformed(List<int> ids)
        {
            var toRep = Hand.Where(c => ids.Contains(c.InstanceId)).ToList();
            var nHand = Hand.Where(c => !ids.Contains(c.InstanceId)).ToList();
            var nDeck = DrawPile.ToList();
            foreach (var c in toRep) { if (nDeck.Count > 0) { nHand.Add(nDeck[0]); nDeck.RemoveAt(0); } nDeck.Add(c); }
            return this.With(hand: nHand, drawPile: nDeck);
        }
    }
}