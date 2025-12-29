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
    public class GameEngine
    {
        public GameState CurrentState { get; set; }
        public bool IsGameOver { get; private set; }
        public int? WinnerId { get; private set; }
        public EventBus Events { get; }
        public CardFactory Factory { get; }
        public DeterministicRng Rng { get; }

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
            _gameContext = new GameContext(Factory, Rng, Events);
            _stateMachine = new GameStateMachine();
            _triggerSystem = new TriggerSystem();
            _deathResolver = new DeathResolver();
            _auraSystem = new AuraSystem();
            CurrentState = _auraSystem.RecalculateAuras(CurrentState);
        }

        public ExecutionResult ExecuteCommand(IGameCommand command)
        {
            Events.ClearHistory();
            if (IsGameOver) return new ExecutionResult(CurrentState, new List<CardGame.Core.Events.Interfaces.IGameEvent>());

            var logic = _stateMachine.GetStateForPhase(CurrentState.CurrentPhase);
            if (!logic.IsCommandAllowed(command, CurrentState))
                return new ExecutionResult(CurrentState, new List<CardGame.Core.Events.Interfaces.IGameEvent>());

            GameState newState = command.Execute(CurrentState, Events, _gameContext);
            _interactionTimer = 0f;


            bool phaseChanged = command is CardGame.Core.Commands.Implementations.EndPhaseCommand || logic.ShouldEndPhaseAutomatically(newState);
            while (phaseChanged)
            {
                var currentLogic = _stateMachine.GetStateForPhase(newState.CurrentPhase);
                newState = currentLogic.ProcessEndPhase(newState, Events, _gameContext);

                var nextLogic = _stateMachine.GetStateForPhase(newState.CurrentPhase);
                phaseChanged = nextLogic.ShouldEndPhaseAutomatically(newState);

                if (CheckGameOver(newState)) break;
            }


            int safety = 0;
            while (safety++ < 100)
            {
                var start = newState;
                newState = _triggerSystem.ProcessEvents(newState, Events, _gameContext);
                newState = _deathResolver.ResolveDeaths(newState, Events, _gameContext);
                newState = _auraSystem.RecalculateAuras(newState);

                if (newState.PendingInteraction != null || CheckGameOver(newState)) break;
                if (!Events.HasEvents && newState.Board == start.Board) break;
            }

            while (newState.PendingInteraction == null && newState.SpellStack.Any())
            {
                var spell = newState.SpellStack.Last();
                newState = newState.UpdatePlayer(newState.GetPlayer(spell.OwnerPlayerId).WithCardAddedToDiscard(spell))
                    .With(spellStack: newState.SpellStack.Take(newState.SpellStack.Count - 1));
            }

            CurrentState = newState;
            return new ExecutionResult(CurrentState, Events.GetHistory());
        }

        public void Update(float delta)
        {
            if (CurrentState.PendingInteraction == null || IsGameOver) return;
            _interactionTimer += delta;
            if (_interactionTimer >= 5.0f)
            {
                var pending = CurrentState.PendingInteraction;
                _interactionTimer = 0f;

                int tid = (pending.RequiredTargetType == CardGame.Core.Cards.Data.TargetType.Choice) ? 0 :
                          (CurrentState.Board.GetAllUnits().FirstOrDefault()?.InstanceId ?? 0);

                ExecuteCommand(new CardGame.Core.Commands.Implementations.SelectTargetCommand(CurrentState.ActivePlayerId, tid));
            }
        }

        private bool CheckGameOver(GameState s)
        {
            bool p1 = s.PlayerA.Health <= 0; bool p2 = s.PlayerB.Health <= 0;
            if (p1 || p2) { IsGameOver = true; WinnerId = p1 && p2 ? null : (p1 ? 2 : 1); return true; }
            return false;
        }
    }
}