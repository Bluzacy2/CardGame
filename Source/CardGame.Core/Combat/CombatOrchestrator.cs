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

            for (int i = workingState.CombatLineIndex; i < 4; i++)
            {
                // --- STEP 0: PUBLISH PRE-COMBAT EVENT ---
                if (workingState.CombatStep == 0)
                {
                    eventBus.Publish(new PreLineCombatEvent(i));
                    workingState = workingState.With(combatStep: 1);
                }

                // --- STEP 1: PROCESS PRE-COMBAT TRIGGERS (Occultist) ---
                if (workingState.CombatStep == 1)
                {
                    workingState = _triggerSystem.ProcessEvents(workingState, eventBus, context);
                    if (workingState.PendingInteraction != null)
                        return workingState.With(combatLineIndex: i, combatStep: 1);
                    if (workingState.PlayerA.Health <= 0 || workingState.PlayerB.Health <= 0)
                        return workingState.With(combatLineIndex: 4, combatStep: 0);

                    workingState = workingState.With(combatStep: 2);
                }

                // --- STEP 2: RESOLVE DUEL / STRIKE ---
                if (workingState.CombatStep == 2)
                {
                    workingState = _deathResolver.ResolveDeaths(workingState, eventBus, context);

                    var line = workingState.Board.Lines[i];
                    var p1 = line.Player1Unit;
                    var p2 = line.Player2Unit;

                    if (p1 != null && p2 != null)
                        workingState = _battleService.ResolveCombatDuel(workingState, p1, p2, i, eventBus, context);
                    else if (p1 != null)
                        workingState = _battleService.ResolveBonusStrike(workingState, p1, null, i, eventBus, context);
                    else if (p2 != null)
                        workingState = _battleService.ResolveBonusStrike(workingState, p2, null, i, eventBus, context);

                    // IMMEDIATE LETHAL CHECK (After duel)
                    if (workingState.PlayerA.Health <= 0 || workingState.PlayerB.Health <= 0)
                        return workingState.With(combatLineIndex: 4, combatStep: 0);

                    workingState = workingState.With(combatStep: 3);
                }

                // --- STEP 3: POST-COMBAT TRIGGERS (Trap / Death Effects) ---
                if (workingState.CombatStep == 3)
                {
                    workingState = _deathResolver.ResolveDeaths(workingState, eventBus, context);
                    workingState = _triggerSystem.ProcessEvents(workingState, eventBus, context);

                    if (workingState.PendingInteraction != null)
                        return workingState.With(combatLineIndex: i, combatStep: 3);

                    // LETHAL CHECK (After post-combat triggers)
                    if (workingState.PlayerA.Health <= 0 || workingState.PlayerB.Health <= 0)
                        return workingState.With(combatLineIndex: 4, combatStep: 0);

                    // Lane complete!
                    workingState = workingState.With(combatLineIndex: i + 1, combatStep: 0);
                }
            }

            return workingState.With(combatLineIndex: 4, combatStep: 0);
        }
        #endregion
    }
}