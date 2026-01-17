using CardGame.Core.Application;
using CardGame.Core.Cards.Component.Actions;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Logic;
using CardGame.Core.Cards.Models;
using CardGame.Core.Events;
using CardGame.Core.Events.Interfaces;
using CardGame.Core.State.Models;
using System;
using System.Linq;

namespace CardGame.Core.Cards.Components.Actions.Handlers
{
    public class ReturnToHandHandler : IActionHandler
    {
        public ActionType Type => ActionType.ReturnToHand;

        public GameState Execute(GameState state, GameContext context, ActionData action, EffectTargets targets, int sourceId, IGameEvent gameEvent)
        {
            var workingState = state;
            foreach (var unit in targets.UnitTargets)
            {
                context.Events.Publish(new CardMovedEvent(
                        unit.InstanceId,
                        unit.OwnerPlayerId,
                        CardZone.Board,
                        CardZone.Hand));
                var freshCard = unit.MoveAndReset();
                var currentOwner = workingState.GetPlayer(unit.OwnerPlayerId);
                workingState = workingState.UpdatePlayer(currentOwner.WithCardAddedToHand(freshCard));

                var board = workingState.Board;
                for (int i = 0; i < 4; i++)
                {
                    if (board.Lines[i].Player1Unit?.InstanceId == unit.InstanceId) board = board.WithUnitPlacedAt(i, 1, null);
                    if (board.Lines[i].Player2Unit?.InstanceId == unit.InstanceId) board = board.WithUnitPlacedAt(i, 2, null);
                }
                workingState = workingState.UpdateBoard(board);
            }
            return workingState;
        }
    }
}