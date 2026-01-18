using CardGame.Core.Events.Interfaces;
using CardGame.Core.State.Models;
using System.Collections.Generic;

namespace CardGame.Core.Application
{
    /// <summary>
    /// Represents the result of executing a game action, including the updated state and generated events.
    /// </summary>
    public class ExecutionResult
    {
        /// <summary>
        /// Gets the game state after execution.
        /// </summary>
        public GameState NewState { get; }

        /// <summary>
        /// Gets the list of events that occurred during execution.
        /// </summary>
        public IReadOnlyList<IGameEvent> EventsHappened { get; }

        /// <summary>
        /// Initializes a new instance of the ExecutionResult class.
        /// </summary>
        /// <param name="newState">The game state after execution.</param>
        /// <param name="events">The events that occurred during execution.</param>
        public ExecutionResult(GameState newState, IEnumerable<IGameEvent> events)
        {
            NewState = newState;
            EventsHappened = new List<IGameEvent>(events);
        }
    }
}