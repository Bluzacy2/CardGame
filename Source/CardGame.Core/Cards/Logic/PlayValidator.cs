using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Models;
using CardGame.Core.State.Models;
using System.Linq;

namespace CardGame.Core.Cards.Logic
{
    /// <summary>
    /// Validates whether a card can be played in the current game state.
    /// </summary>
    public static class PlayValidator
    {
        #region Public Methods
        /// <summary>
        /// Determines whether a card can be played by the specified player in the current game state.
        /// </summary>
        /// <param name="card">The card instance to validate.</param>
        /// <param name="state">The current game state.</param>
        /// <param name="playerId">The ID of the player attempting to play the card.</param>
        /// <returns>True if the card can be played, otherwise false.</returns>
        public static bool CanPlay(CardInstance card, GameState state, int playerId)
        {
            var player = state.GetPlayer(playerId);
            if (!player.CanPlayCard(card.CurrentStats.BloodCost)) return false;

            if (card.Definition.Type == CardType.Unit)
            {
                return state.Board.Lines.Any(line => line.IsSlotEmpty(playerId));
            }

            if (card.Definition.Type == CardType.Spell)
            {
                var onPlayedEffect = card.Definition.Effects.FirstOrDefault(effect => effect.Trigger == TriggerType.OnPlayed);
                if (onPlayedEffect != null)
                {
                    var firstManualAction = onPlayedEffect.Actions.FirstOrDefault(action => IsManualTarget(action.Target) || action.Target == TargetType.SelectedTarget);
                    if (firstManualAction != null)
                    {
                        var typeToCheck = firstManualAction.Target == TargetType.SelectedTarget ? onPlayedEffect.Targeting : firstManualAction.Target;
                        if (!EffectTargetResolver.GetPotentialTargets(typeToCheck, state, card.InstanceId).Any()) return false;
                    }
                }
            }
            return true;
        }
        #endregion

        #region Private Helper Methods
        private static bool IsManualTarget(TargetType targetType) =>
            targetType == TargetType.SelectedTarget ||
            targetType == TargetType.TargetEnemyUnit ||
            targetType == TargetType.TargetFriendlyUnit ||
            targetType == TargetType.OtherFriendlyUnits;
        #endregion
    }
}