using CardGame.Core.Application;
using CardGame.Core.Cards.Component;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Logic; // Używa EffectTargetResolver
using CardGame.Core.Cards.Models;
using CardGame.Core.Events;
using CardGame.Core.Events.Interfaces;
using CardGame.Core.GameRules.Damage;
using CardGame.Core.State.Models;
using System;
using System.Linq;

namespace CardGame.Core.Cards.Components.Implementations
{
    public class JsonEffectComponent : ICardComponent
    {
        private readonly EffectData _effectData;
        private readonly int _ownerCardInstanceId;

        public JsonEffectComponent(EffectData effectData, int ownerCardInstanceId)
        {
            _effectData = effectData;
            _ownerCardInstanceId = ownerCardInstanceId;
        }

        // 1. SPRAWDZANIE TRIGGERÓW
        public bool ShouldTrigger(IGameEvent gameEvent, GameState state)
        {
            switch (_effectData.Trigger)
            {
                case TriggerType.OnPlayed:
                    return gameEvent is CardPlayedEvent cpe && cpe.Card.InstanceId == _ownerCardInstanceId;

                case TriggerType.OnDeath:
                    return gameEvent is UnitDiedEvent ude && ude.Unit.InstanceId == _ownerCardInstanceId;

                case TriggerType.OnSacrificed:
                    // A. Trigger na samej ofierze (np. Black Cat)
                    if (gameEvent is UnitSacrificedEvent use && use.Unit.InstanceId == _ownerCardInstanceId)
                        return true;

                    // B. Trigger na innej karcie (np. The Moder w ręce)
                    // Tutaj nie musimy sprawdzać ID, bo TriggerSystem już przefiltrował strefę (Zone: Hand).
                    // Jedyne co warto sprawdzić, to czy ofiara należy do właściciela tej karty (żeby nie triggerować się na ofiary wroga).
                    if (gameEvent is UnitSacrificedEvent useOther)
                    {
                        int myOwnerId = EffectTargetResolver.DetermineSourceOwner(state, gameEvent, _ownerCardInstanceId);
                        if (useOther.OwnerId == myOwnerId) return true;
                    }
                    return false;

                case TriggerType.OnFriendlyUnitDied:
                    if (gameEvent is UnitDiedEvent diedEvent)
                    {
                        // Triggeruj tylko jeśli zginęła INNA jednostka tego samego gracza
                        int myOwnerId = EffectTargetResolver.DetermineSourceOwner(state, gameEvent, _ownerCardInstanceId);

                        if (diedEvent.Unit.OwnerPlayerId == myOwnerId && diedEvent.Unit.InstanceId != _ownerCardInstanceId)
                        {
                            return true;
                        }
                    }
                    return false;
            }
            return false;
        }

        // 2. GŁÓWNA PĘTLA ROZWIĄZYWANIA
        public GameState Resolve(IGameEvent gameEvent, GameState currentState, GameContext context)
        {
            var workingState = currentState;
            Console.WriteLine($"[DEBUG] Rozpoczynam efekty ID: {_ownerCardInstanceId} (Trigger: {_effectData.Trigger})");

            foreach (var action in _effectData.Actions)
            {
                workingState = ExecuteSingleAction(action, workingState, gameEvent, context);
            }
            return workingState;
        }

