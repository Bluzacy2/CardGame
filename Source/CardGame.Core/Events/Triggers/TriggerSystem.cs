using System;
using CardGame.Core.Application;
using CardGame.Core.State.Models;

namespace CardGame.Core.Events.Triggers
{
    public class TriggerSystem
    {
        // Główna metoda przetwarzająca kolejkę
        public GameState ProcessEvents(GameState currentState, EventBus eventBus)
        {
            var workingState = currentState;

            // Dopóki w kolejce są zdarzenia...
            while (eventBus.HasEvents)
            {
                var evt = eventBus.Pop();

                // Logika (na razie tylko logujemy, w przyszłości tu będzie pętla po kartach)
                Console.WriteLine($"[TRIGGER SYSTEM] Przetwarzam: {evt.GetType().Name}");

                // Tu w przyszłości dodamy:
                // foreach (var card in workingState.Board.GetAllUnits())
                //     if (card ma komponent) -> card.Resolve(...)
            }

            return workingState;
        }
    }
}