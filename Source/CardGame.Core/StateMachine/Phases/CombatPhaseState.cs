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
    public class CombatPhaseState: IPhaseState
    {
        public GamePhase PhaseType => GamePhase.Combat;
        public bool IsCommandAllowed(IGameCommand command, GameState state)
        {
            /* W fazie walki **zazwyczaj** gracze nie mogą nic robić (nie rozmawiamy o eventach w tym domu).
             * Walka dzieje się sama od lewej strony bitwy do prawej. System sam kończy tę fazę (po zakończeniu
             * animacji, I guess).*/
            if (command is EndPhaseCommand) return true;
            return false;
        }

        public GameState ProcessEndPhase(GameState currentState)
        {
            /* ---------------------- KONIEC RUNDY ----------------------------
             * + następuje początek kolejnejm więc musimy:
             * 1. Zwiększyś numer++ rundy. */
            int nextTurnNumber = currentState.TurnNumber + 1;
            /* Ogarnąć, który gracz ma rozpocząć kolejną turę/rundę.
             * (termin tura/runda jest używany zamiennie w tym kontekście). */

            int startingPlayerForNextTurn;

            /* Nieparzyste rundy zaczyna gracz 1, parzyste gracz 2. */
            if (nextTurnNumber % 2 != 0)
            { startingPlayerForNextTurn = 1; }
            

            else
            {startingPlayerForNextTurn = 2;}

            return currentState.With(
                turnNumber: nextTurnNumber,
                currentPhase: GamePhase.UnitOnly,
                activePlayerId: startingPlayerForNextTurn);
        }
    }
}
