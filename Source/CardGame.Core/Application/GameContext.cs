using CardGame.Core.Cards.Components.Actions;
using CardGame.Core.Cards.Components.Actions.Handlers;
using CardGame.Core.Cards.Factories;
using CardGame.Core.Cards.Logic.Keywords;
using CardGame.Core.Cards.Logic.Keywords.Handlers;
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
        public KeywordProcessor Keywords { get; }
        public BattleService Battle { get; }
        public DeathResolver Death { get; }

        public GameContext(CardFactory factory, DeterministicRng rng, EventBus events)
        {
            Factory = factory;
            Rng = rng;
            Events = events;

            Keywords = new KeywordProcessor();
            Keywords.RegisterHandler(new ArmoredHandler());
            Keywords.RegisterHandler(new MarkedHandler());
            Keywords.RegisterHandler(new SplashDamageHandler());
            Keywords.RegisterHandler(new BurnSourceHandler());
            Keywords.RegisterHandler(new BurningHandler());
            Keywords.RegisterHandler(new SoulGuardHandler());
            Keywords.RegisterHandler(new UnkillableHandler());
            Keywords.RegisterHandler(new StunnedHandler());

            DamageCalculator = new DamageCalculator(this);
            Battle = new BattleService();
            Death = new DeathResolver();
            ActionRegistry = new ActionHandlerRegistry();

            RegisterActionHandlers();
        }

        private void RegisterActionHandlers()
        {
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

            ActionRegistry.Register(new BonusAttackHandler());
            ActionRegistry.Register(new HealToFullHandler());
            ActionRegistry.Register(new MoveRightHandler());
            ActionRegistry.Register(new GiveToOpponentHandler());

            ActionRegistry.Register(new SilenceHandler());
            ActionRegistry.Register(new AddResourceHandler());
            ActionRegistry.Register(new MakeAUnitHandler());
        }
    }
}