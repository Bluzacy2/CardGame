using CardGame.Core.Application;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Logic;
using CardGame.Core.Events;
using CardGame.Core.Events.Interfaces;
using CardGame.Core.State.Models;
using System.Linq;

namespace CardGame.Core.Cards.Components.Implementations
{
    /// <summary>
    /// Component that handles the execution of card effects defined in JSON data.
    /// </summary>
    public class JsonEffectComponent
    {
        private readonly EffectData _data;
        private readonly int _sourceId;
        private readonly int _effectIndex;

        /// <summary>
        /// Initializes a new instance of the JsonEffectComponent class.
        /// </summary>
        /// <param name="data">The effect data definition.</param>
        /// <param name="sourceId">The ID of the card that owns this effect.</param>
        /// <param name="effectIndex">The index of this effect within the card's effect list.</param>
        public JsonEffectComponent(EffectData data, int sourceId, int effectIndex = 0)
        {
            _data = data;
            _sourceId = sourceId;
            _effectIndex = effectIndex;
        }

        #region Public Methods

        /// <summary>
        /// Determines whether this effect should trigger based on the current game event and state.
        /// </summary>
        /// <param name="e">The game event that occurred.</param>
        /// <param name="s">The current game state.</param>
        /// <returns>True if the effect should trigger, otherwise false.</returns>
        public bool ShouldTrigger(IGameEvent e, GameState s) => 
            TriggerLogic.Check(_data, e, s, _sourceId);

        /// <summary>
        /// Resolves the effect, potentially creating player interactions for choices.
        /// </summary>
        /// <param name="evt">The triggering game event.</param>
        /// <param name="state">The current game state.</param>
        /// <param name="context">The game context with access to services.</param>
        /// <returns>The updated game state after resolution.</returns>
        public GameState Resolve(IGameEvent evt, GameState state, GameContext context)
        {
            if (_data.Targeting == TargetType.Choice && 
                !(evt is TargetSelectedEvent tse && state.PendingInteraction?.ActionIndex == -1))
            {
                return state.With(pendingInteraction: new PendingInteraction(
                    _sourceId, _effectIndex, -1, TargetType.Choice, evt.SourcePlayerId, _data.ChoiceLabels));
            }
            
            return ResolveFromIndex(evt, state, context, 0);
        }

        /// <summary>
        /// Resolves the effect starting from a specific action index.
        /// </summary>
        /// <param name="evt">The triggering game event.</param>
        /// <param name="state">The current game state.</param>
        /// <param name="context">The game context with access to services.</param>
        /// <param name="startIndex">The index of the first action to execute.</param>
        /// <returns>The updated game state after partial resolution.</returns>
        public GameState ResolveFromIndex(IGameEvent evt, GameState state, GameContext context, int startIndex)
        {
            GameState workingState = state.With(clearPending: true);

            // 1. Initial Choice Selection (User picks the option)
            if (_data.Targeting == TargetType.Choice && evt is TargetSelectedEvent tse && state.PendingInteraction?.ActionIndex == -1)
            {
                int choiceIdx = tse.SelectedTargetId;
                if (choiceIdx >= 0 && choiceIdx < _data.Actions.Count)
                {
                    return ExecuteAction(workingState, context, _data.Actions[choiceIdx], evt, choiceIdx);
                }
                return workingState;
            }

            if (_data.Targeting == TargetType.Choice)
            {
                if (startIndex >= 0 && startIndex < _data.Actions.Count)
                {
                    return ExecuteAction(workingState, context, _data.Actions[startIndex], evt, startIndex);
                }
                return workingState;
            }
          
            for (int i = startIndex; i < _data.Actions.Count; i++)
            {
                var action = _data.Actions[i];
                var nextState = ExecuteAction(workingState, context, action, evt, i);

                if (nextState == workingState && IsTargetedAction(action))
                {
                    return workingState;
                }

                workingState = nextState;

                if (workingState.PendingInteraction != null)
                    return workingState;
            }

            return workingState;
        }

        private bool IsTargetedAction(ActionData action)
        {
            return action.Target == TargetType.TargetEnemyUnit ||
                   action.Target == TargetType.TargetFriendlyUnit ||
                   action.Target == TargetType.SelectedTarget ||
                   action.Target == TargetType.OtherFriendlyUnits ||
                   action.Type == ActionType.SacrificeUnit;
        }


        #endregion

        #region Private Methods

        /// <summary>
        /// Executes a single action within the effect.
        /// </summary>
        /// <param name="state">The current game state.</param>
        /// <param name="context">The game context with access to services.</param>
        /// <param name="action">The action data to execute.</param>
        /// <param name="evt">The triggering game event.</param>
        /// <param name="actionIdx">The index of this action within the effect.</param>
        /// <returns>The updated game state after action execution.</returns>
        private GameState ExecuteAction(GameState state, GameContext context, ActionData action, IGameEvent evt, int actionIdx)
        {
            int ownerId = EffectTargetResolver.GetOwner(state, _sourceId, evt);
            var targetType = action.Target;
            
            if ((targetType == TargetType.SelectedTarget || targetType == TargetType.Self) && 
                _data.Targeting != TargetType.Self)
            {
                targetType = _data.Targeting;
            }

            if (evt is TargetSelectedEvent && state.PendingInteraction?.ActionIndex == actionIdx)
            {
                var manualResolved = EffectTargetResolver.Resolve(targetType, state, evt, _sourceId);
                return context.ActionRegistry.GetHandler(action.Type).Execute(
                    state.With(clearPending: true), context, action, manualResolved, _sourceId, evt);
            }

            var resolved = EffectTargetResolver.Resolve(targetType, state, evt, _sourceId);

            if (resolved.UnitTargets.Any() || resolved.TargetPlayer != null)
            {
                return context.ActionRegistry.GetHandler(action.Type).Execute(
                    state.With(clearPending: true), context, action, resolved, _sourceId, evt);
            }

            if (IsManualTarget(targetType) || targetType == TargetType.TargetEnemyUnit || targetType == TargetType.TargetFriendlyUnit)
            {
                var potential = EffectTargetResolver.GetPotentialTargets(targetType, state, _sourceId);

                if (potential.Count == 0 && !resolved.UnitTargets.Any()) 
                {
                    return state;
                }

                if (potential.Count == 1)
                {
                    var autoEvent = new TargetSelectedEvent(evt.SourcePlayerId, _sourceId, potential[0].InstanceId);
                    var autoResolved = EffectTargetResolver.Resolve(targetType, state, autoEvent, _sourceId);
                    return context.ActionRegistry.GetHandler(action.Type).Execute(
                        state.With(clearPending: true), context, action, autoResolved, _sourceId, evt);
                }

                return state.With(pendingInteraction: new PendingInteraction(_sourceId, _effectIndex, actionIdx, targetType, ownerId));
            }

            return context.ActionRegistry.GetHandler(action.Type).Execute(
                state.With(clearPending: true), context, action, resolved, _sourceId, evt);
        }

        /// <summary>
        /// Determines if a target type requires manual player selection.
        /// </summary>
        /// <param name="t">The target type to check.</param>
        /// <returns>True if the target requires manual selection, otherwise false.</returns>
        private bool IsManualTarget(TargetType t) =>
            t == TargetType.SelectedTarget || 
            t == TargetType.TargetEnemyUnit || 
            t == TargetType.TargetFriendlyUnit || 
            t == TargetType.OtherFriendlyUnits;

        #endregion
    }
}