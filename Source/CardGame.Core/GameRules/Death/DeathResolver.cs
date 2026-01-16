using CardGame.Core.Application;
using CardGame.Core.Cards.Models;
using CardGame.Core.Events;
using CardGame.Core.State.Models;
using System;
using System.Linq;

namespace CardGame.Core.GameRules.Death
{
    /// <summary>
    /// Handles unit death resolution, including death prevention checks and cleanup operations.
    /// </summary>
    public class DeathResolver
    {
        #region Public Methods
        /// <summary>
        /// Iteratively resolves unit deaths in the current game state until no more deaths are pending.
        /// </summary>
        /// <param name="state">The current game state to process deaths for.</param>
        /// <param name="eventBus">Event bus for publishing death-related events.</param>
        /// <param name="context">Game context for death prevention checks.</param>
        /// <returns>The updated game state after resolving all pending deaths.</returns>
        public GameState ResolveDeaths(GameState state, EventBus eventBus, GameContext context)
        {
            var workingState = state;
            bool changed = true;

            while (changed)
            {
                changed = false;
                var allUnits = workingState.Board.GetAllUnits();

                var unitToKill = allUnits.FirstOrDefault(unit => unit.CurrentStats.Health <= 0);

                if (unitToKill != null)
                {
                    int lineIndex = -1;
                    for (int i = 0; i < 4; i++)
                    {
                        if (workingState.Board.Lines[i].Player1Unit?.InstanceId == unitToKill.InstanceId ||
                            workingState.Board.Lines[i].Player2Unit?.InstanceId == unitToKill.InstanceId)
                        {
                            lineIndex = i;
                            break;
                        }
                    }

                    var history = eventBus.GetHistory().ToList();

                    bool isSacrifice = history.OfType<UnitSacrificedEvent>()
                        .Any(eventItem => eventItem.Unit.InstanceId == unitToKill.InstanceId);

                    if (context.Keywords.TryPreventDeath(ref workingState, unitToKill, context, isSacrifice))
                    {
                        changed = true;
                        continue;
                    }

                    var lastDamageEvent = history.OfType<UnitDamagedEvent>()
                        .LastOrDefault(eventItem => eventItem.Unit?.InstanceId == unitToKill.InstanceId && eventItem.Source != null);

                    workingState = KillInstantly(workingState, unitToKill, lineIndex, eventBus, lastDamageEvent?.Source?.InstanceId);
                    changed = true;
                }
            }
            return workingState;
        }

        /// <summary>
        /// Immediately kills a unit, removes it from the board, and moves it to the owner's discard pile.
        /// </summary>
        /// <param name="state">The current game state.</param>
        /// <param name="unit">The unit to kill.</param>
        /// <param name="lineIndex">The line index where the unit is located, or -1 if unknown.</param>
        /// <param name="events">Event bus for publishing death events.</param>
        /// <param name="killerId">Optional ID of the unit that killed this unit.</param>
        /// <returns>The updated game state after killing the unit.</returns>
        public GameState KillInstantly(GameState state, CardInstance unit, int lineIndex, EventBus events, int? killerId)
        {
            var board = state.Board;

            for (int i = 0; i < 4; i++)
            {
                if (board.Lines[i].Player1Unit?.InstanceId == unit.InstanceId)
                    board = board.WithUnitPlacedAt(i, 1, null);
                else if (board.Lines[i].Player2Unit?.InstanceId == unit.InstanceId)
                    board = board.WithUnitPlacedAt(i, 2, null);
            }

            var owner = state.GetPlayer(unit.OwnerPlayerId);
            var newState = state.UpdateBoard(board).UpdatePlayer(owner.WithCardAddedToDiscard(unit));

            events.Publish(new UnitDiedEvent(unit, lineIndex, killerId));
            events.Publish(new CardMovedEvent(unit.InstanceId, unit.OwnerPlayerId, CardZone.Board, CardZone.Graveyard));

            return newState;
        }
        #endregion
    }
}