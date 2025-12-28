using CardGame.Core.Application;
using CardGame.Core.Events;
using CardGame.Core.GameRules.Battle;
using CardGame.Core.GameRules.Death;
using CardGame.Core.State.Models;
using CardGame.Core.Events.Triggers;
using System.Collections.Generic;

namespace CardGame.Core.Combat
{
    public class CombatOrchestrator
    {
        private readonly BattleService _battleService = new BattleService();
        private readonly DeathResolver _deathResolver = new DeathResolver();
        private readonly TriggerSystem _triggerSystem = new TriggerSystem();

        public GameState ResolveCombatPhase(GameState currentState, EventBus eventBus, GameContext context)
        {
            var workingState = currentState;

            for (int i = 0; i < 4; i++)
            {
           
                eventBus.Publish(new PreLineCombatEvent(i));
                workingState = _triggerSystem.ProcessEvents(workingState, eventBus, context);
                workingState = _deathResolver.ResolveDeaths(workingState, eventBus, context);

             
                var line = workingState.Board.Lines[i];
                var u1 = line.Player1Unit;
                var u2 = line.Player2Unit;

                if (u1 != null && u2 != null)
                    workingState = _battleService.ResolveCombatDuel(workingState, u1, u2, i, eventBus, context);
                else if (u1 != null)
                    workingState = _battleService.ResolveBonusStrike(workingState, u1, null, i, eventBus, context);
                else if (u2 != null)
                    workingState = _battleService.ResolveBonusStrike(workingState, u2, null, i, eventBus, context);

                workingState = _deathResolver.ResolveDeaths(workingState, eventBus, context);
                workingState = _triggerSystem.ProcessEvents(workingState, eventBus, context);
            }

            return workingState;
        }
    }
}