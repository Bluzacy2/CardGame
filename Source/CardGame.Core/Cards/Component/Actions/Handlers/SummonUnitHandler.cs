using CardGame.Core.Application;
using CardGame.Core.Cards.Component.Actions;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Logic;
using CardGame.Core.Events;
using CardGame.Core.Events.Interfaces;
using CardGame.Core.State.Models;
using System;
using System.Linq;

namespace CardGame.Core.Cards.Components.Actions.Handlers
{
    /// <summary>
    /// Handles the SummonUnit action, which places a unit card from hand onto the board.
    /// </summary>
    public class SummonUnitHandler : IActionHandler
    {
        /// <summary>
        /// Gets the action type this handler processes.
        /// </summary>
        public ActionType Type => ActionType.SummonUnit;

        /// <summary>
        /// Executes the SummonUnit action to place a unit card from hand onto the board.
        /// </summary>
        /// <param name="state">The current game state.</param>
        /// <param name="context">The game context providing access to services.</param>
        /// <param name="action">The action data defining the summon effect.</param>
        /// <param name="targets">The resolved targets for the action.</param>
        /// <param name="sourceId">The ID of the card that initiated the action.</param>
        /// <param name="gameEvent">The event that triggered this action.</param>
        /// <returns>The updated game state after summoning the unit.</returns>
        public GameState Execute(
            GameState state,
            GameContext context,
            ActionData action,
            EffectTargets targets,
            int sourceId,
            IGameEvent gameEvent)
        {
            int lineIdx = -1;

            #region Lane Index Determination

            if (gameEvent is UnitDiedEvent death)
            {
                lineIdx = death.LineIndex;
            }
            else if (gameEvent is UnitSacrificedEvent sacrifice)
            {
                lineIdx = sacrifice.LineIndex;
            }

            if (lineIdx == -1)
            {
                lineIdx = action.ValueParam;
            }

            #endregion

            var unitToSummon = targets.TargetUnit;
            if (unitToSummon == null || lineIdx < 0)
            {
                return state;
            }

            // GET THE FRESHEST PLAYER FROM STATE (fixes WomboCombo)
            var owner = state.GetPlayer(unitToSummon.OwnerPlayerId);

            if (!state.Board.Lines[lineIdx].IsSlotEmpty(owner.PlayerId))
            {
                return state;
            }

            var cardInHand = owner.Hand.FirstOrDefault(c => c.InstanceId == unitToSummon.InstanceId);
            if (cardInHand == null)
            {
                return state;
            }

            var newOwner = owner.WithCardRemovedFromHand(cardInHand);
            return state.UpdatePlayer(newOwner)
                       .UpdateBoard(state.Board.WithUnitPlacedAt(lineIdx, owner.PlayerId, cardInHand));
        }
    }
}