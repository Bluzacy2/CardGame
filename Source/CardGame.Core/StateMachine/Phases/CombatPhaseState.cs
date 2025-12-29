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
    public class CombatPhaseState : CardGame.Core.StateMachine.Interfaces.IPhaseState
    {
        public CardGame.Core.State.Enums.GamePhase PhaseType => CardGame.Core.State.Enums.GamePhase.Combat;
        public bool IsCommandAllowed(CardGame.Core.Commands.Interfaces.IGameCommand command, CardGame.Core.State.Models.GameState state) => command is CardGame.Core.Commands.Implementations.EndPhaseCommand;
        public bool ShouldEndPhaseAutomatically(CardGame.Core.State.Models.GameState state) => true;

        public CardGame.Core.State.Models.GameState ProcessEndPhase(CardGame.Core.State.Models.GameState currentState, CardGame.Core.Events.EventBus eventBus, CardGame.Core.Application.GameContext context)
        {
            var orchestrator = new CardGame.Core.Combat.CombatOrchestrator();
            var stateAfterCombat = orchestrator.ResolveCombatPhase(currentState, eventBus, context);

            // Logika statusów (Burning itp.)
            var workingState = stateAfterCombat;
            foreach (var unit in workingState.Board.GetAllUnits())
                workingState = context.Keywords.ProcessRoundEnd(workingState, unit, context);
            workingState = new CardGame.Core.GameRules.Death.DeathResolver().ResolveDeaths(workingState, eventBus, context);

            // Nowa runda
            int nextRoundStarter = 3 - currentState.RoundStartingPlayerId;
            int nextTurnNumber = currentState.TurnNumber + 1;
            int manaLimit = (nextTurnNumber + 1) / 2;

            // TU I TYLKO TU: Obaj dobierają karty i Starter dostaje refill many
            var pA = workingState.PlayerA.WithTurnStartBlood(manaLimit, nextRoundStarter == 1).WithCardDrawn(eventBus);
            var pB = workingState.PlayerB.WithTurnStartBlood(manaLimit, nextRoundStarter == 2).WithCardDrawn(eventBus);

            return workingState.With(
                turnNumber: nextTurnNumber,
                currentPhase: CardGame.Core.State.Enums.GamePhase.UnitOnly,
                activePlayerId: nextRoundStarter,
                roundStartingPlayerId: nextRoundStarter,
                playerA: pA,
                playerB: pB
            );
        }
    }
}