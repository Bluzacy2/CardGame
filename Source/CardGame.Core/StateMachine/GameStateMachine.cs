using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using CardGame.Core.State.Enums;
using CardGame.Core.StateMachine.Interfaces;
using CardGame.Core.StateMachine.Phases;

namespace CardGame.Core.StateMachine
{
    public class GameStateMachine
    {
        public IPhaseState GetStateForPhase(GamePhase phase)
        {
            switch(phase)
            {
                case GamePhase.UnitOnly:
                    return new UnitPhaseState();
                case GamePhase.UnitAndAction:
                    return new BothPhaseState();
                case GamePhase.ActionOnly:
                    return new ActionPhaseState();
                case GamePhase.Combat:
                    return new CombatPhaseState();
                default:
                    throw new ArgumentException($"Nieobsługiwana faza gry: {phase}");
            }
        }
    }
}
