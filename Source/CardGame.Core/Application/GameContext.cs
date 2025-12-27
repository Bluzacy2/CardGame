using CardGame.Core.Cards.Components.Actions;
using CardGame.Core.Cards.Components.Actions.Handlers;
using CardGame.Core.Cards.Factories;
using CardGame.Core.Events;
using CardGame.Core.GameRules.Battle;
using CardGame.Core.GameRules.Damage;
using CardGame.Core.GameRules.Death;

namespace CardGame.Core.Application
{
    public class GameContext
    {
        public CardFactory Factory { get; }
        public DeterministicRng Rng { get; }
        public EventBus Events { get; }
        public DamageCalculator DamageCalculator { get; }

        public ActionHandlerRegistry ActionRegistry { get; }
        public BattleService Battle { get; }
        public DeathResolver Death { get; }

        public GameContext(CardFactory factory, DeterministicRng rng, EventBus events, DamageCalculator damageCalculator)
        {
            Factory = factory;
            Rng = rng;
            Events = events;
            DamageCalculator = damageCalculator;

            Battle = new BattleService();
            Death = new DeathResolver();

            // Inicjalizacja Rejestru
            ActionRegistry = new ActionHandlerRegistry();

            // Rejestracja Handlerów
            ActionRegistry.Register(new DealDamageHandler());
            ActionRegistry.Register(new HealHandler());
            ActionRegistry.Register(new BuffStatsHandler());
            ActionRegistry.Register(new ApplyStatusHandler());
            ActionRegistry.Register(new AbsorbStatsHandler());
            ActionRegistry.Register(new ModifyGlobalBuffHandler());

            ActionRegistry.Register(new AddCardToHandHandler());
            ActionRegistry.Register(new DrawCardHandler());
            ActionRegistry.Register(new ShuffleDeckHandler());
            ActionRegistry.Register(new TutorCardHandler());

            ActionRegistry.Register(new DestroyUnitHandler());

            ActionRegistry.Register(new SacrificeUnitHandler());
            ActionRegistry.Register(new SummonUnitHandler());
            ActionRegistry.Register(new ReturnToHandHandler());

            ActionRegistry.Register(new DrawFromDiscardHandler());
        }
    }
}