using System;
using CardGame.Core.Events;
using CardGame.Core.Application;
using CardGame.Core.State.Models;
namespace CardGame.Core.Commands.Interfaces
{
    public interface IGameCommand
    {
       
        int PlayerId { get; }

       
        GameState Execute(GameState currentState, EventBus eventBus, GameContext context);
    }
}
