using CardGame.Core.Cards.Models;
using CardGame.Core.Events.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
// Note: Należy później rozdzielić poszczególne eventy do osobnych plików.
namespace CardGame.Core.Events
{
    public class GameEvents
    {
    }
    public class CardPlayedEvent: IGameEvent 
    {
        public int PlayerId { get; }
        public CardInstance Card { get; }
        public int LineIndex { get; }

        public CardPlayedEvent(int playerId, CardInstance card)
        {
            PlayerId = playerId;
            Card = card;
        }
    }
    public class UnitDamagedEvent : IGameEvent 
    {
        public CardInstance Unit { get; }
        public int Amount { get; }
        public CardInstance? Source { get; } /*Sprawdzenie z jakiego źródła ono pochodzi. - B. */
        public UnitDamagedEvent(CardInstance unit, int amount, CardInstance? source)
        {
            Unit = unit;
            Amount = amount;
            Source = source;
        }
    }

    public class  UnitDestroyedEvent: IGameEvent
    {
        public CardInstance Unit { get; }
        public int OwnerId { get; }
        public UnitDestroyedEvent(CardInstance unit, int ownerId)
        {
            Unit = unit;
            OwnerId = ownerId;
        }
    }

    public class  TurnStartedEvent: IGameEvent
    {
        public int TurnNumer { get; }
        public int ActivePlayerId { get; }
        public TurnStartedEvent(int turnNumer, int activePlayerId)
        {  
            TurnNumer = turnNumer;
            ActivePlayerId = activePlayerId;
        }

    }
    public class UnitDiedEvent : IGameEvent
    {
        public CardInstance Unit { get; }
        public int OwnerId { get; }
        public UnitDiedEvent(CardInstance unit) { Unit = unit; OwnerId = unit.OwnerPlayerId; }
    }
}
