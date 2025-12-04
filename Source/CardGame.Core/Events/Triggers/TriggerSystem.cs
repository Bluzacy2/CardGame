using CardGame.Core.Application;
using CardGame.Core.Cards.Component;
using CardGame.Core.State.Models;
using System;

namespace CardGame.Core.Events.Triggers
{
    public class TriggerSystem
    {
        // Główna metoda przetwarzająca kolejkę
        public GameState ProcessEvents(GameState currentState, EventBus eventBus)
        {
            var workingState = currentState;

            while (eventBus.HasEvents)
            {
                var evt = eventBus.Pop();

                // 1. ZBIERZ WSZYSTKIE KARTY, KTÓRE MOGĄ ZAREAGOWAĆ
                // (Jednostki na stole + ewentualnie karty w ręce, jeśli obsługujemy triggery z ręki)
                var activeUnits = workingState.Board.GetAllUnits(); // Dodaj tę metodę do BoardState!

                // Dla zdarzenia CardPlayedEvent, sama zagrana karta też może zareagować (Battlecry)
                // Musimy ją znaleźć (jest w evencie)

                // 2. SPRAWDŹ KAŻDĄ JEDNOSTKĘ
                foreach (var unit in activeUnits)
                {
                    // Czy ta jednostka ma efekty w definicji?
                    foreach (var effectData in unit.Definition.Effects)
                    {
                        if (effectData.Trigger == CardGame.Core.Cards.Data.TriggerType.OnPlayed)
                            continue;
                        // Tworzymy komponent "w locie" (to lekkie)
                        var component = new JsonEffectComponent(effectData, unit.InstanceId);

                        if (component.ShouldTrigger(evt, workingState))
                        {
                            Console.WriteLine($"[TRIGGER] Uruchamiam efekt karty {unit.Definition.Name}!");
                            workingState = component.Resolve(evt, workingState);
                        }
                    }
                }

                // Specjalny przypadek: OnPlayed (Battlecry) dla karty, która właśnie wchodzi
                // Ona może jeszcze nie być na liście "activeUnits" w zależności od momentu
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
                                workingState = component.Resolve(evt, workingState);
                            }
                        }
                    }
                }
            }

            return workingState;
        }
    }
}