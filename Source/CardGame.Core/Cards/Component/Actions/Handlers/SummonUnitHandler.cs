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
            // Summon najczęściej dzieje się jako Deathrattle (UnitDiedEvent), żeby wskoczyć na miejsce trupa
            if (gameEvent is UnitDiedEvent deathEvt && targets.TargetUnit != null)
            {
                var owner = state.GetPlayer(targets.TargetUnit.OwnerPlayerId);
                // Usuwamy z ręki (jeśli jest w ręce)
                var newOwner = owner.WithCardRemovedFromHand(targets.TargetUnit);
                state = state.UpdatePlayer(newOwner);

                // Stawiamy na linii zmarłego
                state = state.UpdateBoard(state.Board.WithUnitPlacedAt(deathEvt.LineIndex, owner.PlayerId, targets.TargetUnit));
                Console.WriteLine($"[EFEKT] Summon: {targets.TargetUnit.Definition.Name} na linię {deathEvt.LineIndex}");
            }
            // Można tu dodać logikę dla innych eventów, jeśli summon miałby być np. przy zagraniu innej karty
            return state;
        }
    }
}