using CardGame.Core.Events.Interfaces;
using CardGame.Core.State.Models;

namespace CardGame.Core.Events
{
    #region Interaction Events

    /// <summary>
    /// Represents an event that occurs when player interaction is required to proceed with the game.
    /// </summary>
    public class InteractionRequiredEvent : IGameEvent
    {
        /// <summary>
        /// Gets the pending interaction data that requires player input.
        /// </summary>
        public PendingInteraction Interaction { get; }

        /// <summary>
        /// Gets the ID of the player who caused the event (0 indicates a system event).
        /// </summary>
        public int SourcePlayerId => 0;

        /// <summary>
        /// Initializes a new instance of the InteractionRequiredEvent class.
        /// </summary>
        /// <param name="interaction">The pending interaction data.</param>
        public InteractionRequiredEvent(PendingInteraction interaction)
        {
            Interaction = interaction;
        }
    }

    #endregion
}