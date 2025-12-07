using CardGame.Core.Application;
using CardGame.Core.Cards.Component;
using CardGame.Core.Cards.Components.Implementations;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Factories;
using CardGame.Core.State.Models;
using System;

namespace CardGame.Core.Events.Triggers
{
    public class TriggerSystem
    {
       
        public GameState ProcessEvents(GameState currentState, EventBus eventBus, GameContext context)
        {
            var workingState = currentState;

            while (eventBus.HasEvents)
            {
                var evt = eventBus.Pop();

                // --- KROK 1: EFEKTY Z PLANSZY (Zone == Board) ---
                var boardUnits = workingState.Board.GetAllUnits();
                foreach (var unit in boardUnits)
                {
                    foreach (var effect in unit.Definition.Effects)
                    {
                        // Kluczowa poprawka: Sprawdzamy strefę!
                        if (effect.Zone != EffectZone.Board && effect.Zone != EffectZone.Any)
                            continue;

                        // Pomijamy OnPlayed (Battlecry), bo to nie jest efekt pasywny/reaktywny z planszy w tym sensie
                        if (effect.Trigger == CardGame.Core.Cards.Data.TriggerType.OnPlayed)
                            continue;

                        var component = new JsonEffectComponent(effect, unit.InstanceId);
                        if (component.ShouldTrigger(evt, workingState))
                        {
                            Console.WriteLine($"[TRIGGER BOARD] {unit.Definition.Name}: {evt.GetType().Name}");
                            workingState = component.Resolve(evt, workingState, context);
                        }
                    }
                }

                // --- KROK 2: EFEKTY Z RĘKI (Zone == Hand) ---
                
                var handCards = workingState.PlayerA.Hand.Concat(workingState.PlayerB.Hand);

                foreach (var card in handCards)
                {
                    foreach (var effect in card.Definition.Effects)
                    {
                        // Kluczowa poprawka: Reagujemy TYLKO jeśli efekt jest zdefiniowany jako Hand
                        if (effect.Zone != EffectZone.Hand && effect.Zone != EffectZone.Any)
                            continue;

                        var component = new JsonEffectComponent(effect, card.InstanceId);

                        // Tutaj ShouldTrigger zadziała normalnie
                        if (component.ShouldTrigger(evt, workingState))
                        {
                            Console.WriteLine($"[TRIGGER HAND] {card.Definition.Name}: {evt.GetType().Name}");
                            workingState = component.Resolve(evt, workingState, context);
                        }
                    }
                }

                // ---KROK 3: EFEKTY SPECJALNE(Battlecry / Deathrattle) ---
                
                if (evt is CardPlayedEvent cpe)
                {
                    foreach (var effectData in cpe.Card.Definition.Effects)
                    {
                        // Tutaj interesuje nas TYLKO OnPlayed
                        if (effectData.Trigger == CardGame.Core.Cards.Data.TriggerType.OnPlayed)
                        {
                            var component = new JsonEffectComponent(effectData, cpe.Card.InstanceId);
                            // ShouldTrigger i tak sprawdzi TriggerType, ale dla porządku:
                            if (component.ShouldTrigger(evt, workingState))
                            {
                                Console.WriteLine($"[TRIGGER] Battlecry karty {cpe.Card.Definition.Name}!");
                                workingState = component.Resolve(evt, workingState, context);
                            }
                        }
                    }
                }
                if (evt is UnitDiedEvent diedEvent)
                {
                    foreach (var effectData in diedEvent.Unit.Definition.Effects)
                    {
                        // Interesuje nas TYLKO trigger OnDeath tej konkretnej jednostki
                        if (effectData.Trigger == CardGame.Core.Cards.Data.TriggerType.OnDeath)
                        {
                            var component = new JsonEffectComponent(effectData, diedEvent.Unit.InstanceId);

                            if (component.ShouldTrigger(evt, workingState))
                            {
                                Console.WriteLine($"[TRIGGER] Deathrattle karty {diedEvent.Unit.Definition.Name}!");
                                workingState = component.Resolve(evt, workingState, context);
                            }
                        }
                    }
                }

            }

            return workingState;
        }
    }
}