using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using CardGame.Core.Events;
using CardGame.Core.Commands.Interfaces;
using CardGame.Core.State.Enums;
using CardGame.Core.State.Models;

namespace CardGame.Core.StateMachine.Interfaces
{
    public interface IPhaseState
    {
        /* Właściwość PhaseType: Określa typ fazy gry reprezentowanej przez ten stan.
         * Kluczowe dla identyfikacji i zarządzania różnymi fazami w logice gry.
         */
        GamePhase PhaseType { get; }

        /* Metoda IsCommandAllowed: Sprawdza, czy dana komenda jest dozwolona w bieżącej fazie gry.
         * Przyjmuje komendę i aktualny stan gry jako parametry.
         * Zwraca wartość boolowską wskazującą, czy komenda może być wykonana.
         * Kluczowe dla walidacji działań graczy w kontekście fazy gry.
         */
        bool IsCommandAllowed(IGameCommand command, GameState state);

        // Gdy faza się kończy, zwraca następny stan fazy gry.
        GameState ProcessEndPhase(GameState currentState, EventBus eventBus);
    }
}