        // 3. DYSPOZYTOR AKCJI (Switch)
        private GameState ExecuteSingleAction(ActionData action, GameState state, IGameEvent contextEvent, GameContext context)
        {
            // Używamy zewnętrznego Resolvera
            var targets = EffectTargetResolver.Resolve(action.Target, state, contextEvent, _ownerCardInstanceId);

            // Podstawowa walidacja (SummonUnit jest wyjątkiem, bo bazuje na Evencie)
            if (targets.TargetPlayer == null && targets.TargetUnit == null && action.Type != ActionType.SummonUnit)
            {
                // Console.WriteLine("[DEBUG] Brak celu dla akcji. Pomijam.");
                return state;
            }

            switch (action.Type)
            {
                case ActionType.DealDamage: return HandleDealDamage(action, state, context, targets);
                case ActionType.Heal: return HandleHeal(action, state, targets);
                case ActionType.BuffStats: return HandleBuffStats(action, state, targets);
                case ActionType.ModifyGlobalBuff: return HandleGlobalBuff(action, state, targets);
                case ActionType.ApplyStatus: return HandleApplyStatus(action, state, targets);
                case ActionType.AbsorbStats: return HandleAbsorbStats(state, targets);

                case ActionType.AddCardToHand: return HandleAddCard(action, state, context, targets);
                case ActionType.DrawCard: return HandleDrawCard(action, state, targets);
                case ActionType.TutorCard: return HandleTutorCard(state, targets, contextEvent);
                case ActionType.ShuffleDeck: return HandleShuffle(state, context, targets);

                case ActionType.SacrificeUnit: return HandleSacrifice(state, context, targets, contextEvent);
                case ActionType.SummonUnit: return HandleSummonUnit(state, contextEvent, targets);
                case ActionType.ReturnToHand: return HandleReturnToHand(state, targets);
            }

            return state;
        }

        // --- HANDLERY KONKRETNYCH AKCJI ---

        private GameState HandleDealDamage(ActionData action, GameState state, GameContext context, EffectTargets targets)
        {
            CardInstance? sourceCard = null;
            sourceCard = state.Board.GetAllUnits().FirstOrDefault(u => u.InstanceId == _ownerCardInstanceId);

            if (sourceCard == null)
            {

                sourceCard = state.PlayerA.Hand.FirstOrDefault(u => u.InstanceId == _ownerCardInstanceId)
                          ?? state.PlayerB.Hand.FirstOrDefault(u => u.InstanceId == _ownerCardInstanceId);
            }

                if (targets.TargetPlayer != null)
            {
                var newPlayer = targets.TargetPlayer.WithDamageTaken(action.Amount);
                Console.WriteLine($"[EFEKT] {action.Amount} dmg w gracza {newPlayer.PlayerId}");
                return state.UpdatePlayer(newPlayer);
            }
            if (targets.TargetUnit != null)
            {
                var dmgContext = new DamageContext(
                    sourceCard,
                    targets.TargetUnit,
                    action.Amount,
                    DamageType.Effect);
                int finalDamage = context.DamageCalculator.CalculateFinalDamage(dmgContext);

                // TODO: W przyszłości wpiąć tu DamageCalculator
                var damagedUnit = targets.TargetUnit.TakeDamage(action.Amount);
                Console.WriteLine($"[EFEKT] {action.Amount} dmg (Final: {finalDamage}) w jednostkę {damagedUnit.Definition.Name}");
                return state.UpdateBoard(state.Board.UpdateUnit(damagedUnit));
            }
            return state;
        }

        private GameState HandleHeal(ActionData action, GameState state, EffectTargets targets)
        {
            if (targets.TargetPlayer != null)
            {
                var newPlayer = targets.TargetPlayer.WithHealthRestored(action.Amount);
                Console.WriteLine($"[EFEKT] Uleczono gracza {newPlayer.PlayerId}");
                return state.UpdatePlayer(newPlayer);
            }
            return state;
        }

        private GameState HandleBuffStats(ActionData action, GameState state, EffectTargets targets)
        {
            if (targets.TargetUnit != null)
            {
                var stats = targets.TargetUnit.CurrentStats;
                // Interpretacja: Amount -> Koszt, BuffAtk/Hp -> Staty
                int newCost = stats.BloodCost + action.Amount;
                int newAtk = stats.Attack + action.BuffAtk;
                int newHp = stats.Health + action.BuffHp;

                var newStats = new CardStats(newAtk, newHp, newCost, stats.Keywords);
                var newUnit = targets.TargetUnit.WithStats(newStats);

                // Sprawdzamy czy jednostka jest w Ręce czy na Stole
                var owner = state.GetPlayer(newUnit.OwnerPlayerId);
                if (owner.Hand.Any(c => c.InstanceId == newUnit.InstanceId))
                {
                    // W ręce
                    var p = owner.WithCardRemovedFromHand(targets.TargetUnit).WithCardAddedToHand(newUnit);
                    Console.WriteLine($"[EFEKT] Buff w ręce: {newUnit.Definition.Name} (Nowy koszt: {newCost})");
                    return state.UpdatePlayer(p);
                }
                else
                {
                    // Na stole
                    return state.UpdateBoard(state.Board.UpdateUnit(newUnit));
                }
            }
            return state;
        }

