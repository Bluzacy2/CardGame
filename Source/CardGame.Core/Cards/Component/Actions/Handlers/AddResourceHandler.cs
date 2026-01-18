using CardGame.Core.Application;
using CardGame.Core.Cards.Component.Actions;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Logic;
using CardGame.Core.Events;
using CardGame.Core.Events.Interfaces;
using CardGame.Core.State.Models;

namespace CardGame.Core.Cards.Components.Actions.Handlers
{
    #region Resource Management Handlers

    /// <summary>
    /// Handles the AddResource action, increasing a player's available resources.
    /// </summary>
    public class AddResourceHandler : IActionHandler
    {
        /// <summary>
        /// Gets the action type this handler processes.
        /// </summary>
        public ActionType Type => ActionType.AddResource;

        /// <summary>
        /// Executes the AddResource action, adding resources to the target player.
        /// </summary>
        public GameState Execute(
            GameState state,
            GameContext context,
            ActionData action,
            EffectTargets targets,
            int sourceId,
            IGameEvent gameEvent)
        {
            if (targets.TargetPlayer != null)
            {
                var player = state.GetPlayer(targets.TargetPlayer.PlayerId);
                int oldBlood = player.CurrentBlood;
                int amount = action.Amount > 0 ? action.Amount : 1;
                var newPlayer = player.With(currentBlood: player.CurrentBlood + amount);

                context.Events.Publish(new ResourceChangedEvent(
                    newPlayer.PlayerId,
                    oldBlood,
                    newPlayer.CurrentBlood));

                return state.UpdatePlayer(newPlayer);
            }

            return state;
        }
    }

    #endregion
}