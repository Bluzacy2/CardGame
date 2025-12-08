using CardGame.Core.Events.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CardGame.Core.Events
{
    public class EventBus
    {
        private readonly Queue<IGameEvent> _processingQueue = new Queue<IGameEvent>();
        private readonly List<IGameEvent> _history = new List<IGameEvent>();

        public void Publish(IGameEvent gameEvent)
        {
            _processingQueue.Enqueue(gameEvent);
            _history.Add(gameEvent);
        }
        public bool HasEvents => _processingQueue.Count > 0;

        public IGameEvent Pop()
        {
            return _processingQueue.Dequeue();
        }
        public void ClearHistory()
        {
            _history.Clear();
        }
        public IEnumerable<IGameEvent> GetHistory()
        {
            return _history.AsReadOnly();
        }
    }
}
