using CardGame.Core.Application;
using CardGame.Core.Commands.Implementations;
using CardGame.Core.Commands.Interfaces;
using CardGame.Core.Events;
using CardGame.Core.State.Enums;
using CardGame.Core.State.Models;
using CardGame.Core.StateMachine.Interfaces;

namespace CardGame.Core.StateMachine.Phases
{
    public class BothPhaseState : IPhaseState
    {
        public GamePhase PhaseType => GamePhase.UnitAndAction;

        public bool IsCommandAllowed(IGameCommand command, GameState state)
        {
     
            if (command is SelectTargetCommand) return true;

        
            return command.PlayerId == state.ActivePlayerId &&
                   (command is PlayUnitCommand ||
                    command is PlaySpellCommand ||
                    command is EndPhaseCommand);
        }

        public bool ShouldEndPhaseAutomatically(GameState state) => false;

        public GameState ProcessEndPhase(GameState currentState, EventBus eventBus, GameContext context)
        {
            return currentState.With(
                currentPhase: GamePhase.ActionOnly,
                activePlayerId: currentState.RoundStartingPlayerId
            );
        }
    }
}