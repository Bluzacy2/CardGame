using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Factories;
using CardGame.Core.Commands.Implementations;
using CardGame.Core.Commands.Interfaces;
using CardGame.Core.Events;
using CardGame.Core.Events.Triggers;
using CardGame.Core.GameRules.Auras;
using CardGame.Core.GameRules.Damage;
using CardGame.Core.GameRules.Death;
using CardGame.Core.State.Models;
using CardGame.Core.StateMachine;
using CardGame.Core.StateMachine.Interfaces;
using System;
using static CardGame.Core.Events.UnitSacrificedEvent;

namespace CardGame.Core.Application
{
    public class GameEngine
    {
        public GameState CurrentState { get; set; }
        public bool IsGameOver { get; private set; }
        public int? WinnerId { get; private set; }

        public EventBus Events { get; }
        public DeterministicRng Rng { get; }
        public CardFactory Factory { get; }

        private readonly GameContext _gameContext;
        private readonly GameStateMachine _stateMachine;
        private readonly TriggerSystem _triggerSystem;
        private readonly DeathResolver _deathResolver;
        private readonly AuraSystem _auraSystem;

        public GameEngine(GameState initialState, int seed = 0)
        {
            CurrentState = initialState;
            Events = new EventBus();
            Rng = new DeterministicRng(seed);
            Factory = new CardFactory(CardLibrary.Instance, Rng);

            var damageCalculator = new DamageCalculator();
            _gameContext = new GameContext(Factory, Rng, Events, damageCalculator);

            _stateMachine = new GameStateMachine();
            _triggerSystem = new TriggerSystem();
            _deathResolver = new DeathResolver();
            _auraSystem = new AuraSystem();

            CurrentState = _auraSystem.RecalculateAuras(CurrentState);
        }

        public void ExecuteCommand(IGameCommand command)
        {
            if (IsGameOver)
            {
                Console.WriteLine("[SILNIK] Gra zakończona. Ruchy zablokowane.");
                return;
            }

            IPhaseState currentPhaseLogic = _stateMachine.GetStateForPhase(CurrentState.CurrentPhase);
            if (!currentPhaseLogic.IsCommandAllowed(command, CurrentState))
            {
                Console.WriteLine($"[BŁĄD] Komenda {command.GetType().Name} niedozwolona.");
                return;
            }

            GameState newState = command.Execute(CurrentState, Events);

            if (command is EndPhaseCommand)
            {
                newState = currentPhaseLogic.ProcessEndPhase(newState, Events);
            }
            else if (newState.CurrentPhase == CurrentState.CurrentPhase)
            {
                if (currentPhaseLogic.ShouldEndPhaseAutomatically(newState))
                {
                    newState = currentPhaseLogic.ProcessEndPhase(newState, Events);
                }
            }
            while (true)
            {
                var stateBeforeIteration = CurrentState;
                newState = _auraSystem.RecalculateAuras(newState);
                newState = _triggerSystem.ProcessEvents(newState, Events, _gameContext);
                newState = _deathResolver.ResolveDeaths(newState, Events);

                
                bool gameOver = CheckGameOver(newState);


                if (gameOver)
                {
                    CurrentState = newState;
                    return;
                }

                if (!Events.HasEvents)
                {
                    newState = _auraSystem.RecalculateAuras(newState);
                    break;
                }

            }

            CurrentState = newState;
        }

        private bool CheckGameOver(GameState state)
        {
            // Console.WriteLine($"[DEBUG KOŃCA GRY] HP P1: {state.PlayerA.Health}, HP P2: {state.PlayerB.Health}");

            bool p1Dead = state.PlayerA.Health <= 0;
            bool p2Dead = state.PlayerB.Health <= 0;

            if (p1Dead || p2Dead)
            {
                IsGameOver = true;

                if (p1Dead && p2Dead)
                {
                    WinnerId = null;
                    Console.WriteLine("[GAME OVER] REMIS!");
                    Events.Publish(new GameOverEvent(null));
                }
                else if (p1Dead)
                {
                    WinnerId = 2;
                    Console.WriteLine("[GAME OVER] Zwycięzca: Gracz 2!");
                    Events.Publish(new GameOverEvent(2));
                }
                else
                {
                    WinnerId = 1;
                    Console.WriteLine("[GAME OVER] Zwycięzca: Gracz 1!");
                    Events.Publish(new GameOverEvent(1));
                }
                return true;
            }
            return false;
        }
    }
}