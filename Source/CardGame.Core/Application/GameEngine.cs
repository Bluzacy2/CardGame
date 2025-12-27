using CardGame.Core.Cards.Factories;
using CardGame.Core.Commands.Interfaces;
using CardGame.Core.Events;
using CardGame.Core.Events.Interfaces;
using CardGame.Core.Events.Triggers;
using CardGame.Core.GameRules.Auras;
using CardGame.Core.GameRules.Death;
using CardGame.Core.State.Models;
using CardGame.Core.StateMachine;
using CardGame.Core.StateMachine.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

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
        private float _interactionTimer = 0f;

        public GameEngine(GameState initialState, int seed = 0)
        {
            CurrentState = initialState;
            Events = new EventBus();
            Rng = new DeterministicRng(seed);
            Factory = new CardFactory(CardGame.Core.Cards.Data.CardLibrary.Instance, Rng);
            _gameContext = new GameContext(Factory, Rng, Events, new GameRules.Damage.DamageCalculator());
            _stateMachine = new GameStateMachine();
            _triggerSystem = new TriggerSystem();
            _deathResolver = new DeathResolver();
            _auraSystem = new AuraSystem();
            CurrentState = _auraSystem.RecalculateAuras(CurrentState);
        }

        public ExecutionResult ExecuteCommand(IGameCommand command)
        {
            Events.ClearHistory();
            if (IsGameOver) return new ExecutionResult(CurrentState, new List<IGameEvent>());

            IPhaseState currentPhaseLogic = _stateMachine.GetStateForPhase(CurrentState.CurrentPhase);
            if (!currentPhaseLogic.IsCommandAllowed(command, CurrentState)) return new ExecutionResult(CurrentState, new List<IGameEvent>());

            GameState newState = command.Execute(CurrentState, Events, _gameContext);
            _interactionTimer = 0f;

            if (command is CardGame.Core.Commands.Implementations.EndPhaseCommand)
                newState = currentPhaseLogic.ProcessEndPhase(newState, Events, _gameContext);
            else if (currentPhaseLogic.ShouldEndPhaseAutomatically(newState))
                newState = currentPhaseLogic.ProcessEndPhase(newState, Events, _gameContext);

            // Safety Break - chroni przed nieskończonymi pętlami efektów
            int safetyCycle = 0;
            const int MAX_CYCLES = 100;

            while (true)
            {
                if (safetyCycle++ > MAX_CYCLES)
                {
                    Console.WriteLine("[CRITICAL] Wykryto potencjalną nieskończoną pętlę efektów! Przerywanie.");
                    break;
                }

                var stateBefore = newState;
                newState = _deathResolver.ResolveDeaths(newState, Events);
                newState = _auraSystem.RecalculateAuras(newState);
                newState = _triggerSystem.ProcessEvents(newState, Events, _gameContext);

                if (newState.PendingInteraction != null)
                {
                    newState = _deathResolver.ResolveDeaths(newState, Events);
                    break;
                }

                if (CheckGameOver(newState)) break;
                if (!Events.HasEvents && newState == stateBefore) break;
            }

            while (newState.PendingInteraction == null && newState.SpellStack.Count > 0)
            {
                var topSpell = newState.SpellStack.Last();
                var owner = newState.GetPlayer(topSpell.OwnerPlayerId);

                var newStack = newState.SpellStack.ToList();
                newStack.RemoveAt(newStack.Count - 1);

                newState = newState.UpdatePlayer(owner.WithCardAddedToDiscard(topSpell))
                                   .With(spellStack: newStack);
            }

            CurrentState = newState;
            return new ExecutionResult(CurrentState, Events.GetHistory());
        }

        public void Update(float deltaTime)
        {
            if (CurrentState.PendingInteraction == null || IsGameOver) return;
            _interactionTimer += deltaTime;
            if (_interactionTimer >= 5.0f) AutoResolveInteraction();
        }

        private void AutoResolveInteraction()
        {
            var pending = CurrentState.PendingInteraction;
            if (pending == null) return;
            int autoId = 0;
            if (pending.RequiredTargetType != CardGame.Core.Cards.Data.TargetType.Choice)
            {
                var u = CurrentState.Board.GetAllUnits().FirstOrDefault();
                if (u != null) autoId = u.InstanceId;
            }
            ExecuteCommand(new CardGame.Core.Commands.Implementations.SelectTargetCommand(CurrentState.ActivePlayerId, autoId));
        }

        private bool CheckGameOver(GameState state)
        {
            bool p1D = state.PlayerA.Health <= 0;
            bool p2D = state.PlayerB.Health <= 0;
            if (p1D || p2D)
            {
                IsGameOver = true;
                WinnerId = p1D && p2D ? null : (p1D ? 2 : 1);
                return true;
            }
            return false;
        }
    }
}