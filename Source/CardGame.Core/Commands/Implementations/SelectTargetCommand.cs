using System;
using System.Linq;
using CardGame.Core.Application;
using CardGame.Core.Cards.Components.Implementations;
using CardGame.Core.Commands.Interfaces;
using CardGame.Core.Events;
using CardGame.Core.State.Models;

namespace CardGame.Core.Commands.Implementations
{
    public class SelectTargetCommand : IGameCommand
    {
        public int PlayerId { get; }
        public int TargetId { get; }

        public SelectTargetCommand(int playerId, int targetId)
        {
            PlayerId = playerId;
            TargetId = targetId;
        }

        public GameState Execute(GameState currentState, EventBus eventBus, GameContext context)
        {
            var pending = currentState.PendingInteraction;
            if (pending == null) throw new InvalidOperationException("Gra nie oczekuje na wybór celu!");

            // ZMIANA: Szukamy źródła na stosie (SpellStack) zamiast w ActiveSpell
            var sourceCard = currentState.SpellStack.FirstOrDefault(s => s.InstanceId == pending.SourceCardInstanceId)
                          ?? currentState.Board.GetAllUnits().FirstOrDefault(u => u.InstanceId == pending.SourceCardInstanceId)
                          ?? currentState.PlayerA.Hand.FirstOrDefault(u => u.InstanceId == pending.SourceCardInstanceId)
                          ?? currentState.PlayerB.Hand.FirstOrDefault(u => u.InstanceId == pending.SourceCardInstanceId)
                          ?? currentState.PlayerA.DiscardPile.FirstOrDefault(u => u.InstanceId == pending.SourceCardInstanceId)
                          ?? currentState.PlayerB.DiscardPile.FirstOrDefault(u => u.InstanceId == pending.SourceCardInstanceId);

            if (sourceCard == null) throw new Exception("Nie znaleziono źródła efektu.");

            var effectData = sourceCard.Definition.Effects[pending.EffectIndex];
            var selectionEvent = new TargetSelectedEvent(PlayerId, pending.SourceCardInstanceId, TargetId);
            var component = new JsonEffectComponent(effectData, sourceCard.InstanceId, pending.EffectIndex);

            return (pending.ActionIndex == -1)
                ? component.Resolve(selectionEvent, currentState, context)
                : component.ResolveFromIndex(selectionEvent, currentState, context, pending.ActionIndex);
        }
    }
}