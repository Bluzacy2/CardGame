using CardGame.Core.Application;
using CardGame.Core.Cards.Component.Actions;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Logic;
using CardGame.Core.Events;
using CardGame.Core.Events.Interfaces;
using CardGame.Core.State.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CardGame.Core.Cards.Components.Actions.Handlers
{
    #region Token Creation Handlers

    /// <summary>
    /// Handles the MakeAUnit action, creating token units on the board with various placement rules.
    /// </summary>
    public class MakeAUnitHandler : IActionHandler
    {
        /// <summary>
        /// Gets the action type this handler processes.
        /// </summary>
        public ActionType Type => ActionType.MakeAUnit;

        /// <summary>
        /// Executes the MakeAUnit action, creating token units on the board based on placement rules.
        /// </summary>
        public GameState Execute(
            GameState state,
            GameContext context,
            ActionData action,
            EffectTargets targets,
            int sourceId,
            IGameEvent gameEvent)
        {
            // Find owner in order: Target (Player) -> Event -> Active player
            int ownerId = targets.TargetPlayer?.PlayerId ?? gameEvent.SourcePlayerId;
            if (ownerId == 0)
            {
                ownerId = state.ActivePlayerId;
            }

            var workingBoard = state.Board;
            int targetLine = -1;

            // 1. Manual choice logic
            if (action.StringParam == "Choose")
            {
                if (gameEvent is TargetSelectedEvent targetSelectedEvent)
                {
                    targetLine = targetSelectedEvent.SelectedTargetId;
                }
                else
                {
                    // Fallback: if only one free slot, take it
                    var freeLines = Enumerable.Range(0, 4)
                        .Where(i => workingBoard.Lines[i].IsSlotEmpty(ownerId))
                        .ToList();

                    if (freeLines.Count == 1)
                    {
                        targetLine = freeLines[0];
                    }
                    else
                    {
                        return state; // Wait for PendingInteraction
                    }
                }
            }
            // 2. Adjacent lanes logic
            else if (action.StringParam == "AdjacentLanes")
            {
                int sourceLine = GetUnitLineContext(state, sourceId, gameEvent);
                if (sourceLine == -1)
                {
                    return state;
                }

                var nextBoard = workingBoard;
                foreach (int idx in new[] { sourceLine - 1, sourceLine + 1 })
                {
                    if (idx >= 0 && idx < 4 && nextBoard.Lines[idx].IsSlotEmpty(ownerId))
                    {
                        var token = context.Factory.CreateCard(action.ValueParam, ownerId);
                        nextBoard = nextBoard.WithUnitPlacedAt(idx, ownerId, token);
                    }
                }

                return state.UpdateBoard(nextBoard);
            }
            // 3. Random placement logic
            else if (action.StringParam == "Random")
            {
                var freeLines = Enumerable.Range(0, 4)
                    .Where(i => workingBoard.Lines[i].IsSlotEmpty(ownerId))
                    .ToList();

                if (freeLines.Any())
                {
                    targetLine = freeLines[context.Rng.Next(0, freeLines.Count)];
                }
            }
            else
            {
                // Default: First available free slot
                for (int i = 0; i < 4; i++)
                {
                    if (workingBoard.Lines[i].IsSlotEmpty(ownerId))
                    {
                        targetLine = i;
                        break;
                    }
                }
            }

            if (targetLine == -1 || targetLine > 3)
            {
                return state;
            }

            var singleToken = context.Factory.CreateCard(action.ValueParam, ownerId);
            context.Events.Publish(new CardCreatedEvent(singleToken, ownerId));
            context.Events.Publish(new CardMovedEvent(
                singleToken.InstanceId,
                ownerId,
                CardZone.Deck,
                CardZone.Board,
                targetLine));

            return state.UpdateBoard(workingBoard.WithUnitPlacedAt(targetLine, ownerId, singleToken));
        }

        #region Private Helper Methods

        /// <summary>
        /// Gets the board lane index where a unit is located based on event context.
        /// </summary>
        private int GetUnitLineContext(GameState state, int unitId, IGameEvent gameEvent)
        {
            if (gameEvent is UnitDiedEvent unitDiedEvent)
            {
                return unitDiedEvent.LineIndex;
            }

            if (gameEvent is UnitSacrificedEvent unitSacrificedEvent)
            {
                return unitSacrificedEvent.LineIndex;
            }

            for (int i = 0; i < 4; i++)
            {
                if (state.Board.Lines[i].Player1Unit?.InstanceId == unitId ||
                    state.Board.Lines[i].Player2Unit?.InstanceId == unitId)
                {
                    return i;
                }
            }

            return -1;
        }

        #endregion
    }

    #endregion
}