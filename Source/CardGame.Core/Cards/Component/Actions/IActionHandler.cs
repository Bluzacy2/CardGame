using System;
using CardGame.Core.Application;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Logic;
using CardGame.Core.State.Models;
using CardGame.Core.Events.Interfaces;

namespace CardGame.Core.Cards.Component.Actions
{
    public interface  IActionHandler
    {
        ActionType Type { get; }
        GameState Execute(
            GameState state, 
            GameContext context,
            ActionData actionData, 
            EffectTargets targets, 
            int sourceId,
            IGameEvent gameEvent
            );
    }
}
