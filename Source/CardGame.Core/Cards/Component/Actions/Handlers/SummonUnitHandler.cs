using CardGame.Core.Application;
using CardGame.Core.Cards.Component.Actions;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Logic;
using CardGame.Core.Events;
using CardGame.Core.Events.Interfaces;
using CardGame.Core.State.Models;
using System;

namespace CardGame.Core.Cards.Components.Actions.Handlers
{
    public class SummonUnitHandler : IActionHandler
    {
        public ActionType Type => ActionType.SummonUnit;

        public GameState Execute(GameState state, GameContext context, ActionData action, EffectTargets targets, int sourceId, IGameEvent gameEvent)
        {
            // KROK 1: Szukamy linii w zdarzeniu (Priorytet dla Corpse Eater / Deathrattle)
            int lineIdx = -1;
            if (gameEvent is UnitDiedEvent death) lineIdx = death.LineIndex;
            else if (gameEvent is UnitSacrificedEvent sac) lineIdx = sac.LineIndex;

            // KROK 2: Jeśli event nie podał linii (np. rzucamy czar przyzwania), bierzemy ją z JSON
            // Ale tylko jeśli ValueParam jest ustawione (czyli np. nie jest domyślnym 0 przy Summonach reaktywnych)
            if (lineIdx == -1)
            {
                lineIdx = action.ValueParam;
            }

            if (lineIdx != -1 && targets.TargetUnit != null)
            {
                var owner = state.GetPlayer(targets.TargetUnit.OwnerPlayerId);

                // KROK 3: Sprawdzenie wolnego miejsca
                if (!state.Board.Lines[lineIdx].IsSlotEmpty(owner.PlayerId))
                {
                    Console.WriteLine($"[SUMMON DEBUG] Linia {lineIdx} zajęta. Summon przerwany.");
                    return state;
                }

                var newOwner = owner.WithCardRemovedFromHand(targets.TargetUnit);
                var workingState = state.UpdatePlayer(newOwner);

                Console.WriteLine($"[EFEKT] Summon: {targets.TargetUnit.Definition.Name} na linię {lineIdx}");
                return workingState.UpdateBoard(workingState.Board.WithUnitPlacedAt(lineIdx, owner.PlayerId, targets.TargetUnit));
            }
            return state;
        }
    }
}