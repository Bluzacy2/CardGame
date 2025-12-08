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
    public class SacrificeUnitHandler : IActionHandler
    {
        public ActionType Type => ActionType.SacrificeUnit;

        public GameState Execute(GameState state, GameContext context, ActionData action, EffectTargets targets, int sourceId, IGameEvent gameEvent)
        {
            if (targets.TargetUnit != null)
            {
                // Sprawdzamy kto poświęca (helper z EffectTargetResolver)
                int sourceOwnerId = EffectTargetResolver.DetermineSourceOwner(state, gameEvent, sourceId);

                if (targets.TargetUnit.OwnerPlayerId != sourceOwnerId)
                {
                    Console.WriteLine("[BŁĄD ZASAD] Nie można poświęcić wrogiej jednostki!");
                    return state;
                }

                Console.WriteLine($"[EFEKT] POŚWIĘCAM: {targets.TargetUnit.Definition.Name}");
                context.Events.Publish(new UnitSacrificedEvent(targets.TargetUnit));

                // Zadaj obrażenia śmiertelne (mechanizm damage)
                var deadUnit = targets.TargetUnit.TakeDamage(9999);
                return state.UpdateBoard(state.Board.UpdateUnit(deadUnit));
            }
            return state;
        }
    }
}