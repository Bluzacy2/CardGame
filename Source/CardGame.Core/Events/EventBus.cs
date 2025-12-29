using CardGame.Core.Events.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CardGame.Core.Events
{
    public class EventBus
    {
        private readonly Queue<IGameEvent> _processingQueue = new Queue<IGameEvent>();

        // Historia tylko dla obecnej komendy/klatki (używana przez DeathResolver)
        private readonly List<IGameEvent> _commandHistory = new List<IGameEvent>();

        // Cała historia gry od początku (używana przez UI/Runnera)
        private readonly List<IGameEvent> _globalHistory = new List<IGameEvent>();

        public void Publish(IGameEvent gameEvent)
        {
            _processingQueue.Enqueue(gameEvent);
            _commandHistory.Add(gameEvent);
            _globalHistory.Add(gameEvent);
        }

        public bool HasEvents => _processingQueue.Count > 0;

        public IGameEvent Pop()
        {
            return _processingQueue.Dequeue();
        }

        // Metoda dla silnika - czyści tylko krótką historię
        public void ClearHistory()
        {
            _commandHistory.Clear();
        }

        // Metoda dla DeathResolvera i Triggerów (krótka historia)
        public IEnumerable<IGameEvent> GetHistory()
        {
            return _commandHistory.AsReadOnly();
        }

        // Metoda dla Runnera/UI (cała historia gry)
        public IEnumerable<IGameEvent> GetGlobalHistory()
        {
            return _globalHistory.AsReadOnly();
        }
    }
}