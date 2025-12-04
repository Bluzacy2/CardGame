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
        private readonly Queue<IGameEvent> _eventQueue = new Queue<IGameEvent>();

        public void Publish(IGameEvent gameEvent)
        {
            _eventQueue.Enqueue(gameEvent);
        }
        public bool HasEvents => _eventQueue.Count > 0;

        public IGameEvent Pop()
        {
            return _eventQueue.Dequeue();
        }
        public void Clear()
        {
            _eventQueue.Clear();
        }
    }
}
