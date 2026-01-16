using CardGame.Core.Application;
using CardGame.Core.Events;
using CardGame.Core.Events.Triggers;
using CardGame.Core.GameRules.Battle;
using CardGame.Core.GameRules.Death;
using CardGame.Core.State.Models;

namespace CardGame.Core.Combat
{
    /// <summary>
    /// Orchestrates the combat phase by resolving battles across all board lines and handling associated triggers.
    /// </summary>
    public class CombatOrchestrator
    {
        #region Private Fields
        private readonly BattleService _battleService = new BattleService();
        private readonly DeathResolver _deathResolver = new DeathResolver();
        private readonly TriggerSystem _triggerSystem = new TriggerSystem();
        #endregion

        #region Combat Resolution
        /// <summary>
        /// Resolves the entire combat phase by processing each board line sequentially.
        /// </summary>
        /// <param name="currentState">The current game state at the start of combat.</param>
        /// <param name="eventBus">The event bus for publishing combat events.</param>
        /// <param name="context">The game context with additional services.</param>
        /// <returns>The updated game state after resolving all combat.</returns>
        public GameState ResolveCombatPhase(GameState currentState, EventBus eventBus, GameContext context)
        {
            var workingState = currentState;

            for (int lineIndex = 0; lineIndex < 4; lineIndex++)
            {
                eventBus.Publish(new PreLineCombatEvent(lineIndex));
                workingState = _triggerSystem.ProcessEvents(workingState, eventBus, context);
                workingState = _deathResolver.ResolveDeaths(workingState, eventBus, context);

                var line = workingState.Board.Lines[lineIndex];
                var player1Unit = line.Player1Unit;
                var player2Unit = line.Player2Unit;

                if (player1Unit != null && player2Unit != null)
                    workingState = _battleService.ResolveCombatDuel(workingState, player1Unit, player2Unit, lineIndex, eventBus, context);
                else if (player1Unit != null)
                    workingState = _battleService.ResolveBonusStrike(workingState, player1Unit, null, lineIndex, eventBus, context);
                else if (player2Unit != null)
                    workingState = _battleService.ResolveBonusStrike(workingState, player2Unit, null, lineIndex, eventBus, context);

                workingState = _deathResolver.ResolveDeaths(workingState, eventBus, context);
                workingState = _triggerSystem.ProcessEvents(workingState, eventBus, context);
            }

            return workingState;
        }
        #endregion
    }
}