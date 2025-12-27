
using CardGame.Core.Application;
using CardGame.Core.Events;
using CardGame.Core.GameRules.Battle;
using CardGame.Core.GameRules.Damage;
using CardGame.Core.GameRules.Death;
using CardGame.Core.State.Models;
using System.Collections.Generic;

namespace CardGame.Core.Combat
{
    public class CombatOrchestrator
    {
        private readonly BattleService _battleService = new BattleService();
        private readonly DeathResolver _deathResolver;

        public CombatOrchestrator()
        {
            _deathResolver = new DeathResolver();
        }

        public GameState ResolveCombatPhase(GameState currentState, EventBus eventBus, GameContext context)
        {
            var workingState = currentState;

            for (int i = 0; i < 4; i++)
            {
                var line = workingState.Board.Lines[i];
                var u1 = line.Player1Unit;
                var u2 = line.Player2Unit;

                if (u1 != null && u2 != null)
                    workingState = _battleService.ResolveCombatDuel(workingState, u1, u2, i, eventBus, context);
                else if (u1 != null)
                    workingState = _battleService.ResolveBonusStrike(workingState, u1, null, i, eventBus, context);
                else if (u2 != null)
                    workingState = _battleService.ResolveBonusStrike(workingState, u2, null, i, eventBus, context);
            }

            return _deathResolver.ResolveDeaths(workingState, eventBus);
        }
    }
}
