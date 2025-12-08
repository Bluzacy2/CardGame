using CardGame.Core.Cards.Component.Actions;
using CardGame.Core.Cards.Data;
using System;
using System.Collections.Generic;

namespace CardGame.Core.Cards.Components.Actions
{
    public class ActionHandlerRegistry
    {
        private readonly Dictionary<ActionType, IActionHandler> _handlers = new();

        public void Register(IActionHandler handler)
        {
            if (_handlers.ContainsKey(handler.Type))
            {
                throw new Exception($"Handler dla akcji {handler.Type} jest już zarejestrowany!");
            }
            _handlers[handler.Type] = handler;
        }

        public IActionHandler GetHandler(ActionType type)
        {
            if (_handlers.TryGetValue(type, out var handler))
            {
                return handler;
            }
            throw new Exception($"Brak zarejestrowanego handlera dla akcji: {type}");
        }
    }
}