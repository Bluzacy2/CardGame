using CardGame.Core.Application;
using CardGame.Core.Cards.Logic;
using CardGame.Core.Commands.Interfaces;
using CardGame.Core.Events;
using CardGame.Core.State.Models;
using System;
using System.Linq;

namespace CardGame.Core.Commands.Implementations
{
    /// <summary>
    /// Command to play a spell card from the player's hand, moving it to the spell stack and paying its cost.
    /// </summary>
    public class PlaySpellCommand : IGameCommand
    {
        #region Properties
        /// <summary>
        /// Gets the ID of the player playing the spell.
        /// </summary>
        public int PlayerId { get; }

        /// <summary>
        /// Gets the instance ID of the spell card being played.
        /// </summary>
        public int CardInstanceId { get; }

        /// <summary>
        /// Gets the optional ID of the selected target for the spell.
        /// </summary>
        public int? SelectedTargetId { get; }
        #endregion

        #region Constructor
        /// <summary>
        /// Initializes a new instance of the PlaySpellCommand class.
        /// </summary>
        /// <param name="playerId">The ID of the player playing the spell.</param>
        /// <param name="cardInstanceId">The instance ID of the spell card being played.</param>
        /// <param name="selectedTargetId">Optional ID of the selected target for the spell.</param>
        public PlaySpellCommand(int playerId, int cardInstanceId, int? selectedTargetId = null)
        {
            PlayerId = playerId;
            CardInstanceId = cardInstanceId;
            SelectedTargetId = selectedTargetId;
        }
        #endregion

        #region Command Execution
        /// <summary>
        /// Executes the spell playing command, validating the play, paying costs, and moving the card to the spell stack.
        /// </summary>
        /// <param name="currentState">The current game state.</param>
        /// <param name="eventBus">The event bus for publishing game events.</param>
        /// <param name="context">The game context with additional services.</param>
        /// <returns>The updated game state after playing the spell.</returns>
        public GameState Execute(GameState currentState, EventBus eventBus, GameContext context)
        {
            var player = currentState.GetPlayer(PlayerId);
            int oldBlood = player.CurrentBlood;

            var card = player.Hand.FirstOrDefault(c => c.InstanceId == CardInstanceId);

            if (card == null) return currentState;

            if (!PlayValidator.CanPlay(card, currentState, PlayerId))
            {
                return currentState;
            }

            var newPlayer = player.WithBloodSpent(card.CurrentStats.BloodCost)
                                  .WithCardRemovedFromHand(card);

            eventBus.Publish(new ResourceChangedEvent(PlayerId, oldBlood, newPlayer.CurrentBlood));

            var newStack = currentState.SpellStack.ToList();
            newStack.Add(card);

            var newState = currentState.UpdatePlayer(newPlayer).With(spellStack: newStack);

            eventBus.Publish(new CardMovedEvent(card.InstanceId, PlayerId, CardZone.Hand, CardZone.Stack));
            eventBus.Publish(new CardPlayedEvent(PlayerId, card, null, SelectedTargetId));

            return newState;
        }
        #endregion
    }
}