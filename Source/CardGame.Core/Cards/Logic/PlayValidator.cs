using System.Linq;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Models;
using CardGame.Core.State.Models;

namespace CardGame.Core.Cards.Logic
{
    public static class PlayValidator
    {
        public static bool CanPlay(CardInstance card, GameState state, int playerId)
        {
            var player = state.GetPlayer(playerId);

            if (!player.CanPlayCard(card.CurrentStats.BloodCost)) return false;

            if (card.Definition.Type == CardType.Unit)
            {
                bool hasEmptySlot = state.Board.Lines.Any(l => l.IsSlotEmpty(playerId));
                if (!hasEmptySlot) return false;
            }
            if (card.Definition.Type == CardType.Spell)
            {
                var onPlayedEffect = card.Definition.Effects.FirstOrDefault(e => e.Trigger == TriggerType.OnPlayed);
                if (onPlayedEffect != null)
                {
                    var firstTargetedAction = onPlayedEffect.Actions.FirstOrDefault(a => IsManualTarget(a.Target));
                    if (firstTargetedAction != null)
                    {
                        if (!HasAnyValidTarget(firstTargetedAction.Target, state, playerId))
                            return false;
                    }
                }
            }

            return true;
        }

        private static bool IsManualTarget(TargetType type)
        {
            return type == TargetType.TargetEnemyUnit ||
                   type == TargetType.TargetFriendlyUnit ||
                   type == TargetType.SelectedTarget;
        }

        private static bool HasAnyValidTarget(TargetType type, GameState state, int playerId)
        {
            int opponentId = playerId == 1 ? 2 : 1;
            var units = state.Board.GetAllUnits();

            return type switch
            {
                TargetType.TargetFriendlyUnit => units.Any(u => u.OwnerPlayerId == playerId),
                TargetType.TargetEnemyUnit => units.Any(u => u.OwnerPlayerId == opponentId),
                TargetType.SelectedTarget => units.Any(),
                _ => true
            };
        }
    }
}