using CardGame.Core.Events;
using CardGame.Core.Cards.Factories;

namespace CardGame.Core.Application
{
    public class GameContext
    {

        public CardFactory Factory { get; }
        public DeterministicRng Rng { get; }
        public EventBus Events { get; }

        public GameContext(CardFactory factory, DeterministicRng rng, EventBus events)
        {
            Factory = factory;
            Rng = rng;
            Events = events;
        }
    }
}