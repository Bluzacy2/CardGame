using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using CardGame.Core.Commands.Implementations;
using CardGame.Core.Commands.Interfaces;
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
            /* Walidacja, czy kraczyna wykonująca komendę jest aktywnym graczem. */
            if (command.PlayerId != state.ActivePlayerId) return false;

            /* Dozwolone komendy w fazie jednostek są następujące:
             * 1. Zagranie jedostki.
             * 2. Zakończenie fazy (tury gracza). */

            /* wstaw wystawienie jednostki komenda */
            if (command is EndPhaseCommand) return true;

            return false;
        }
    

        public GameState ProcessEndPhase(GameState currentState)
        {
            int nextPlayerId = (currentState.ActivePlayerId == 1) ? 2 : 1;
            return currentState.With(
                currentPhase: GamePhase.UnitAndAction,
                activePlayerId: nextPlayerId);
        }
    }
}
