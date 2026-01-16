using System.Collections.Generic;
using System.Linq;
using CardGame.Core.Application;
using CardGame.Core.Cards.Component.Actions;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Logic;
using CardGame.Core.Events;
using CardGame.Core.Events.Interfaces;
using CardGame.Core.State.Models;

namespace CardGame.Core.Cards.Components.Actions.Handlers
{
    public class MakeAUnitHandler : IActionHandler
    {
        public ActionType Type => ActionType.MakeAUnit;

        public GameState Execute(GameState state, GameContext context, ActionData action, EffectTargets targets, int sourceId, IGameEvent gameEvent)
        {
            // Szukamy właściciela w kolejności: Cel (Player) -> Event -> Aktywny gracz
            int ownerId = targets.TargetPlayer?.PlayerId ?? gameEvent.SourcePlayerId;
            if (ownerId == 0) ownerId = state.ActivePlayerId;

            var workingBoard = state.Board;
            int targetLine = -1;

            // 1. Logika wyboru manualnego
            if (action.StringParam == "Choose")
            {
                if (gameEvent is TargetSelectedEvent tse)
                    targetLine = tse.SelectedTargetId;
                else
                {
                    // Fallback: jeśli tylko jedno miejsce, bierz je
                    var free = Enumerable.Range(0, 4).Where(i => workingBoard.Lines[i].IsSlotEmpty(ownerId)).ToList();
                    if (free.Count == 1) targetLine = free[0];
                    else return state; // Czekaj na PendingInteraction
                }
            }
            // 2. Logika AdjacentLanes
            else if (action.StringParam == "AdjacentLanes")
            {
                int sourceLine = GetUnitLineContext(state, sourceId, gameEvent);
                if (sourceLine == -1) return state;

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
            // 3. Logika automatów
            else if (action.StringParam == "Random")
            {
                var free = Enumerable.Range(0, 4).Where(i => workingBoard.Lines[i].IsSlotEmpty(ownerId)).ToList();
                if (free.Any()) targetLine = free[context.Rng.Next(0, free.Count)];
            }
            else
            {
                // Domyślnie: Pierwsza wolna (First Free)
                for (int i = 0; i < 4; i++)
                {
                    if (workingBoard.Lines[i].IsSlotEmpty(ownerId))
                    {
                        targetLine = i;
                        break;
                    }
                }
            }

            if (targetLine == -1 || targetLine > 3) return state;

            var singleToken = context.Factory.CreateCard(action.ValueParam, ownerId);
            context.Events.Publish(new CardCreatedEvent(singleToken, ownerId));
            context.Events.Publish(new CardMovedEvent(singleToken.InstanceId, ownerId, CardZone.Deck, CardZone.Board, targetLine));
            return state.UpdateBoard(workingBoard.WithUnitPlacedAt(targetLine, ownerId, singleToken));
        }

        private int GetUnitLineContext(GameState s, int id, IGameEvent e)
        {
            if (e is UnitDiedEvent ude) return ude.LineIndex;
            if (e is UnitSacrificedEvent use) return use.LineIndex;
            for (int i = 0; i < 4; i++)
                if (s.Board.Lines[i].Player1Unit?.InstanceId == id || s.Board.Lines[i].Player2Unit?.InstanceId == id) return i;
            return -1;
        }
    }
}