using CardGame.Core.Commands.Implementations;
using CardGame.Core.Commands.Interfaces;
using CardGame.Core.Events;
using CardGame.Core.Events.Triggers;
using CardGame.Core.State.Models;
using CardGame.Core.StateMachine;
using CardGame.Core.StateMachine.Interfaces;


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CardGame.Core.Application
{
    public class GameEngine
    {
        public GameState CurrentState { get;  set; }
        public EventBus Events { get; }

        private readonly GameStateMachine _stateMachine;
        private readonly TriggerSystem _triggerSystem;

        
        public GameEngine(GameState initialState)
        {
            CurrentState = initialState;
            _stateMachine = new GameStateMachine();
            Events = new EventBus();
            _triggerSystem = new TriggerSystem();
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
            GameState newState = command.Execute(CurrentState, Events);

            /* 4. Sprawdź, czy komenda to EndPhaseCommand, aby przetworzyć logikę końca fazy. */
            if (command is EndPhaseCommand)
            {
                newState = currentPhaseLogic.ProcessEndPhase(newState, Events);
            }
            newState = _triggerSystem.ProcessEvents(newState, Events);

            /* 5. Zaktualizuj CurrentState do nowego stanu. */
            CurrentState = newState;

            // Debug output
            
        }
    }
}
