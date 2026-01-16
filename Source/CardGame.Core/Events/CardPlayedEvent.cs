using CardGame.Core.Cards.Models;
using CardGame.Core.Events.Interfaces;

namespace CardGame.Core.Events
{
    #region Card Events

    /// <summary>
    /// Represents an event that occurs when a card is played from hand to the board.
    /// </summary>
    public class CardPlayedEvent : IGameEvent
    {
        /// <summary>
        /// Initializes a new instance of the CardPlayedEvent class.
        /// </summary>
        /// <param name="playerId">The ID of the player who played the card.</param>
        /// <param name="card">The card instance that was played.</param>
        /// <param name="lineIndex">The optional board lane index for unit placement.</param>
        /// <param name="selectedTargetId">The optional target ID for card effects.</param>
        public CardPlayedEvent(int playerId, CardInstance card, int? lineIndex, int? selectedTargetId)
        {
            PlayerId = playerId;
            Card = card;
            LineIndex = lineIndex;
            SelectedTargetId = selectedTargetId;
        }

        /// <summary>
        /// Gets the ID of the player who played the card.
        /// </summary>
        public int PlayerId { get; }

        /// <summary>
        /// Gets the ID of the player who played the card.
        /// </summary>
        public int SourcePlayerId => PlayerId;

        /// <summary>
        /// Gets the card instance that was played.
        /// </summary>
        public CardInstance Card { get; }

        /// <summary>
        /// Gets the optional board lane index where the unit was placed.
        /// </summary>
        public int? LineIndex { get; }

        /// <summary>
        /// Gets the optional ID of a selected target for the card effect.
        /// </summary>
        public int? SelectedTargetId { get; }
    }

    #endregion
}