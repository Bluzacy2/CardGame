using CardGame.Core.Application;
using CardGame.Core.Cards.Component.Actions;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Logic;
using CardGame.Core.Events.Interfaces;
using CardGame.Core.State.Models;
using System;

namespace CardGame.Core.Cards.Components.Actions.Handlers
{
    #region Card Draw Handlers

    /// <summary>
    /// Handles the DrawCard action, allowing players to draw cards from their deck.
    /// </summary>
    public class DrawCardHandler : IActionHandler
    {
        /// <summary>
        /// Gets the action type this handler processes.
        /// </summary>
        public ActionType Type => ActionType.DrawCard;

        /// <summary>
        /// Executes the DrawCard action, drawing a specified number of cards from the player's deck.
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
                var workingPlayer = targets.TargetPlayer;
                int amount = action.Amount > 0 ? action.Amount : 1;

                // Here you could add logic for Fatigue (damage for having no cards in deck)
                for (int i = 0; i < amount; i++)
                {
                    workingPlayer = workingPlayer.WithCardDrawn(context.Events);
                }

                return state.UpdatePlayer(workingPlayer);
            }

            return state;
        }
    }

    #endregion
}