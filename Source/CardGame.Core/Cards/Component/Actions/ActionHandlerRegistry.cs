using CardGame.Core.Cards.Component.Actions;
using CardGame.Core.Cards.Data;
using System;
using System.Collections.Generic;

namespace CardGame.Core.Cards.Components.Actions
{
    /// <summary>
    /// Registry for managing and retrieving action handlers for card effects.
    /// </summary>
    public class ActionHandlerRegistry
    {
        private readonly Dictionary<ActionType, IActionHandler> _handlers = new Dictionary<ActionType, IActionHandler>();

        #region Handler Management

        /// <summary>
        /// Registers an action handler for a specific action type.
        /// </summary>
        /// <param name="handler">The handler to register.</param>
        /// <exception cref="Exception">Thrown when a handler for the action type is already registered.</exception>
        public void Register(IActionHandler handler)
        {
            if (_handlers.ContainsKey(handler.Type))
            {
                throw new Exception($"Handler for action {handler.Type} is already registered!");
            }

            _handlers[handler.Type] = handler;
        }

        /// <summary>
        /// Retrieves the handler for a specific action type.
        /// </summary>
        /// <param name="type">The type of action.</param>
        /// <returns>The registered IActionHandler for the specified type.</returns>
        /// <exception cref="Exception">Thrown when no handler is registered for the action type.</exception>
        public IActionHandler GetHandler(ActionType type)
        {
            if (_handlers.TryGetValue(type, out var handler))
            {
                return handler;
            }

            throw new Exception($"No registered handler for action: {type}");
        }

        #endregion
    }
}