using CardGame.Core.Application;
using CardGame.Core.Cards.Data;
using CardGame.Core.Combat;
using CardGame.Core.Commands.Implementations;
using CardGame.Core.Commands.Interfaces;
using CardGame.Core.Events;
using CardGame.Core.GameRules.Death;
using CardGame.Core.State.Enums;
using CardGame.Core.State.Models;
using CardGame.Core.StateMachine.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CardGame.Core.StateMachine.Phases
{
    public class CombatPhaseState: IPhaseState
    {
        public GamePhase PhaseType => GamePhase.Combat;
        public bool IsCommandAllowed(IGameCommand command, GameState state)
        {
         
            if (command is EndPhaseCommand) return true;
            return false;
        }

        public GameState ProcessEndPhase(GameState currentState, EventBus eventBus, GameContext context)
        {
            /* ---------------------- KONIEC RUNDY ----------------------------
             * + następuje początek kolejnejm więc musimy:
             * 1. Zwiększyś numer++ rundy. */
            var orchestrator = new CombatOrchestrator();
            var stateAfterCombat = orchestrator.ResolveCombatPhase(currentState, eventBus, context);
            stateAfterCombat = ProcessEndOfRoundStatuses(stateAfterCombat, eventBus, context);

            /* Ogarnąć, który gracz ma rozpocząć kolejną turę/rundę.
             * (termin tura/runda jest używany zamiennie w tym kontekście). */
            int nextTurnNumber = currentState.TurnNumber + 1;
            int nextActivePlayerId = (nextTurnNumber % 2 != 0) ? 1 : 2;

            var playerToStart = stateAfterCombat.GetPlayer(nextActivePlayerId);
            var updatedPlayer = playerToStart
               .WithTurnStartBlood(nextTurnNumber)
               .WithCardDrawn();

            GameState finalState;
            if (nextActivePlayerId == 1)
            {
                finalState = stateAfterCombat.With(
                    turnNumber: nextTurnNumber,
                    currentPhase: GamePhase.UnitOnly,
                    activePlayerId: nextActivePlayerId,
                    playerA: updatedPlayer // Aktualizujemy A
                );
            }
            else
            {
                finalState = stateAfterCombat.With(
                    turnNumber: nextTurnNumber,
                    currentPhase: GamePhase.UnitOnly,
                    activePlayerId: nextActivePlayerId,
                    playerB: updatedPlayer // Aktualizujemy B
                );
            }

            return finalState;
        }
        public bool ShouldEndPhaseAutomatically(GameState state)
        {
            return false; // Ta faza nigdy nie kończy się sama, czeka na EndPhaseCommand
        }
        private GameState ProcessEndOfRoundStatuses(GameState state, EventBus events, GameContext context)
        {
            var workingState = state;
            var unitsToCheck = workingState.Board.GetAllUnits();

            foreach (var unit in unitsToCheck)
            {
                // KeywordProcessor sprawdzi Burning i inne przyszłe efekty końca tury
                workingState = context.Keywords.ProcessRoundEnd(workingState, unit, context);
            }

            return new DeathResolver().ResolveDeaths(workingState, events, context);
        }
    }

}
