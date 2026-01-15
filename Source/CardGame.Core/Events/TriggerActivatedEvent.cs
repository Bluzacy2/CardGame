using CardGame.Core.Events.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CardGame.Core.Events
{
    public class TriggerActivatedEvent : IGameEvent
    {
        public int SourceCardId { get; }
        public int SourcePlayerId { get; }

        public TriggerActivatedEvent(int sourceCardId, int sourcePlayerId)
        {
            SourceCardId = sourceCardId;
            SourcePlayerId = sourcePlayerId;
        }
    }
}
