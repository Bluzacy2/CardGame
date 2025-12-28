using CardGame.Core.Application;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Logic;
using CardGame.Core.Events;
using CardGame.Core.Events.Interfaces;
using CardGame.Core.State.Models;
using System.Linq;

namespace CardGame.Core.Cards.Components.Implementations
{
    public class JsonEffectComponent
    {
        private readonly EffectData _data;
        private readonly int _sourceId;
        private readonly int _effectIndex;

        public JsonEffectComponent(EffectData data, int sourceId, int effectIndex = 0)
        {
            _data = data; _sourceId = sourceId; _effectIndex = effectIndex;
        }

        public bool ShouldTrigger(IGameEvent e, GameState s) => TriggerLogic.Check(_data, e, s, _sourceId);

        public GameState Resolve(IGameEvent evt, GameState state, GameContext context)
        {
            if (_data.Targeting == TargetType.Choice && !(evt is TargetSelectedEvent tse && state.PendingInteraction?.ActionIndex == -1))
            {
                return state.With(pendingInteraction: new PendingInteraction(_sourceId, _effectIndex, -1, TargetType.Choice, _data.ChoiceLabels));
            }
            return ResolveFromIndex(evt, state, context, 0);
        }

        public GameState ResolveFromIndex(IGameEvent evt, GameState state, GameContext context, int startIndex)
        {
            GameState workingState = state.With(clearPending: true);

            // Obsługa Choice (wybór ścieżki efektu)
            if (_data.Targeting == TargetType.Choice && evt is TargetSelectedEvent tse && state.PendingInteraction?.ActionIndex == -1)
            {
                int choiceIdx = tse.SelectedTargetId;
                if (choiceIdx >= 0 && choiceIdx < _data.Actions.Count)
                    return ExecuteAction(workingState, context, _data.Actions[choiceIdx], evt, choiceIdx);
                return workingState;
            }

            for (int i = startIndex; i < _data.Actions.Count; i++)
            {
                var nextState = ExecuteAction(workingState, context, _data.Actions[i], evt, i);
                if (nextState.PendingInteraction != null) return nextState;
                workingState = nextState;
            }
            return workingState;
        }

        private GameState ExecuteAction(GameState state, GameContext context, ActionData action, IGameEvent evt, int actionIdx)
        {
            var targetType = (action.Target == TargetType.Self && _data.Targeting != TargetType.Self) ? _data.Targeting : action.Target;

            if (IsManualTarget(targetType))
            {
                var resolved = EffectTargetResolver.Resolve(targetType, state, evt, _sourceId);

                // SPRAWDZENIE: Czy ten konkretny cel został już dostarczony?
                bool targetAlreadyProvided = false;

                // A. Przez komendę zagrania (tylko dla pierwszej akcji)
                if (actionIdx == 0 && evt is CardPlayedEvent cpe && cpe.SelectedTargetId.HasValue)
                    targetAlreadyProvided = true;

                // B. Przez wznowienie interakcji (tylko jeśli ID akcji się zgadza)
                if (evt is TargetSelectedEvent tse && state.PendingInteraction?.ActionIndex == actionIdx)
                    targetAlreadyProvided = true;

                if (!resolved.UnitTargets.Any() && !targetAlreadyProvided)
                {
                    if (EffectTargetResolver.GetPotentialTargets(targetType, state, _sourceId).Any())
                        return state.With(pendingInteraction: new PendingInteraction(_sourceId, _effectIndex, actionIdx, targetType));
                    return state;
                }
                return context.ActionRegistry.GetHandler(action.Type).Execute(state, context, action, resolved, _sourceId, evt);
            }

            var autoTargets = EffectTargetResolver.Resolve(targetType, state, evt, _sourceId);
            return context.ActionRegistry.GetHandler(action.Type).Execute(state, context, action, autoTargets, _sourceId, evt);
        }

        private bool IsManualTarget(TargetType t) => t == TargetType.SelectedTarget || t == TargetType.TargetEnemyUnit || t == TargetType.TargetFriendlyUnit;
    }
}