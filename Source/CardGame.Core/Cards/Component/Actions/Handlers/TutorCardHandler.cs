using CardGame.Core.Application;
using CardGame.Core.Cards.Component.Actions;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Logic;
using CardGame.Core.Events; // Potrzebne do CardPlayedEvent
using CardGame.Core.Events.Interfaces;
using CardGame.Core.State.Models;
using System;
using System.Linq;

namespace CardGame.Core.Cards.Components.Actions.Handlers
{
    public class TutorCardHandler : IActionHandler
    {
        public ActionType Type => ActionType.TutorCard;

        public GameState Execute(GameState state, GameContext context, ActionData action, EffectTargets targets, int sourceId, IGameEvent gameEvent)
        {
            // Tutaj logika zależy od tego, czy mamy informację o wyborze
            if (targets.TargetPlayer != null && gameEvent is CardPlayedEvent cpe && cpe.SelectedTargetId.HasValue)
            {
                var card = targets.TargetPlayer.DrawPile.FirstOrDefault(c => c.InstanceId == cpe.SelectedTargetId.Value);
                if (card != null)
                {
                    Console.WriteLine($"[EFEKT] TUTOR: {card.Definition.Name}");
                    return state.UpdatePlayer(targets.TargetPlayer.WithCardRemovedFromDeck(card).WithCardAddedToHand(card));
                }
                else
                {
                    Console.WriteLine("[EFEKT BŁĄD] Tutor: Nie znaleziono wybranej karty w talii.");
                }
            }
            return state;
        }
    }
}