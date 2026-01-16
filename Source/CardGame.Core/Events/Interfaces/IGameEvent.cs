using System;

namespace CardGame.Core.Events.Interfaces
{
    /// <summary>
    /// Defines the contract for all game events, providing information about the player who caused the event.
    /// </summary>
    public interface IGameEvent
    {
        /// <summary>
        /// Gets the ID of the player who caused or initiated the event.
        /// </summary>
        int SourcePlayerId { get; }
    }
}