        private GameState HandleGlobalBuff(ActionData action, GameState state, EffectTargets targets)
        {
            if (targets.TargetPlayer != null)
            {
                var newP = targets.TargetPlayer.WithGlobalBuffModifier(action.BuffAtk, action.BuffHp);
                state = state.UpdatePlayer(newP);
                Console.WriteLine($"[EFEKT] Global Buff dla gracza {newP.PlayerId}");

                // Aktualizuj obecne jednostki
                var units = state.Board.GetAllUnits().Where(u => u.OwnerPlayerId == newP.PlayerId);
                var buff = new CardStats(action.BuffAtk, action.BuffHp, 0);
                foreach (var u in units)
                {
                    state = state.UpdateBoard(state.Board.UpdateUnit(u.WithStats(u.CurrentStats + buff)));
                }
                return state;
            }
            return state;
        }

        private GameState HandleApplyStatus(ActionData action, GameState state, EffectTargets targets)
        {
            if (targets.TargetUnit != null && Enum.TryParse<Keyword>(action.StringParam, out var k))
            {
                Console.WriteLine($"[EFEKT] Status {k} dla {targets.TargetUnit.Definition.Name}");
                return state.UpdateBoard(state.Board.UpdateUnit(targets.TargetUnit.WithKeywordAdded(k)));
            }
            return state;
        }

        private GameState HandleAbsorbStats(GameState state, EffectTargets targets)
        {
            if (targets.TargetUnit != null)
            {
                var me = state.Board.GetAllUnits().FirstOrDefault(u => u.InstanceId == _ownerCardInstanceId);
                if (me != null)
                {
                    var victimStats = targets.TargetUnit.CurrentStats;
                    var newStats = new CardStats(
                        me.CurrentStats.Attack + victimStats.Attack,
                        me.CurrentStats.Health + victimStats.Health,
                        me.CurrentStats.BloodCost,
                        me.CurrentStats.Keywords);

                    var biggerMe = me.WithStats(newStats);
                    Console.WriteLine($"[EFEKT] AbsorbStats: {biggerMe.Definition.Name} rośnie!");
                    return state.UpdateBoard(state.Board.UpdateUnit(biggerMe));
                }
            }
            return state;
        }

        private GameState HandleSacrifice(GameState state, GameContext context, EffectTargets targets, IGameEvent contextEvent)
        {
            if (targets.TargetUnit != null)
            {
                // Używamy helpera, żeby ustalić, kto wywołał efekt (czyli kto poświęca)
                int sourceOwnerId = EffectTargetResolver.DetermineSourceOwner(state, contextEvent, _ownerCardInstanceId);

                if (targets.TargetUnit.OwnerPlayerId != sourceOwnerId)
                {
                    Console.WriteLine("[BŁĄD ZASAD] Nie można poświęcić wrogiej jednostki!");
                    return state;
                }

                Console.WriteLine($"[EFEKT] POŚWIĘCAM: {targets.TargetUnit.Definition.Name}");
                context.Events.Publish(new UnitSacrificedEvent(targets.TargetUnit));

                var deadUnit = targets.TargetUnit.TakeDamage(9999);
                return state.UpdateBoard(state.Board.UpdateUnit(deadUnit));
            }
            return state;
        }

