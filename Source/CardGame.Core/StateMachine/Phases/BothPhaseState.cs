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
    public class BothPhaseState : IPhaseState
    {
        public GamePhase PhaseType => GamePhase.UnitAndAction;
        public bool IsCommandAllowed(IGameCommand command, GameState state)
        {
            /* Walidacja, czy kraczyna wykonująca komendę jest aktywnym graczem. */
            if (command.PlayerId != state.ActivePlayerId) return false;

            /* Dozwolone komendy w fazie jednostek są następujące:
             * 1. Zagranie jedostki.
             * 2. Wykonanie akcji.
             * 3. Zakończenie fazy (tury gracza). */

            // if (command is PlayUnitCommand) return true;
            // if (command is ActionCommand) return true;
            if (command is EndPhaseCommand) return true;
            return false;
        }

        public GameState ProcessEndPhase(GameState currentState)
        {
            int nextPlayerId = (currentState.ActivePlayerId == 1) ? 2 : 1;
            return currentState.With(
                currentPhase: GamePhase.ActionOnly,
                activePlayerId: nextPlayerId);
        }
    }
    
    
}
