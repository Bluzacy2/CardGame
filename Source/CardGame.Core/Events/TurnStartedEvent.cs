using CardGame.Core.Events.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CardGame.Core.Events
{
    public class TurnStartedEvent : IGameEvent
    {
        public int TurnNumer { get; }
        public int ActivePlayerId { get; }
        public TurnStartedEvent(int turnNumer, int activePlayerId)
        {
            TurnNumer = turnNumer;
            ActivePlayerId = activePlayerId;
        }

    }
}
