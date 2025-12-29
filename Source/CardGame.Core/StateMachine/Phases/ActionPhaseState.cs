using CardGame.Core.Application;
using CardGame.Core.Commands.Implementations;
using CardGame.Core.Commands.Interfaces;
using CardGame.Core.Events;
using CardGame.Core.State.Enums;
using CardGame.Core.State.Models;
using CardGame.Core.StateMachine.Interfaces;

namespace CardGame.Core.StateMachine.Phases
{
    public class ActionPhaseState : IPhaseState
    {
        public GamePhase PhaseType => GamePhase.ActionOnly;

        public bool IsCommandAllowed(IGameCommand command, GameState state)
        {
       
            if (command is SelectTargetCommand) return true;
 
            return command.PlayerId == state.ActivePlayerId &&
                   (command is PlaySpellCommand ||
                    command is EndPhaseCommand);
        }

        public bool ShouldEndPhaseAutomatically(GameState state) => false;

        public GameState ProcessEndPhase(GameState currentState, EventBus eventBus, GameContext context)
        {
  
            return currentState.With(currentPhase: GamePhase.Combat);
        }
    }
}