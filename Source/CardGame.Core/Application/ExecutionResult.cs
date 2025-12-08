using System.Collections.Generic;
using CardGame.Core.Events.Interfaces;
using CardGame.Core.State.Models;

namespace CardGame.Core.Application
{
    public class ExecutionResult
    {
        public GameState NewState { get; }
        public IReadOnlyList<IGameEvent> EventsHappened { get; }

        public ExecutionResult(GameState newState, IEnumerable<IGameEvent> events)
        {
            NewState = newState;
            EventsHappened = new List<IGameEvent>(events);
        }
    }
}