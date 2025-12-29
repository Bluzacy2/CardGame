using CardGame.Core.Application;
using CardGame.Core.Commands.Implementations;
using CardGame.Core.Commands.Interfaces;
using CardGame.Core.Events;
using CardGame.Core.State.Enums;
using CardGame.Core.State.Models;
using CardGame.Core.StateMachine.Interfaces;

namespace CardGame.Core.StateMachine.Phases
{
    public class UnitPhaseState : IPhaseState
    {
        public GamePhase PhaseType => GamePhase.UnitOnly;

        public bool IsCommandAllowed(IGameCommand command, GameState state)
        {
            if (command is SelectTargetCommand) return true;

            return command.PlayerId == state.ActivePlayerId &&
                   (command is PlayUnitCommand || command is EndPhaseCommand);
        }

        public bool ShouldEndPhaseAutomatically(GameState state) => false;

        public GameState ProcessEndPhase(GameState currentState, EventBus eventBus, GameContext context)
        {
            int opponentId = 3 - currentState.ActivePlayerId;
            return currentState.With(
                currentPhase: GamePhase.UnitAndAction,
                activePlayerId: opponentId
            );
        }
    }
}