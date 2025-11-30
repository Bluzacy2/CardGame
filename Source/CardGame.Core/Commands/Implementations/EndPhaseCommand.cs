using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using CardGame.Core.Commands.Interfaces;
using CardGame.Core.State.Models;
using CardGame.Core.StateMachine.Interfaces;

/* W komendach NIE implementujemy logiki gry, jedynie przekazujemy dla StateMachine informacje co chcemy zrobić.
 * Logika gry jest implementowana w StateMachine. */

namespace CardGame.Core.Commands.Implementations
{
    public class EndPhaseCommand: IGameCommand
    {
        public int PlayerId { get; }
        public EndPhaseCommand(int playerId)
        {
            PlayerId = playerId;
        }

        public GameState Execute(GameState currentState)
        {
            /* Logika kończenia fazy jest wyjątkiem od reguły. Pozostałe komendy **zazwyczaj** same zmieniają stan gry
             * poprzez wykonywanie operacji przez samych siebie np. odejmij punkty życia, zagraj kartę itp.
             * Inaczej jest z kończeniem fazy, gdyż to StateMachine decyduje jak i na jaką fazę przejść dalej.
             * Przez to zwracamy obecny stan gry i wysyłamy sygnał do StateMachine, że ma zakończyć fazę i przejść
             * do kolejnej . */
            return currentState;
        }
    }
}