        private GameState HandleSummonUnit(GameState state, IGameEvent evt, EffectTargets targets)
        {
            if (evt is UnitDiedEvent deathEvt && targets.TargetUnit != null)
            {
                var owner = state.GetPlayer(targets.TargetUnit.OwnerPlayerId);
                var newOwner = owner.WithCardRemovedFromHand(targets.TargetUnit);
                state = state.UpdatePlayer(newOwner);

                state = state.UpdateBoard(state.Board.WithUnitPlacedAt(deathEvt.LineIndex, owner.PlayerId, targets.TargetUnit));
                Console.WriteLine($"[EFEKT] Summon: {targets.TargetUnit.Definition.Name} na linię {deathEvt.LineIndex}");
            }
            return state;
        }

        private GameState HandleReturnToHand(GameState state, EffectTargets targets)
        {
            if (targets.TargetUnit != null)
            {
                var unit = targets.TargetUnit;
                var freshCard = unit.WithStats(unit.Definition.BaseStats); // Reset statystyk
                var owner = state.GetPlayer(unit.OwnerPlayerId);
                state = state.UpdatePlayer(owner.WithCardAddedToHand(freshCard));

                // Usuń z planszy
                for (int i = 0; i < 4; i++)
                {
                    var l = state.Board.Lines[i];
                    if (l.Player1Unit?.InstanceId == unit.InstanceId)
                        state = state.UpdateBoard(state.Board.WithUnitPlacedAt(i, 1, null));
                    if (l.Player2Unit?.InstanceId == unit.InstanceId)
                        state = state.UpdateBoard(state.Board.WithUnitPlacedAt(i, 2, null));
                }
                Console.WriteLine($"[EFEKT] ReturnToHand: {unit.Definition.Name}");
            }
            return state;
        }

        private GameState HandleAddCard(ActionData action, GameState state, GameContext context, EffectTargets targets)
        {
            if (targets.TargetPlayer != null)
            {
                try
                {
                    var tokenCard = context.Factory.CreateCard(action.ValueParam, targets.TargetPlayer.PlayerId);
                    Console.WriteLine($"[EFEKT] Dodano {tokenCard.Definition.Name} do ręki.");
                    return state.UpdatePlayer(targets.TargetPlayer.WithCardAddedToHand(tokenCard));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[BŁĄD] AddCard: {ex.Message}");
                }
            }
            return state;
        }

        private GameState HandleDrawCard(ActionData action, GameState state, EffectTargets targets)
        {
            if (targets.TargetPlayer != null)
            {
                Console.WriteLine($"[EFEKT] Gracz {targets.TargetPlayer.PlayerId} dobiera kartę.");
                return state.UpdatePlayer(targets.TargetPlayer.WithCardDrawn());
            }
            return state;
        }

        private GameState HandleShuffle(GameState state, GameContext context, EffectTargets targets)
        {
            if (targets.TargetPlayer != null)
            {
                Console.WriteLine($"[EFEKT] Tasowanie talii gracza {targets.TargetPlayer.PlayerId}.");
                return state.UpdatePlayer(targets.TargetPlayer.WithShuffledDeck(context.Rng));
            }
            return state;
        }

        private GameState HandleTutorCard(GameState state, EffectTargets targets, IGameEvent evt)
        {
            if (targets.TargetPlayer != null && evt is CardPlayedEvent cpe && cpe.SelectedTargetId.HasValue)
            {
                var card = targets.TargetPlayer.DrawPile.FirstOrDefault(c => c.InstanceId == cpe.SelectedTargetId.Value);
                if (card != null)
                {
                    Console.WriteLine($"[EFEKT] TUTOR: {card.Definition.Name}");
                    return state.UpdatePlayer(targets.TargetPlayer.WithCardRemovedFromDeck(card).WithCardAddedToHand(card));
                }
                else
                {
                    Console.WriteLine("[EFEKT BŁĄD] Tutor: Nie znaleziono karty w talii.");
                }
            }
            return state;
        }
    }
}