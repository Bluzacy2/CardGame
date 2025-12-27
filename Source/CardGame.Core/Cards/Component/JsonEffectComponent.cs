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
        private readonly int _effectIndex;

        public JsonEffectComponent(EffectData effectData, int ownerCardInstanceId, int effectIndex = 0)
        {
            _effectData = effectData;
            _ownerCardInstanceId = ownerCardInstanceId;
            _effectIndex = effectIndex;
        }

        public bool ShouldTrigger(IGameEvent e, GameState s) => TriggerLogic.Check(_effectData.Trigger, e, s, _ownerCardInstanceId);

        public GameState Resolve(IGameEvent gameEvent, GameState currentState, GameContext context)
        {
            bool isChoiceResponse = (gameEvent is TargetSelectedEvent tse && currentState.PendingInteraction?.RequiredTargetType == TargetType.Choice);

            if (_effectData.Targeting == TargetType.Choice && !isChoiceResponse)
            {
                return currentState.With(pendingInteraction: new PendingInteraction(
                    _ownerCardInstanceId, _effectIndex, -1, TargetType.Choice, _effectData.ChoiceLabels));
            }

            return ResolveFromIndex(gameEvent, currentState, context, 0);
        }

        public GameState ResolveFromIndex(IGameEvent gameEvent, GameState currentState, GameContext context, int startActionIndex)
        {
            var workingState = currentState.With(clearPending: true);

            if (_effectData.Targeting == TargetType.Choice && gameEvent is TargetSelectedEvent tse && currentState.PendingInteraction?.ActionIndex == -1)
            {
                int choiceIdx = tse.SelectedTargetId;
                if (choiceIdx >= 0 && choiceIdx < _effectData.Actions.Count)
                {
                    // Po dokonaniu wyboru, sprawdzamy tę JEDNĄ konkretną akcję
                    return ExecuteSingleAction(workingState, context, _effectData.Actions[choiceIdx], gameEvent, choiceIdx, true);
                }
                return workingState;
            }

            for (int i = startActionIndex; i < _effectData.Actions.Count; i++)
            {
                workingState = ExecuteSingleAction(workingState, context, _effectData.Actions[i], gameEvent, i, false);
                if (workingState.PendingInteraction != null) return workingState;
            }
            return workingState;
        }

        private GameState ExecuteSingleAction(GameState state, GameContext context, ActionData action, IGameEvent gameEvent, int idx, bool wasChoice)
        {
            var targetType = action.Target == TargetType.Self ? _effectData.Targeting : action.Target;

            bool isTargeted = (targetType == TargetType.SelectedTarget ||
                               targetType == TargetType.TargetEnemyUnit ||
                               targetType == TargetType.TargetFriendlyUnit);

            bool hasValidSelection = false;

            // Jeśli akcja jest częścią wyboru Choice, to pierwotny TargetSelectedEvent 
            // służył do wybrania opcji, a NIE celu akcji. Musimy wymusić nową interakcję.
            if (!wasChoice)
            {
                hasValidSelection = (idx == 0 && gameEvent is CardPlayedEvent cpe && cpe.SelectedTargetId.HasValue) ||
                                   (gameEvent is TargetSelectedEvent ts && state.PendingInteraction == null);
            }

            if (isTargeted && !hasValidSelection)
            {
                return state.With(pendingInteraction: new PendingInteraction(_ownerCardInstanceId, _effectIndex, idx, targetType));
            }

            var targets = EffectTargetResolver.Resolve(targetType, state, gameEvent, _ownerCardInstanceId);
            var handler = context.ActionRegistry.GetHandler(action.Type);
            return handler.Execute(state, context, action, targets, _ownerCardInstanceId, gameEvent);
        }
    }
}