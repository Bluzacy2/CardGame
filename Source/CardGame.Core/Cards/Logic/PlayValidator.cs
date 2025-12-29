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
                return state.Board.Lines.Any(l => l.IsSlotEmpty(playerId));
            }

            if (card.Definition.Type == CardType.Spell)
            {
                var onPlayedEffect = card.Definition.Effects.FirstOrDefault(e => e.Trigger == TriggerType.OnPlayed);
                if (onPlayedEffect != null)
                {
                    var firstManualAction = onPlayedEffect.Actions.FirstOrDefault(a => IsManualTarget(a.Target) || a.Target == TargetType.SelectedTarget);
                    if (firstManualAction != null)
                    {
                        var typeToCheck = firstManualAction.Target == TargetType.SelectedTarget ? onPlayedEffect.Targeting : firstManualAction.Target;
                        if (!EffectTargetResolver.GetPotentialTargets(typeToCheck, state, card.InstanceId).Any()) return false;
                    }
                }
            }
            return true;
        }

        private static bool IsManualTarget(TargetType t) =>
            t == TargetType.SelectedTarget || t == TargetType.TargetEnemyUnit || t == TargetType.TargetFriendlyUnit || t == TargetType.OtherFriendlyUnits;
    }
}