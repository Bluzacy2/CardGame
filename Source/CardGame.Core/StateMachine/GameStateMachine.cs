using System;
using CardGame.Core.State.Enums;
using CardGame.Core.StateMachine.Interfaces;
using CardGame.Core.StateMachine.Phases;

namespace CardGame.Core.StateMachine
{
    /// <summary>
    /// Factory that returns the appropriate phase state implementation for a given game phase.
    /// </summary>
    public class GameStateMachine
    {
        #region Phase State Factory
        /// <summary>
        /// Gets the phase state implementation corresponding to the specified game phase.
        /// </summary>
        /// <param name="phase">The game phase to get the state for.</param>
        /// <returns>An IPhaseState implementation that handles the specified phase.</returns>
        /// <exception cref="ArgumentException">Thrown when an unsupported game phase is provided.</exception>
        public IPhaseState GetStateForPhase(GamePhase phase)
        {
            return phase switch
            {
                GamePhase.Mulligan => new MulliganPhaseState(),
                GamePhase.UnitOnly => new UnitPhaseState(),
                GamePhase.UnitAndAction => new BothPhaseState(),
                GamePhase.ActionOnly => new ActionPhaseState(),
                GamePhase.Combat => new CombatPhaseState(),
                _ => throw new ArgumentException($"Unsupported game phase: {phase}")
            };
        }
        #endregion
    }
}