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
    public class ActionPhaseState: IPhaseState
    {
        public GamePhase PhaseType => GamePhase.ActionOnly;
        public bool IsCommandAllowed(IGameCommand command, GameState state)
        {
            /* Walidacja, czy kraczyna wykonująca komendę jest aktywnym graczem. */
            if (command.PlayerId != state.ActivePlayerId) return false;
            /* Dozwolone komendy w fazie akcji są następujące:
             * 1. Wykonanie akcji.
             * 2. Zakończenie fazy (tury gracza). */
            // if (command is ActionCommand) return true;
            if (command is EndPhaseCommand) return true;
            return false;
        }
        public GameState ProcessEndPhase(GameState currentState)
        {
            /* LOGIKA PRZEJŚCIA DO KOLEJNEJ FAZY LUB TURY GRACZA
             * ActionPhase jest ostatnią interaktywną fazę, gdzie
             * następnie przechodzimy w AutoBattlera na liniach od
             * lewej. Jest wiele podejść co do tego co ma się dziać
             * z ActivePlayerem, ale na razie uznajmy, że pozostoaje
             * bez zmian.*/
            return currentState.With(
                currentPhase: GamePhase.Combat);
        }
    }
}
