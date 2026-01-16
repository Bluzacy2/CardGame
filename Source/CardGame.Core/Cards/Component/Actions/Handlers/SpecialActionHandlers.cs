using CardGame.Core.Application;
using CardGame.Core.Cards.Component.Actions;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Logic;
using CardGame.Core.Events.Interfaces;
using CardGame.Core.State.Models;
using System;
using System.Linq;

namespace CardGame.Core.Cards.Components.Actions.Handlers
{
    #region Movement and Special Action Handlers

    /// <summary>
    /// Handles the BonusAttack action, allowing units to attack multiple times or out of sequence.
    /// </summary>
    public class BonusAttackHandler : IActionHandler
    {
        /// <summary>
        /// Gets the action type this handler processes.
        /// </summary>
        public ActionType Type => ActionType.BonusAttack;

        /// <summary>
        /// Executes the BonusAttack action, allowing specified units to attack again.
        /// </summary>
        public GameState Execute(GameState state, GameContext context, ActionData action, EffectTargets targets, int sourceId, IGameEvent gameEvent)
        {
            var workingState = state;
            
            foreach (var target in targets.UnitTargets)
            {
                var unit = workingState.Board.GetAllUnits().FirstOrDefault(u => u.InstanceId == target.InstanceId);
                if (unit == null)
                {
                    continue;
                }

                int lineIdx = -1;
                for (int i = 0; i < 4; i++)
                {
                    if (workingState.Board.Lines[i].Player1Unit?.InstanceId == unit.InstanceId ||
                        workingState.Board.Lines[i].Player2Unit?.InstanceId == unit.InstanceId)
                    { 
                        lineIdx = i; 
                        break; 
                    }
                }
                
                if (lineIdx == -1)
                {
                    continue;
                }

                int opponentId = unit.OwnerPlayerId == 1 ? 2 : 1;
                var defender = (opponentId == 1) ? 
                    workingState.Board.Lines[lineIdx].Player1Unit : 
                    workingState.Board.Lines[lineIdx].Player2Unit;
                
                workingState = context.Battle.ResolveBonusStrike(
                    workingState, unit, defender, lineIdx, context.Events, context);
            }
            
            return workingState;
        }
    }

    /// <summary>
    /// Handles the HealToFull action, restoring a unit's health to maximum.
    /// </summary>
    public class HealToFullHandler : IActionHandler
    {
        /// <summary>
        /// Gets the action type this handler processes.
        /// </summary>
        public ActionType Type => ActionType.HealToFull;

        /// <summary>
        /// Executes the HealToFull action, restoring target units to full health.
        /// </summary>
        public GameState Execute(GameState state, GameContext context, ActionData action, EffectTargets targets, int sourceId, IGameEvent gameEvent)
        {
            var workingState = state;
            
            foreach (var target in targets.UnitTargets)
            {
                var unitOnBoard = workingState.Board.GetAllUnits().FirstOrDefault(u => u.InstanceId == target.InstanceId);
                if (unitOnBoard == null)
                {
                    continue;
                }

                var healedUnit = unitOnBoard.Heal(999);
                workingState = workingState.UpdateBoard(workingState.Board.UpdateUnit(healedUnit));
            }
            
            return workingState;
        }
    }

    /// <summary>
    /// Handles the MoveRight action, moving units one lane to the right on the board.
    /// </summary>
    public class MoveRightHandler : IActionHandler
    {
        /// <summary>
        /// Gets the action type this handler processes.
        /// </summary>
        public ActionType Type => ActionType.MoveRight;

        /// <summary>
        /// Executes the MoveRight action, moving target units one lane to the right if possible.
        /// </summary>
        public GameState Execute(GameState state, GameContext context, ActionData action, EffectTargets targets, int sourceId, IGameEvent gameEvent)
        {
            var workingState = state;
            
            foreach (var target in targets.UnitTargets)
            {
                var unitOnBoard = workingState.Board.GetAllUnits().FirstOrDefault(u => u.InstanceId == target.InstanceId);
                if (unitOnBoard == null)
                {
                    continue;
                }

                int currentLine = -1;
                for (int i = 0; i < 4; i++)
                {
                    if (workingState.Board.Lines[i].Player1Unit?.InstanceId == unitOnBoard.InstanceId ||
                        workingState.Board.Lines[i].Player2Unit?.InstanceId == unitOnBoard.InstanceId)
                    { 
                        currentLine = i; 
                        break; 
                    }
                }

                if (currentLine != -1 && currentLine < 3)
                {
                    int nextLine = currentLine + 1;
                    if (workingState.Board.Lines[nextLine].IsSlotEmpty(unitOnBoard.OwnerPlayerId))
                    {
                        var board = workingState.Board.WithUnitPlacedAt(currentLine, unitOnBoard.OwnerPlayerId, null);
                        board = board.WithUnitPlacedAt(nextLine, unitOnBoard.OwnerPlayerId, unitOnBoard);
                        workingState = workingState.UpdateBoard(board);
                    }
                }
            }
            
            return workingState;
        }
    }

    /// <summary>
    /// Handles the GiveToOpponent action, transferring a card from one player to another.
    /// </summary>
    public class GiveToOpponentHandler : IActionHandler
    {
        /// <summary>
        /// Gets the action type this handler processes.
        /// </summary>
        public ActionType Type => ActionType.GiveToOpponent;

        /// <summary>
        /// Executes the GiveToOpponent action, transferring a card to the opposing player's hand.
        /// </summary>
        public GameState Execute(GameState state, GameContext context, ActionData action, EffectTargets targets, int sourceId, IGameEvent gameEvent)
        {
            var card = state.SpellStack.FirstOrDefault(s => s.InstanceId == sourceId)
                    ?? state.PlayerA.DiscardPile.Concat(state.PlayerB.DiscardPile)
                       .FirstOrDefault(c => c.InstanceId == sourceId);

            if (card == null)
            {
                return state;
            }

            int recipientId = card.OwnerPlayerId == 1 ? 2 : 1;
            var recipient = state.GetPlayer(recipientId);
            var newCard = context.Factory.CreateCard(int.Parse(card.Definition.Id), recipientId);

            return state.UpdatePlayer(recipient.WithCardAddedToHand(newCard));
        }
    }

    #endregion
}