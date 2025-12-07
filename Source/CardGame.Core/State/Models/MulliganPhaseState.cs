using CardGame.Core.Commands.Implementations;
using CardGame.Core.Commands.Interfaces;
using CardGame.Core.Events;
using CardGame.Core.State.Enums;
using CardGame.Core.State.Models;
using CardGame.Core.StateMachine.Interfaces;

namespace CardGame.Core.StateMachine.Phases
{
    public class MulliganPhaseState : IPhaseState
    {
        public GamePhase PhaseType => GamePhase.Mulligan;

        public bool IsCommandAllowed(IGameCommand command, GameState state)
        {
            // W tej fazie każdy może wykonać ruch, ale tylko raz (teoretycznie)
            // Można dodać walidację state.PlayersReady.Contains(command.PlayerId),
            // żeby zablokować wielokrotne wysyłanie, ale UI powinno to blokować.

            if (command is ConfirmMulliganCommand) return true;

            return false;
        }

        public GameState ProcessEndPhase(GameState currentState, EventBus eventBus)
        {
            // Ta metoda jest wywoływana zazwyczaj przez EndPhaseCommand.
            // W Mulliganie nie używamy EndPhaseCommand, tylko automatycznie przełączamy,
            // gdy obaj są gotowi. Ale GameEngine nie robi auto-przejścia sam z siebie.

            // ARCHITEKTURA:
            // GameEngine musi sprawdzić po wykonaniu komendy, czy zmienić fazę.
            // Możemy to zrobić sprytnie: Niech ConfirmMulliganCommand na końcu
            // sprawdza warunek i jeśli obaj gotowi, sama zmienia fazę?
            // NIE. Komenda nie powinna zmieniać fazy.

            // Zrobimy tak: GameEngine po każdej komendzie może zapytać PhaseState: "Czy to już koniec?".
            // Ale Twój silnik jest prosty.

            // Najprościej: Niech ConfirmMulliganCommand zwraca stan zaktualizowany.
            // A jeśli obaj są gotowi, to GameStateMachine przy kolejnym zapytaniu
            // (albo specjalny Check) powinien przenieść nas dalej.

            // W Twoim obecnym GameEngine zmiana fazy dzieje się TYLKO przy EndPhaseCommand.
            // To problem dla Mulligana, który kończy się "sam".

            // ROZWIĄZANIE:
            // Użyjmy "EndPhaseCommand" jako "Systemowy sygnał przejścia".
            // Ale to skomplikowane.

            // Zróbmy tak: Wewnątrz MulliganPhaseState dodajmy logikę, która 
            // przy zapytaniu o 'ProcessEndPhase' po prostu przenosi do gry.
            // A w GameEngine dodamy mały hack, żeby sprawdzić ten warunek.

            // WRÓĆ. Najczystsze rozwiązanie w Twoim kodzie:
            // 1. Gracz klika "Zatwierdź".
            // 2. Komenda się wykonuje.
            // 3. Jeśli 'PlayersReady' ma 2 osoby -> Wykonaj logicznie przejście do UnitOnly.

            // Zróbmy to w GameEngine.

            return currentState.With(currentPhase: GamePhase.UnitOnly);
        }

        // Metoda pomocnicza, którą wywoła GameEngine
        public bool IsPhaseComplete(GameState state)
        {
            return state.PlayersReady.Contains(1) && state.PlayersReady.Contains(2);
        }
        public bool ShouldEndPhaseAutomatically(GameState state)
        {
            // Faza kończy się sama, gdy obaj gracze (ID 1 i 2) są na liście gotowych
            return state.PlayersReady.Contains(1) && state.PlayersReady.Contains(2);
        }
    }
}