using System.Collections.Generic;
using CardGame.Core.Commands.Interfaces;
using CardGame.Core.Events;
using CardGame.Core.State.Models;

namespace CardGame.Core.Commands.Implementations
{
    public class ConfirmMulliganCommand : IGameCommand
    {
        public int PlayerId { get; }
        public List<int> RejectedCardIds { get; } // Karty do wymiany

        public ConfirmMulliganCommand(int playerId, List<int> rejectedCardIds)
        {
            PlayerId = playerId;
            RejectedCardIds = rejectedCardIds ?? new List<int>();
        }

        public GameState Execute(GameState currentState, EventBus eventBus)
        {
            // 1. Pobierz gracza
            var player = currentState.GetPlayer(PlayerId);

            // 2. Wykonaj logikę wymiany
            var newPlayer = player.WithMulliganPerformed(RejectedCardIds);

            // 3. Oznacz gracza jako gotowego w stanie gry
            var newState = currentState
                .UpdatePlayer(newPlayer)
                .MarkPlayerAsReady(PlayerId);

            // Możemy wygenerować event (opcjonalnie)
            // eventBus.Publish(new MulliganCompletedEvent(PlayerId));

            return newState;
        }
    }
}