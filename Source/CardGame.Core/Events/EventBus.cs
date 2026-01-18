using CardGame.Core.Events.Interfaces;
using System.Collections.Generic;

namespace CardGame.Core.Events
{
    /// <summary>
    /// Manages the publishing and processing of game events with both short-term and global history tracking.
    /// </summary>
    public class EventBus
    {
        private readonly Queue<IGameEvent> _processingQueue = new Queue<IGameEvent>();

        // Short history for current command/frame (used by DeathResolver)
        private readonly List<IGameEvent> _commandHistory = new List<IGameEvent>();

        // Complete game history from start (used by UI/Runner)
        private readonly List<IGameEvent> _globalHistory = new List<IGameEvent>();

        #region Event Publishing

        /// <summary>
        /// Publishes a game event to the processing queue and history.
        /// </summary>
        /// <param name="gameEvent">The event to publish.</param>
        public void Publish(IGameEvent gameEvent)
        {
            _processingQueue.Enqueue(gameEvent);
            _commandHistory.Add(gameEvent);
            _globalHistory.Add(gameEvent);
        }

        /// <summary>
        /// Gets a value indicating whether there are pending events in the processing queue.
        /// </summary>
        public bool HasEvents => _processingQueue.Count > 0;

        /// <summary>
        /// Removes and returns the next event from the processing queue.
        /// </summary>
        /// <returns>The next IGameEvent in the queue.</returns>
        public IGameEvent Pop()
        {
            return _processingQueue.Dequeue();
        }

        #endregion

        #region History Management

        /// <summary>
        /// Clears the short-term command history (used between command executions).
        /// </summary>
        public void ClearHistory()
        {
            _commandHistory.Clear();
        }

        /// <summary>
        /// Gets the short-term history of events for the current command/frame.
        /// </summary>
        /// <returns>A read-only collection of recent events.</returns>
        public IEnumerable<IGameEvent> GetHistory()
        {
            return _commandHistory.AsReadOnly();
        }

        /// <summary>
        /// Gets the complete history of all events from the start of the game.
        /// </summary>
        /// <returns>A read-only collection of all game events.</returns>
        public IEnumerable<IGameEvent> GetGlobalHistory()
        {
            return _globalHistory.AsReadOnly();
        }

        #endregion
    }
}