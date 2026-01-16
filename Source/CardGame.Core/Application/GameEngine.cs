using CardGame.Core.Cards.Factories;
using CardGame.Core.Commands.Interfaces;
using CardGame.Core.Events;
using CardGame.Core.Events.Triggers;
using CardGame.Core.GameRules.Auras;
using CardGame.Core.GameRules.Death;
using CardGame.Core.State.Models;
using CardGame.Core.StateMachine;
using System.Collections.Generic;
using System.Linq;

namespace CardGame.Core.Application
{
    /// <summary>
    /// The core orchestrator of the card game. 
    /// Manages state transitions, command execution, and the resolution of triggered effects.
    /// </summary>
    public class GameEngine
    {
        #region Private Fields
        private readonly GameContext _gameContext;
        private readonly GameStateMachine _stateMachine;
        private readonly TriggerSystem _triggerSystem;
        private readonly DeathResolver _deathResolver;
        private readonly AuraSystem _auraSystem;
        private float _interactionTimer = 0f;
        #endregion

        #region Public Properties
        public GameState CurrentState { get; set; }
        public bool IsGameOver { get; private set; }
        public int? WinnerId { get; private set; }
        public EventBus Events { get; }
        public CardFactory Factory { get; }
        public DeterministicRng Rng { get; }
        #endregion

        public GameEngine(GameState initialState, int seed = 0)
        {
            CurrentState = initialState;
            Events = new EventBus();
            Rng = new DeterministicRng(seed);
            Factory = new CardFactory(CardGame.Core.Cards.Data.CardLibrary.Instance, Rng);

            _gameContext = new GameContext(Factory, Rng, Events);
            _stateMachine = new GameStateMachine();
            _triggerSystem = new TriggerSystem();
            _deathResolver = new DeathResolver();
            _auraSystem = new AuraSystem();

            // Perform initial aura calculation to set the starting state
            CurrentState = _auraSystem.RecalculateAuras(initialState, null);
        }

        /// <summary>
        /// Processes a player command, updates the game state, and resolves all resulting triggers/auras.
        /// </summary>
        public ExecutionResult ExecuteCommand(IGameCommand command)
        {
            Events.ClearHistory();

            if (IsGameOver)
                return new ExecutionResult(CurrentState, new List<CardGame.Core.Events.Interfaces.IGameEvent>());

            var logic = _stateMachine.GetStateForPhase(CurrentState.CurrentPhase);
            if (!logic.IsCommandAllowed(command, CurrentState))
                return new ExecutionResult(CurrentState, new List<CardGame.Core.Events.Interfaces.IGameEvent>());

            var oldPhase = CurrentState.CurrentPhase;
            GameState newState = command.Execute(CurrentState, Events, _gameContext);
            _interactionTimer = 0f;

            #region Phase Transition Logic
            bool phaseFinished = command is CardGame.Core.Commands.Implementations.EndPhaseCommand || logic.ShouldEndPhaseAutomatically(newState);

            while (phaseFinished)
            {
                var currentLogic = _stateMachine.GetStateForPhase(newState.CurrentPhase);
                newState = currentLogic.ProcessEndPhase(newState, Events, _gameContext);

                if (newState.CurrentPhase != oldPhase)
                {
                    Events.Publish(new PhaseChangedEvent(newState.CurrentPhase, newState.ActivePlayerId));
                    oldPhase = newState.CurrentPhase;
                }

                var nextLogic = _stateMachine.GetStateForPhase(newState.CurrentPhase);
                phaseFinished = nextLogic.ShouldEndPhaseAutomatically(newState);

                if (CheckGameOver(newState)) break;
            }
            #endregion

            #region Reactive Logic Loop (Triggers, Death, Auras)
            int safetyCounter = 0;
            while (safetyCounter++ < 100)
            {
                var startState = newState;
                newState = _triggerSystem.ProcessEvents(newState, Events, _gameContext);
                newState = _deathResolver.ResolveDeaths(newState, Events, _gameContext);
                newState = _auraSystem.RecalculateAuras(newState, Events);

                if (newState.PendingInteraction != null || CheckGameOver(newState)) break;
                if (!Events.HasEvents && newState.Board == startState.Board) break;
            }
            #endregion

            #region Spell Stack Cleanup
            while (newState.PendingInteraction == null && newState.SpellStack.Any())
            {
                var spell = newState.SpellStack.Last();
                Events.Publish(new CardMovedEvent(spell.InstanceId, spell.OwnerPlayerId, CardZone.Stack, CardZone.Graveyard));
                newState = newState.UpdatePlayer(newState.GetPlayer(spell.OwnerPlayerId).WithCardAddedToDiscard(spell))
                    .With(spellStack: newState.SpellStack.Take(newState.SpellStack.Count - 1));
            }
            #endregion

            if (newState.PendingInteraction != null)
            {
                Events.Publish(new InteractionRequiredEvent(newState.PendingInteraction));
            }

            CurrentState = newState;
            return new ExecutionResult(CurrentState, Events.GetHistory());
        }

        /// <summary>
        /// Periodic update for handling interaction timeouts.
        /// </summary>
        public void Update(float delta)
        {
            if (CurrentState.PendingInteraction == null || IsGameOver) return;

            _interactionTimer += delta;
            if (_interactionTimer >= 5.0f)
            {
                var pending = CurrentState.PendingInteraction;
                _interactionTimer = 0f;

                // Default target selection for timeout fallback
                int targetId = (pending.RequiredTargetType == CardGame.Core.Cards.Data.TargetType.Choice) ? 0 :
                               (CurrentState.Board.GetAllUnits().FirstOrDefault()?.InstanceId ?? 0);

                ExecuteCommand(new CardGame.Core.Commands.Implementations.SelectTargetCommand(CurrentState.ActivePlayerId, targetId));
            }
        }

        #region Private Helpers
        private bool CheckGameOver(GameState s)
        {
            bool p1Dead = s.PlayerA.Health <= 0;
            bool p2Dead = s.PlayerB.Health <= 0;

            if (p1Dead || p2Dead)
            {
                IsGameOver = true;
                WinnerId = (p1Dead && p2Dead) ? null : (p1Dead ? 2 : 1);

                Events.Publish(new GameOverEvent(WinnerId));
                return true;
            }
            return false;
        }
        #endregion
    }
}