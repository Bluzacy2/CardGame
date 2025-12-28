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
    public class SummonUnitHandler : IActionHandler
    {
        public ActionType Type => ActionType.SummonUnit;

        public GameState Execute(GameState state, GameContext context, ActionData action, EffectTargets targets, int sourceId, IGameEvent gameEvent)
        {
            int lineIdx = -1;
            if (gameEvent is UnitDiedEvent death) lineIdx = death.LineIndex;
            else if (gameEvent is UnitSacrificedEvent sac) lineIdx = sac.LineIndex;
            if (lineIdx == -1) lineIdx = action.ValueParam;

            var unitToSummon = targets.TargetUnit;
            if (unitToSummon == null || lineIdx < 0) return state;

            // POBIERZ NAJŚWIEŻSZEGO GRACZA ZE STANU (naprawia WomboCombo)
            var owner = state.GetPlayer(unitToSummon.OwnerPlayerId);
            if (!state.Board.Lines[lineIdx].IsSlotEmpty(owner.PlayerId)) return state;

            var cardInHand = owner.Hand.FirstOrDefault(c => c.InstanceId == unitToSummon.InstanceId);
            if (cardInHand == null) return state;

            var newOwner = owner.WithCardRemovedFromHand(cardInHand);
            return state.UpdatePlayer(newOwner).UpdateBoard(state.Board.WithUnitPlacedAt(lineIdx, owner.PlayerId, cardInHand));
        }
    }
}