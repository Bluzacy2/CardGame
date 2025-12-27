using CardGame.Core.Application;
using CardGame.Core.Cards.Components.Implementations;
using CardGame.Core.Cards.Data;
using CardGame.Core.State.Models;
using System.Linq;

namespace CardGame.Core.Events.Triggers
{
    public class TriggerSystem
    {
        public GameState ProcessEvents(GameState currentState, EventBus eventBus, GameContext context)
        {
            var workingState = currentState;

            while (eventBus.HasEvents)
            {
                // Jeśli poprzednia akcja wymusiła interakcję, przerywamy. 
                // Eventy zostają w kolejce (Bus) na później.
                if (workingState.PendingInteraction != null) break;

                var evt = eventBus.Pop();

                // 1. EFEKTY Z PLANSZY
                foreach (var unit in workingState.Board.GetAllUnits().ToList())
                {
                    foreach (var effect in unit.Definition.Effects)
                    {
                        if (effect.Zone != EffectZone.Board && effect.Zone != EffectZone.Any) continue;
                        if (effect.Trigger == TriggerType.OnPlayed) continue;

                        var component = new JsonEffectComponent(effect, unit.InstanceId);
                        if (component.ShouldTrigger(evt, workingState))
                        {
                            workingState = component.Resolve(evt, workingState, context);
                            if (workingState.PendingInteraction != null) return workingState;
                        }
                    }
                }

                // 2. EFEKTY Z RĘKI
                var hand = workingState.PlayerA.Hand.Concat(workingState.PlayerB.Hand).ToList();
                foreach (var card in hand)
                {
                    foreach (var effect in card.Definition.Effects)
                    {
                        if (effect.Zone != EffectZone.Hand && effect.Zone != EffectZone.Any) continue;
                        var component = new JsonEffectComponent(effect, card.InstanceId);
                        if (component.ShouldTrigger(evt, workingState))
                        {
                            workingState = component.Resolve(evt, workingState, context);
                            if (workingState.PendingInteraction != null) return workingState;
                        }
                    }
                }

                // 3. SPECIALS (Battlecry / Deathrattle)
                if (evt is CardPlayedEvent cpe)
                {
                    foreach (var eff in cpe.Card.Definition.Effects.Where(e => e.Trigger == TriggerType.OnPlayed))
                    {
                        var comp = new JsonEffectComponent(eff, cpe.Card.InstanceId);
                        workingState = comp.Resolve(evt, workingState, context);
                        if (workingState.PendingInteraction != null) return workingState;
                    }
                }
                if (evt is UnitDiedEvent ude)
                {
                    foreach (var eff in ude.Unit.Definition.Effects.Where(e => e.Trigger == TriggerType.OnDeath))
                    {
                        var comp = new JsonEffectComponent(eff, ude.Unit.InstanceId);
                        workingState = comp.Resolve(evt, workingState, context);
                        if (workingState.PendingInteraction != null) return workingState;
                    }
                }
            }
            return workingState;
        }
    }
}