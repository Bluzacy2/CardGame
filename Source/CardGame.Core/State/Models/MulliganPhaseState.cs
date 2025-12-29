using CardGame.Core.Application;
using CardGame.Core.Commands.Implementations;
using CardGame.Core.Commands.Interfaces;
using CardGame.Core.Events;
using CardGame.Core.State.Enums;
using CardGame.Core.State.Models;
using CardGame.Core.StateMachine.Interfaces;

namespace CardGame.Core.StateMachine.Phases
{
    public class MulliganPhaseState : IPhaseState
    {
        public GamePhase PhaseType => GamePhase.Mulligan;
        public bool IsCommandAllowed(IGameCommand command, GameState state) => command is CardGame.Core.Commands.Implementations.ConfirmMulliganCommand;
        public bool ShouldEndPhaseAutomatically(GameState state) => state.PlayersReady.Contains(1) && state.PlayersReady.Contains(2);

        public GameState ProcessEndPhase(GameState currentState, EventBus eventBus, GameContext context)
        {
            // Start Rundy 1: Obaj gracze dobierają kartę
            var pA = currentState.PlayerA.WithCardDrawn(eventBus);
            var pB = currentState.PlayerB.WithCardDrawn(eventBus);

            return currentState.With(
                currentPhase: GamePhase.UnitOnly,
                activePlayerId: 1,
                roundStartingPlayerId: 1,
                playerA: pA,
                playerB: pB
            );
        }
    }
}