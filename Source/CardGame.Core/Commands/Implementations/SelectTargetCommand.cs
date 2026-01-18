using System;
using System.Linq;
using CardGame.Core.Application;
using CardGame.Core.Cards.Components.Implementations;
using CardGame.Core.Commands.Interfaces;
using CardGame.Core.Events;
using CardGame.Core.State.Models;

namespace CardGame.Core.Commands.Implementations
{
    /// <summary>
    /// Command to select a target for a pending interaction (e.g., spell targeting or unit ability).
    /// </summary>
    public class SelectTargetCommand : IGameCommand
    {
        #region Properties
        /// <summary>
        /// Gets the ID of the player making the target selection.
        /// </summary>
        public int PlayerId { get; }

        /// <summary>
        /// Gets the ID of the selected target (unit, player, or other entity).
        /// </summary>
        public int TargetId { get; }
        #endregion

        #region Constructor
        /// <summary>
        /// Initializes a new instance of the SelectTargetCommand class.
        /// </summary>
        /// <param name="playerId">The ID of the player making the selection.</param>
        /// <param name="targetId">The ID of the selected target.</param>
        public SelectTargetCommand(int playerId, int targetId)
        {
            PlayerId = playerId;
            TargetId = targetId;
        }
        #endregion

        #region Command Execution
        /// <summary>
        /// Executes the target selection, resolves the pending interaction, and applies the associated effect.
        /// </summary>
        /// <param name="currentState">The current game state with a pending interaction.</param>
        /// <param name="eventBus">The event bus for publishing game events.</param>
        /// <param name="context">The game context with additional services.</param>
        /// <returns>The updated game state after resolving the target selection.</returns>
        public GameState Execute(GameState currentState, EventBus eventBus, GameContext context)
        {
            var pendingInteraction = currentState.PendingInteraction;
            if (pendingInteraction == null || PlayerId != pendingInteraction.PlayerIdWhoChooses)
            {
                return currentState;
            }

            var sourceCard = currentState.SpellStack.FirstOrDefault(card => card.InstanceId == pendingInteraction.SourceCardInstanceId)
                          ?? currentState.Board.GetAllUnits().FirstOrDefault(unit => unit.InstanceId == pendingInteraction.SourceCardInstanceId)
                          ?? currentState.PlayerA.Hand.FirstOrDefault(card => card.InstanceId == pendingInteraction.SourceCardInstanceId)
                          ?? currentState.PlayerB.Hand.FirstOrDefault(card => card.InstanceId == pendingInteraction.SourceCardInstanceId)
                          ?? currentState.PlayerA.DiscardPile.FirstOrDefault(card => card.InstanceId == pendingInteraction.SourceCardInstanceId)
                          ?? currentState.PlayerB.DiscardPile.FirstOrDefault(card => card.InstanceId == pendingInteraction.SourceCardInstanceId);

            if (sourceCard == null) return currentState.With(clearPending: true);

            int effectCount = sourceCard.Definition.Effects.Count;
            if (effectCount == 0) return currentState.With(clearPending: true);

            int safeIndex = Math.Clamp(pendingInteraction.EffectIndex, 0, effectCount - 1);

            var effectData = sourceCard.Definition.Effects[safeIndex];
            var selectionEvent = new TargetSelectedEvent(PlayerId, pendingInteraction.SourceCardInstanceId, TargetId);
            var component = new JsonEffectComponent(effectData, sourceCard.InstanceId, pendingInteraction.EffectIndex);

            return (pendingInteraction.ActionIndex == -1)
                ? component.Resolve(selectionEvent, currentState, context)
                : component.ResolveFromIndex(selectionEvent, currentState, context, pendingInteraction.ActionIndex);
        }
        #endregion
    }
}