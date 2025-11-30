using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using CardGame.Core.Commands.Implementations;
using CardGame.Core.Commands.Interfaces;
using CardGame.Core.State.Models;
using CardGame.Core.StateMachine;
using CardGame.Core.StateMachine.Interfaces;

namespace CardGame.Core.Application
{
    public class GameEngine
    {
        /* Aktualny stan gry. */
        public GameState CurrentState { get; private set; }
        /* Pomocnik tłumaczenia Faz gry. */
        private readonly GameStateMachine _stateMachine;
        public GameEngine(GameState initialState)
        {
            CurrentState = initialState;
            _stateMachine = new GameStateMachine();
        }

        public void ExecuteCommand(IGameCommand command)
        {
            /* 1. Pobierz logikę dla obecnej fazy, aby zadecydować, co można zrobić dla obecnej fazy. */
            IPhaseState currentPhaseLogic = _stateMachine.GetStateForPhase(CurrentState.CurrentPhase);

            /* 2. Sprawdź, czy komenda jest dozwolona w obecnej fazie (WALIDACJA). */
            if (!currentPhaseLogic.IsCommandAllowed(command, CurrentState))
            {
                Console.WriteLine($"Komenda {command.GetType().Name} nie jest dozwolona w fazie {CurrentState.CurrentPhase} " +
                    $"dla gracza {command.PlayerId}.");
                return;
            }
            /* 3. Wykonaj komendę, co zmienia cokolwiek. */
            GameState newState = command.Execute(CurrentState);

            /* 4. Sprawdź, czy komenda to EndPhaseCommand, aby przetworzyć logikę końca fazy. */
            if (command is EndPhaseCommand)
            {
                newState = currentPhaseLogic.ProcessEndPhase(newState);
            }

            /* 5. Zaktualizuj CurrentState do nowego stanu. */
            CurrentState = newState;

            // Debug output
            Console.WriteLine($"Nowy stan gry po komendzie {command.GetType().Name}: " +
                $"Tura {CurrentState.TurnNumber}, Faza {CurrentState.CurrentPhase}, " +
                $"Aktywny gracz {CurrentState.ActivePlayerId}");
        }
    }
}
