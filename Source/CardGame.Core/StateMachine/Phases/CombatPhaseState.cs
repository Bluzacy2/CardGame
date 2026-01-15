using CardGame.Core.Application;
using CardGame.Core.Combat;
using CardGame.Core.Commands.Interfaces;
using CardGame.Core.Events;
using CardGame.Core.GameRules.Death;
using CardGame.Core.State.Enums;
using CardGame.Core.State.Models;
using CardGame.Core.StateMachine.Interfaces;

namespace CardGame.Core.StateMachine.Phases
{
    public class CombatPhaseState : IPhaseState
    {
        public GamePhase PhaseType => GamePhase.Combat;
        public bool IsCommandAllowed(IGameCommand command, GameState state) => command is CardGame.Core.Commands.Implementations.EndPhaseCommand;
        public bool ShouldEndPhaseAutomatically(GameState state) => true;

        public GameState ProcessEndPhase(GameState currentState, EventBus eventBus, GameContext context)
        {
            var orchestrator = new CombatOrchestrator();
            var stateAfterCombat = orchestrator.ResolveCombatPhase(currentState, eventBus, context);

            var workingState = stateAfterCombat;
            foreach (var unit in workingState.Board.GetAllUnits())
                workingState = context.Keywords.ProcessRoundEnd(workingState, unit, context);

            workingState = new DeathResolver().ResolveDeaths(workingState, eventBus, context);

            int nextRoundStarter = 3 - currentState.RoundStartingPlayerId;
            int nextTurnNumber = currentState.TurnNumber + 1;


            int manaLimit = nextTurnNumber;

            int oldBloodA = workingState.PlayerA.CurrentBlood;
            var pA = workingState.PlayerA.WithTurnStartBlood(manaLimit, true).WithCardDrawn(eventBus);
            eventBus.Publish(new ResourceChangedEvent(pA.PlayerId, oldBloodA, pA.CurrentBlood));

            int oldBloodB = workingState.PlayerB.CurrentBlood;
            var pB = workingState.PlayerB.WithTurnStartBlood(manaLimit, true).WithCardDrawn(eventBus);
            eventBus.Publish(new ResourceChangedEvent(pB.PlayerId, oldBloodB, pB.CurrentBlood));

            return workingState.With(
                turnNumber: nextTurnNumber,
                currentPhase: GamePhase.UnitOnly,
                activePlayerId: nextRoundStarter,
                roundStartingPlayerId: nextRoundStarter,
                playerA: pA,
                playerB: pB
            );
        }
    }
}