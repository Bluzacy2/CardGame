using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Factories;
using CardGame.Core.Commands.Implementations;
using CardGame.Core.Commands.Interfaces;
using CardGame.Core.Events;
using CardGame.Core.Events.Triggers;
using CardGame.Core.State.Enums;
using CardGame.Core.State.Models;
using CardGame.Core.StateMachine;
using CardGame.Core.StateMachine.Interfaces;
using CardGame.Core.StateMachine.Phases;
using CardGame.Core.GameRules.Death;
using System;

namespace CardGame.Core.Application
{
    public class GameEngine
    {
        public GameState CurrentState { get;  set; }
        public EventBus Events { get; }

        public DeterministicRng Rng { get; }
        public CardFactory Factory { get; }

        private readonly GameContext _gameContext;

        private readonly GameStateMachine _stateMachine;
        private readonly TriggerSystem _triggerSystem;
        private readonly DeathResolver _deathResolver;


        public GameEngine(GameState initialState, int seed = 0)
        {
            CurrentState = initialState;
            Events = new EventBus();
            Rng = new DeterministicRng(seed);
            Factory = new CardFactory(CardLibrary.Instance, Rng);
            
            _gameContext = new GameContext(Factory, Rng, Events);

            _stateMachine = new GameStateMachine();
            _triggerSystem = new TriggerSystem();
            _deathResolver = new DeathResolver();

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
            while (true)
            {
                // A. Przetwórz wszystkie aktywne triggery (np. OnPlayed, OnSacrificed)
                newState = _triggerSystem.ProcessEvents(newState, Events, _gameContext);

                // B. Sprawdź śmierć (Cleanup Step)
                // To wygeneruje UnitDiedEvent, jeśli ktoś ma HP <= 0
                // WAŻNE: DeathResolver musi być napisany tak, że jeśli nikt nie ginie, to nie generuje eventów.
                newState = _deathResolver.ResolveDeaths(newState, Events);

                // C. Warunek wyjścia:
                // Jeśli nie ma nowych eventów (czyli nikt nie umarł, nic się nie odpaliło), kończymy.
                if (!Events.HasEvents)
                {
                    break;
                }

                // Jeśli są eventy (np. UnitDied wygenerowane w kroku B), pętla leci od nowa,
                // żeby TriggerSystem (krok A) mógł obsłużyć Deathrattle/CorpseEater!
            }


            /* 4. Sprawdź, czy komenda to EndPhaseCommand, aby przetworzyć logikę końca fazy. */
            if (command is EndPhaseCommand)
            {
                newState = currentPhaseLogic.ProcessEndPhase(newState, Events);
            }
            else if (newState.CurrentPhase == CurrentState.CurrentPhase)
            {
                if (currentPhaseLogic.ShouldEndPhaseAutomatically(newState))
                {
                    Console.WriteLine($"[SILNIK] Faza {currentPhaseLogic.PhaseType} zakończona automatycznie.");
                    newState = currentPhaseLogic.ProcessEndPhase(newState, Events);
                }
            }
            newState = _triggerSystem.ProcessEvents(newState, Events, _gameContext);

            /* 5. Zaktualizuj CurrentState do nowego stanu. */
            CurrentState = newState;

            // Debug output
            
        }
    }
}
