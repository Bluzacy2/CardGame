using System;
using System.Linq;
using CardGame.Core.Application;
using CardGame.Core.Cards.Component.Actions;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Logic;
using CardGame.Core.Events.Interfaces;
using CardGame.Core.State.Models;

namespace CardGame.Core.Cards.Components.Actions.Handlers
{
    public class BonusAttackHandler : IActionHandler
    {
        public ActionType Type => ActionType.BonusAttack;
        public GameState Execute(GameState state, GameContext context, ActionData action, EffectTargets targets, int sourceId, IGameEvent gameEvent)
        {
            var workingState = state;
            foreach (var target in targets.UnitTargets)
            {
                var unit = workingState.Board.GetAllUnits().FirstOrDefault(u => u.InstanceId == target.InstanceId);
                if (unit == null) continue;

                int lineIdx = -1;
                for (int i = 0; i < 4; i++)
                {
                    if (workingState.Board.Lines[i].Player1Unit?.InstanceId == unit.InstanceId ||
                        workingState.Board.Lines[i].Player2Unit?.InstanceId == unit.InstanceId)
                    { lineIdx = i; break; }
                }
                if (lineIdx == -1) continue;

                int opponentId = unit.OwnerPlayerId == 1 ? 2 : 1;
                var defender = (opponentId == 1) ? workingState.Board.Lines[lineIdx].Player1Unit : workingState.Board.Lines[lineIdx].Player2Unit;
                workingState = context.Battle.ResolveBonusStrike(workingState, unit, defender, lineIdx, context.Events, context);
            }
            return workingState;
        }
    }

    public class HealToFullHandler : IActionHandler
    {
        public ActionType Type => ActionType.HealToFull;
        public GameState Execute(GameState state, GameContext context, ActionData action, EffectTargets targets, int sourceId, IGameEvent gameEvent)
        {
            var workingState = state;
            foreach (var target in targets.UnitTargets)
            {
                var unitOnBoard = workingState.Board.GetAllUnits().FirstOrDefault(u => u.InstanceId == target.InstanceId);
                if (unitOnBoard == null) continue;

                var healed = unitOnBoard.Heal(999);
                workingState = workingState.UpdateBoard(workingState.Board.UpdateUnit(healed));
            }
            return workingState;
        }
    }

    public class MoveRightHandler : IActionHandler
    {
        public ActionType Type => ActionType.MoveRight;
        public GameState Execute(GameState state, GameContext context, ActionData action, EffectTargets targets, int sourceId, IGameEvent gameEvent)
        {
            var workingState = state;
            foreach (var target in targets.UnitTargets)
            {
                var unitOnBoard = workingState.Board.GetAllUnits().FirstOrDefault(u => u.InstanceId == target.InstanceId);
                if (unitOnBoard == null) continue;

                int currentLine = -1;
                for (int i = 0; i < 4; i++)
                {
                    if (workingState.Board.Lines[i].Player1Unit?.InstanceId == unitOnBoard.InstanceId ||
                        workingState.Board.Lines[i].Player2Unit?.InstanceId == unitOnBoard.InstanceId)
                    { currentLine = i; break; }
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

    public class GiveToOpponentHandler : IActionHandler
    {
        public ActionType Type => ActionType.GiveToOpponent;
        public GameState Execute(GameState state, GameContext context, ActionData action, EffectTargets targets, int sourceId, IGameEvent gameEvent)
        {
            var card = state.SpellStack.FirstOrDefault(s => s.InstanceId == sourceId)
                    ?? state.PlayerA.DiscardPile.Concat(state.PlayerB.DiscardPile).FirstOrDefault(c => c.InstanceId == sourceId);

            if (card == null) return state;

            int recipientId = card.OwnerPlayerId == 1 ? 2 : 1;
            var recipient = state.GetPlayer(recipientId);
            var newCard = context.Factory.CreateCard(int.Parse(card.Definition.Id), recipientId);

            return state.UpdatePlayer(recipient.WithCardAddedToHand(newCard));
        }
    }
}