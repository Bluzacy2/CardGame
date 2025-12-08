using CardGame.Core.Events;
using CardGame.Core.Cards.Factories;
using CardGame.Core.GameRules.Damage;

namespace CardGame.Core.Application
{
    public class GameContext
    {

        public CardFactory Factory { get; }
        public DeterministicRng Rng { get; }
        public EventBus Events { get; }

        public DamageCalculator DamageCalculator { get; }
        public GameContext(CardFactory factory, DeterministicRng rng, EventBus events, DamageCalculator damageCalculator)
        {
            Factory = factory;
            Rng = rng;
            Events = events;
            DamageCalculator = damageCalculator;
        }
    }
}