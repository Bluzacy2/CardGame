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
            _data = data;
            _sourceId = sourceId;
            _effectIndex = effectIndex;
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

            // Jeśli wracamy z interakcji wyboru (Choice)
            if (_data.Targeting == TargetType.Choice && evt is TargetSelectedEvent tse && state.PendingInteraction?.ActionIndex == -1)
            {
                int choiceIdx = tse.SelectedTargetId;
                if (choiceIdx >= 0 && choiceIdx < _data.Actions.Count)
                {
                    // Wykonujemy wybraną akcję używając zdarzenia wyboru jako kontekstu
                    return ExecuteAction(workingState, context, _data.Actions[choiceIdx], evt, choiceIdx);
                }
                return workingState;
            }

            for (int i = startIndex; i < _data.Actions.Count; i++)
            {
                workingState = ExecuteAction(workingState, context, _data.Actions[i], evt, i);
                if (workingState.PendingInteraction != null) return workingState;
            }
            return workingState;
        }

        private GameState ExecuteAction(GameState state, GameContext context, ActionData action, IGameEvent evt, int actionIdx)
        {
            var targetType = action.Target;
            if ((targetType == TargetType.SelectedTarget || targetType == TargetType.Self) && _data.Targeting != TargetType.Self)
            {
                targetType = _data.Targeting;
            }

            var resolved = EffectTargetResolver.Resolve(targetType, state, evt, _sourceId);

            // Jeśli cel został ustalony automatycznie (np. Hero lub Self)
            if (resolved.UnitTargets.Any() || resolved.TargetPlayer != null)
            {
                return context.ActionRegistry.GetHandler(action.Type).Execute(state.With(clearPending: true), context, action, resolved, _sourceId, evt);
            }

            // Celowanie manualne
            if (IsManualTarget(targetType))
            {
                var potential = EffectTargetResolver.GetPotentialTargets(targetType, state, _sourceId);

                if (potential.Count == 0) return state;

                if (potential.Count == 1)
                {
                    var autoEvent = new TargetSelectedEvent(evt.SourcePlayerId, _sourceId, potential[0].InstanceId);
                    var autoResolved = EffectTargetResolver.Resolve(targetType, state, autoEvent, _sourceId);
                    return context.ActionRegistry.GetHandler(action.Type).Execute(state.With(clearPending: true), context, action, autoResolved, _sourceId, evt);
                }

                return state.With(pendingInteraction: new PendingInteraction(_sourceId, _effectIndex, actionIdx, targetType));
            }

            return context.ActionRegistry.GetHandler(action.Type).Execute(state, context, action, resolved, _sourceId, evt);
        }

        private bool IsManualTarget(TargetType t) =>
            t == TargetType.SelectedTarget || t == TargetType.TargetEnemyUnit || t == TargetType.TargetFriendlyUnit || t == TargetType.OtherFriendlyUnits;
    }
}