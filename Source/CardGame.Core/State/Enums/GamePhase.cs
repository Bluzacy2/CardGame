using System;

namespace CardGame.Core.State.Enums
{
    /// <summary>
    /// Represents the different phases of a game turn.
    /// </summary>
    public enum GamePhase
    {
        /// <summary>
        /// Undefined or initial phase.
        /// </summary>
        None,

        /// <summary>
        /// First game phase - Mulligan and drawing cards.
        /// </summary>
        Mulligan,

        /// <summary>
        /// Unit playing phase (units only).
        /// </summary>
        UnitOnly,

        /// <summary>
        /// Combined phase for playing both units and actions.
        /// </summary>
        UnitAndAction,

        /// <summary>
        /// Action playing phase (actions only).
        /// </summary>
        ActionOnly,

        /// <summary>
        /// Combat resolution phase.
        /// </summary>
        Combat,

        /// <summary>
        /// End of turn phase.
        /// </summary>
        EndTurn
    }
}