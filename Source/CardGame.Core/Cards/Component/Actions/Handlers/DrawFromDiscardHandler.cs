using CardGame.Core.Application;
using CardGame.Core.Cards.Component.Actions;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Logic;
using CardGame.Core.Events.Interfaces;
using CardGame.Core.State.Models;

namespace CardGame.Core.Cards.Components.Actions.Handlers
{
    #region Card Draw Handlers

    /// <summary>
    /// Handles the DrawFromDiscard action, allowing players to draw cards from their discard pile.
    /// </summary>
    public class DrawFromDiscardHandler : IActionHandler
    {
        /// <summary>
        /// Gets the action type this handler processes.
        /// </summary>
        public ActionType Type => ActionType.DrawFromDiscard;

        /// <summary>
        /// Executes the DrawFromDiscard action, moving cards from the discard pile to the player's hand.
        /// </summary>
        public GameState Execute(
            GameState state,
            GameContext context,
            ActionData action,
            EffectTargets targets,
            int sourceId,
            IGameEvent gameEvent)
        {
            if (targets.TargetPlayer == null)
            {
                return state;
            }

            var newPlayer = targets.TargetPlayer.WithCardsDrawnFromDiscard(action.Amount, context.Events);
            return state.UpdatePlayer(newPlayer);
        }
    }

    #endregion
}