using CardGame.Core.Application;
using CardGame.Core.Cards.Component;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Logic;
using CardGame.Core.Events;
using CardGame.Core.Events.Interfaces;
using CardGame.Core.State.Models;
using System;

namespace CardGame.Core.Cards.Components.Implementations
{
    public class JsonEffectComponent : ICardComponent
    {
        private readonly EffectData _effectData;
        private readonly int _ownerCardInstanceId;
        private readonly int _effectIndex; // Musimy wiedzieć, który to efekt na liście

        // Zaktualizowany konstruktor
        public JsonEffectComponent(EffectData effectData, int ownerCardInstanceId, int effectIndex = 0)
        {
            _effectData = effectData;
            _ownerCardInstanceId = ownerCardInstanceId;
            _effectIndex = effectIndex;
        }

        public bool ShouldTrigger(IGameEvent gameEvent, GameState state)
        {
            return TriggerLogic.Check(_effectData.Trigger, gameEvent, state, _ownerCardInstanceId);
        }

        public GameState Resolve(IGameEvent gameEvent, GameState currentState, GameContext context)
        {
            return ResolveFromIndex(gameEvent, currentState, context, 0);
        }

        public GameState ResolveFromIndex(IGameEvent gameEvent, GameState currentState, GameContext context, int startActionIndex)
        {
            var workingState = currentState;

            for (int i = startActionIndex; i < _effectData.Actions.Count; i++)
            {
                var action = _effectData.Actions[i];

                // 1. Sprawdź czy to akcja wymagająca interakcji
                if (IsPlayerSelectionRequired(action.Target))
                {
                    bool targetProvided = false;

                    // A. Mamy cel w CardPlayedEvent (tylko dla pierwszej akcji - index 0)
                    if (startActionIndex == 0 && i == 0 && gameEvent is CardPlayedEvent cpe && cpe.SelectedTargetId.HasValue)
                        targetProvided = true;

                    // B. Mamy cel w TargetSelectedEvent (dla wznawiania)
                    if (gameEvent is TargetSelectedEvent)
                        targetProvided = true;

                    // Jeśli nie mamy celu -> PRZERYWAMY
                    if (!targetProvided)
                    {
                        Console.WriteLine($"[INTERAKCJA] Zatrzymano na efekcie {_effectIndex}, akcji {i}. Czekam na cel.");
                        return workingState.With(
                            pendingInteraction: new PendingInteraction(_ownerCardInstanceId, _effectIndex, i, action.Target)
                        );
                    }
                }

                // 2. Standardowe rozwiązywanie
                var targets = EffectTargetResolver.Resolve(action.Target, workingState, gameEvent, _ownerCardInstanceId);

                // Walidacja dostępności celów
                if (targets.TargetPlayer == null && targets.TargetUnit == null
                    && action.Type != ActionType.SummonUnit
                    && action.Type != ActionType.AddCardToHand)
                {
                    continue;
                }

                // 3. Wykonanie
                try
                {
                    var handler = context.ActionRegistry.GetHandler(action.Type);
                    workingState = handler.Execute(workingState, context, action, targets, _ownerCardInstanceId, gameEvent);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[CRITICAL ERROR] {ex.Message}");
                }
            }

            // Sukces - czyścimy flagę oczekiwania
            return workingState.With(clearPending: true);
        }

        private bool IsPlayerSelectionRequired(TargetType targetType)
        {
            return targetType == TargetType.SelectedTarget
                || targetType == TargetType.TargetEnemyUnit
                || targetType == TargetType.TargetFriendlyUnit;
        }
    }
}