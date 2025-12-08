using CardGame.Core.Application;
using CardGame.Core.Cards.Component.Actions;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Logic;
using CardGame.Core.Cards.Models;
using CardGame.Core.Events.Interfaces;
using CardGame.Core.State.Models;
using System;

namespace CardGame.Core.Cards.Components.Actions.Handlers
{
    public class ReturnToHandHandler : IActionHandler
    {
        public ActionType Type => ActionType.ReturnToHand;

        public GameState Execute(GameState state, GameContext context, ActionData action, EffectTargets targets, int sourceId, IGameEvent gameEvent)
        {
            if (targets.TargetUnit != null)
            {
                var unit = targets.TargetUnit;
                // Reset statystyk do bazowych
                var freshCard = new CardInstance(unit.InstanceId, unit.OwnerPlayerId, unit.Definition);

                var owner = state.GetPlayer(unit.OwnerPlayerId);
                state = state.UpdatePlayer(owner.WithCardAddedToHand(freshCard));

                // Usuń z planszy (przeszukujemy linie)
                for (int i = 0; i < 4; i++)
                {
                    var l = state.Board.Lines[i];
                    if (l.Player1Unit?.InstanceId == unit.InstanceId)
                        state = state.UpdateBoard(state.Board.WithUnitPlacedAt(i, 1, null));
                    if (l.Player2Unit?.InstanceId == unit.InstanceId)
                        state = state.UpdateBoard(state.Board.WithUnitPlacedAt(i, 2, null));
                }
                Console.WriteLine($"[EFEKT] ReturnToHand: {unit.Definition.Name}");
            }
            return state;
        }
    }
}