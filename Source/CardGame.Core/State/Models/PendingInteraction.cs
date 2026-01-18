using CardGame.Core.Cards.Data;
using System.Collections.Generic;

namespace CardGame.Core.State.Models
{
    /// <summary>
    /// Represents a pending interaction that requires player input, such as target selection for a spell or ability.
    /// </summary>
    public class PendingInteraction
    {
        #region Properties
        /// <summary>
        /// Gets the instance ID of the card executing the effect.
        /// </summary>
        public int SourceCardInstanceId { get; }

        /// <summary>
        /// Gets the index of the effect within the card's Effects list (0, 1, 2...).
        /// </summary>
        public int EffectIndex { get; }

        /// <summary>
        /// Gets the index of the action within the effect (0, 1, 2...).
        /// </summary>
        public int ActionIndex { get; }

        /// <summary>
        /// Gets the type of target the UI should look for.
        /// </summary>
        public TargetType RequiredTargetType { get; }
        
        public int PlayerIdWhoChooses { get; }

        /// <summary>
        /// Gets the list of options available for this interaction.
        /// </summary>
        public IReadOnlyList<string> Options { get; }

        #endregion

        #region Constructor
        /// <summary>
        /// Initializes a new instance of the PendingInteraction class.
        /// </summary>
        /// <param name="sourceId">The instance ID of the card executing the effect.</param>
        /// <param name="effectIndex">The index of the effect within the card's Effects list.</param>
        /// <param name="actionIndex">The index of the action within the effect.</param>
        /// <param name="targetType">The type of target the UI should look for.</param>
        /// <param name="options">Optional list of options for the interaction.</param>
        public PendingInteraction(int sourceId, int effectIndex, int actionIndex, TargetType targetType, int playerIdWhoChooses, IEnumerable<string>? options = null)
        {
            SourceCardInstanceId = sourceId;
            EffectIndex = effectIndex;
            ActionIndex = actionIndex;
            RequiredTargetType = targetType;
            PlayerIdWhoChooses = playerIdWhoChooses;
            Options = options != null ? new List<string>(options) : new List<string>();
        }
        #endregion
    }
}