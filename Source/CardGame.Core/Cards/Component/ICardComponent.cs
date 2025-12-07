using System;
using CardGame.Core.Application;
using CardGame.Core.State.Models;
using CardGame.Core.Cards.Factories;
using CardGame.Core.Events.Interfaces;


namespace CardGame.Core.Cards.Component
{
    public interface ICardComponent
    {
        bool ShouldTrigger(IGameEvent gameEvent, GameState state);
        GameState Resolve(IGameEvent gameEvent, GameState currentState, GameContext context);
    }
}
