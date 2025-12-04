using CardGame.Core.Events.Interfaces;
using CardGame.Core.State.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CardGame.Core.Cards.Component
{
    public interface ICardComponent
    {
        bool ShouldTrigger(IGameEvent gameEvent, GameState state);
        GameState Resolve(IGameEvent gameEvent, GameState gameState);
    }
}
