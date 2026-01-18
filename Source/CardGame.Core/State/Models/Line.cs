using CardGame.Core.Cards.Models;
using System.Collections.Generic;

namespace CardGame.Core.State.Models
{
    /// <summary>
    /// Represents a single battle line on the game board that can hold units from both players.
    /// </summary>
    public class Line
    {
        #region Constructor
        /// <summary>
        /// Initializes a new instance of the Line class with specified units.
        /// </summary>
        /// <param name="index">The positional index of this line on the board (0-3).</param>
        /// <param name="player1Unit">Optional unit belonging to player 1 in this line.</param>
        /// <param name="player2Unit">Optional unit belonging to player 2 in this line.</param>
        public Line(int index, CardInstance? player1Unit, CardInstance? player2Unit)
        {
            Index = index;
            Player1Unit = player1Unit;
            Player2Unit = player2Unit;
        }
        #endregion

        #region Properties
        /// <summary>
        /// Gets the positional index of this line on the board (0-3).
        /// </summary>
        public int Index { get; }

        /// <summary>
        /// Gets the unit belonging to player 1 in this line, if any.
        /// </summary>
        public CardInstance? Player1Unit { get; }

        /// <summary>
        /// Gets the unit belonging to player 2 in this line, if any.
        /// </summary>
        public CardInstance? Player2Unit { get; }
        #endregion

        #region Static Factory Methods
        /// <summary>
        /// Creates an empty line at the specified index.
        /// </summary>
        /// <param name="index">The positional index for the empty line.</param>
        /// <returns>A new empty Line instance.</returns>
        public static Line Empty(int index) => new Line(index, null, null);
        #endregion

        #region State Query Methods
        /// <summary>
        /// Determines whether the specified player's slot in this line is empty.
        /// </summary>
        /// <param name="playerId">The ID of the player (1 or 2).</param>
        /// <returns>True if the player's slot is empty, otherwise false.</returns>
        public bool IsSlotEmpty(int playerId) => playerId == 1 ? Player1Unit == null : Player2Unit == null;

        /// <summary>
        /// Gets all units currently present in this line.
        /// </summary>
        /// <returns>A list containing all units in this line (both players).</returns>
        public List<CardInstance> GetAllUnits()
        {
            var units = new List<CardInstance>();
            if (Player1Unit != null) units.Add(Player1Unit);
            if (Player2Unit != null) units.Add(Player2Unit);
            return units;
        }
        #endregion

        #region State Modification Methods
        /// <summary>
        /// Creates a new line with a unit placed in the specified player's slot.
        /// </summary>
        /// <param name="playerId">The ID of the player placing the unit (1 or 2).</param>
        /// <param name="unit">The unit to place, or null to clear the slot.</param>
        /// <returns>A new Line instance with the updated unit placement.</returns>
        public Line WithUnitPlaced(int playerId, CardInstance? unit)
        {
            return playerId == 1
                ? new Line(Index, unit, Player2Unit)
                : new Line(Index, Player1Unit, unit);
        }

        /// <summary>
        /// Creates a new line with both player's units updated simultaneously.
        /// </summary>
        /// <param name="newPlayer1Unit">The new unit for player 1, or null to clear.</param>
        /// <param name="newPlayer2Unit">The new unit for player 2, or null to clear.</param>
        /// <returns>A new Line instance with both units updated.</returns>
        public Line UpdateUnits(CardInstance? newPlayer1Unit, CardInstance? newPlayer2Unit) =>
            new Line(Index, newPlayer1Unit, newPlayer2Unit);
        #endregion
    }
}