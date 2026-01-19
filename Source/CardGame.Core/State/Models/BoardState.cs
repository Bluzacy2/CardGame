using CardGame.Core.Cards.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CardGame.Core.State.Models
{
    /// <summary>
    /// Represents the current state of the game board, including all lines and units.
    /// </summary>
    public class BoardState
    {
        #region Properties
        /// <summary>
        /// Gets the collection of battle lines on the board.
        /// </summary>
        public IReadOnlyList<Line> Lines { get; }
        #endregion

        #region Constructors
        /// <summary>
        /// Initializes a new instance of the BoardState class with the specified lines.
        /// </summary>
        /// <param name="lines">The collection of lines to initialize the board with.</param>
        public BoardState(IEnumerable<Line> lines)
        {
            Lines = new List<Line>(lines);
        }
        #endregion

        #region Static Factory Methods
        /// <summary>
        /// Creates an empty board state with 4 empty lines.
        /// </summary>
        /// <returns>A new BoardState with all lines empty.</returns>
        public static BoardState Empty()
        {
            var emptyLines = new List<Line>();
            for (int i = 0; i < 4; i++)
            {
                emptyLines.Add(Line.Empty(i));
            }
            return new BoardState(emptyLines);
        }
        #endregion

        #region Board Modification Methods
        /// <summary>
        /// Places a unit at the specified line and player slot, returning a new board state.
        /// </summary>
        /// <param name="lineIndex">The index of the line (0-3).</param>
        /// <param name="playerId">The ID of the player (1 or 2).</param>
        /// <param name="unit">The unit to place, or null to clear the slot.</param>
        /// <returns>A new BoardState with the unit placed at the specified location.</returns>
        public BoardState WithUnitPlacedAt(int lineIndex, int playerId, CardInstance? unit)
        {
            if (unit != null && unit.OwnerPlayerId != playerId)
            {
                unit = new CardInstance(unit.InstanceId, playerId, unit.Definition);
            }
            var newLines = new List<Line>(Lines);
            var oldLine = newLines[lineIndex];
            var newLine = oldLine.WithUnitPlaced(playerId, unit);
            newLines[lineIndex] = newLine;
            return new BoardState(newLines);
        }

        /// <summary>
        /// Updates a unit on the board with a modified instance.
        /// </summary>
        /// <param name="updatedUnit">The updated unit instance.</param>
        /// <returns>A new BoardState with the unit updated, or the same state if the unit was not found.</returns>
        public BoardState UpdateUnit(CardInstance updatedUnit)
        {
            if (updatedUnit == null) return this;

            var newLines = new List<Line>();
            bool found = false;

            foreach (var line in Lines)
            {
                if (line.Player1Unit?.InstanceId == updatedUnit.InstanceId)
                {
                    newLines.Add(line.WithUnitPlaced(1, updatedUnit));
                    found = true;
                }
                else if (line.Player2Unit?.InstanceId == updatedUnit.InstanceId)
                {
                    newLines.Add(line.WithUnitPlaced(2, updatedUnit));
                    found = true;
                }
                else
                {
                    newLines.Add(line);
                }
            }

            return found ? new BoardState(newLines) : this;
        }
        #endregion

        #region Unit Query Methods
        /// <summary>
        /// Gets all units currently on the board, from all lines and both players.
        /// </summary>
        /// <returns>A list containing all units on the board.</returns>
        public List<CardInstance> GetAllUnits()
        {
            var units = new List<CardInstance>();
            foreach (var line in Lines)
            {
                if (line.Player1Unit != null) units.Add(line.Player1Unit);
                if (line.Player2Unit != null) units.Add(line.Player2Unit);
            }
            return units;
        }
        #endregion
    }
}