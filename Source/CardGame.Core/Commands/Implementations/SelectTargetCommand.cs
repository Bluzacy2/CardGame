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

        // Context jest teraz legalnie dostępny!
        public GameState Execute(GameState currentState, EventBus eventBus, GameContext context)
        {
            var pending = currentState.PendingInteraction;
            if (pending == null)
                throw new InvalidOperationException("Gra nie oczekuje na wybór celu!");

            // 1. Znajdź kartę źródłową
            var sourceCard = currentState.Board.GetAllUnits().FirstOrDefault(u => u.InstanceId == pending.SourceCardInstanceId)
                          ?? currentState.PlayerA.Hand.FirstOrDefault(u => u.InstanceId == pending.SourceCardInstanceId)
                          ?? currentState.PlayerB.Hand.FirstOrDefault(u => u.InstanceId == pending.SourceCardInstanceId)
                          // Ważne: szukamy też w cmentarzu, bo efekt może być "OnDeath"
                          ?? currentState.PlayerA.DiscardPile.FirstOrDefault(u => u.InstanceId == pending.SourceCardInstanceId)
                          ?? currentState.PlayerB.DiscardPile.FirstOrDefault(u => u.InstanceId == pending.SourceCardInstanceId);

            if (sourceCard == null)
                throw new Exception($"Nie znaleziono karty źródłowej (ID: {pending.SourceCardInstanceId}).");

            // 2. Pobierz konkretny efekt z definicji
            if (pending.EffectIndex >= sourceCard.Definition.Effects.Count)
                throw new Exception("Indeks efektu z pending interaction jest poza zakresem.");

            var effectData = sourceCard.Definition.Effects[pending.EffectIndex];

            // 3. Stwórz Event kontekstowy (to on niesie targetId do Resolvera)
            var selectionEvent = new TargetSelectedEvent(PlayerId, pending.SourceCardInstanceId, TargetId);

            // 4. Wznów wykonywanie
            var component = new JsonEffectComponent(effectData, sourceCard.InstanceId, pending.EffectIndex);

            // Wznawiamy od Pending.ActionIndex
            // Context przekazujemy legalnie
            var newState = component.ResolveFromIndex(selectionEvent, currentState, context, pending.ActionIndex);

            return newState;
        }
    }
}