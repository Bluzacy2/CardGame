using System;
using System.Collections.Generic;
using System.Linq;
using CardGame.Core.Application;
using CardGame.Core.Cards.Models;
using CardGame.Core.State.Enums;

namespace CardGame.Core.State.Models
{
    /// <summary>
    /// Represents the complete state of a game, including board, players, and game phase information.
    /// </summary>
    public class GameState
    {
        #region Properties
        /// <summary>
        /// Gets the current turn number.
        /// </summary>
        public int TurnNumber { get; }

        /// <summary>
        /// Gets the current phase of the game (Mulligan, Main, Combat, etc.).
        /// </summary>
        public GamePhase CurrentPhase { get; }

        /// <summary>
        /// Gets the ID of the player whose turn it is (1 or 2).
        /// </summary>
        public int ActivePlayerId { get; }

        /// <summary>
        /// Gets the current state of the game board.
        /// </summary>
        public BoardState Board { get; }

        /// <summary>
        /// Gets the state of player A.
        /// </summary>
        public PlayerState PlayerA { get; }

        /// <summary>
        /// Gets the state of player B.
        /// </summary>
        public PlayerState PlayerB { get; }

        /// <summary>
        /// Gets the list of player IDs who have confirmed their mulligan.
        /// </summary>
        public IReadOnlyList<int> PlayersReady { get; }

        /// <summary>
        /// Gets the current pending interaction, if any (e.g., target selection).
        /// </summary>
        public PendingInteraction? PendingInteraction { get; }

        /// <summary>
        /// Gets the current spell stack (for resolving spell chains).
        /// </summary>
        public IReadOnlyList<CardInstance> SpellStack { get; }

        /// <summary>
        /// Gets the ID of the player who started the current round (1 or 2).
        /// </summary>
        public int RoundStartingPlayerId { get; }

        public int CombatLineIndex { get; }
        public int CombatStep { get; } 
        #endregion

        #region Constructor
        /// <summary>
        /// Initializes a new instance of the GameState class.
        /// </summary>
        /// <param name="turnNumber">The current turn number.</param>
        /// <param name="currentPhase">The current phase of the game.</param>
        /// <param name="activePlayerId">The ID of the active player.</param>
        /// <param name="board">The current board state.</param>
        /// <param name="playerA">The state of player A.</param>
        /// <param name="playerB">The state of player B.</param>
        /// <param name="playersReady">Optional list of players who are ready.</param>
        /// <param name="pendingInteraction">Optional pending interaction.</param>
        /// <param name="spellStack">Optional current spell stack.</param>
        /// <param name="roundStartingPlayerId">The ID of the player who started this round.</param>
        public GameState(
            int turnNumber,
            GamePhase currentPhase,
            int activePlayerId,
            BoardState board,
            PlayerState playerA,
            PlayerState playerB,
            IEnumerable<int>? playersReady = null,
            PendingInteraction? pendingInteraction = null,
            IEnumerable<CardInstance>? spellStack = null,
            int roundStartingPlayerId = 1,
            int combatLineIndex = 0,
            int combatStep = 0)
        {
            TurnNumber = turnNumber;
            CurrentPhase = currentPhase;
            ActivePlayerId = activePlayerId;
            Board = board;
            PlayerA = playerA;
            PlayerB = playerB;
            PlayersReady = playersReady != null ? new List<int>(playersReady) : new List<int>();
            PendingInteraction = pendingInteraction;
            SpellStack = spellStack != null ? new List<CardInstance>(spellStack) : new List<CardInstance>();
            RoundStartingPlayerId = roundStartingPlayerId;
            CombatLineIndex = combatLineIndex;
            CombatStep = combatStep;
        }
        #endregion

        #region Initial State Creation
        /// <summary>
        /// Creates the initial game state for a new game.
        /// </summary>
        /// <param name="startingPlayerId">The ID of the player who goes first (1 or 2).</param>
        /// <param name="deckA">The deck for player A.</param>
        /// <param name="deckB">The deck for player B.</param>
        /// <param name="rng">The random number generator for deck shuffling.</param>
        /// <returns>A new GameState representing the start of a game.</returns>
        public static GameState Initial(int startingPlayerId, List<CardInstance> deckA, List<CardInstance> deckB, DeterministicRng rng)
        {
            var playerA = PlayerState.Initial(1, deckA).WithShuffledDeck(rng);
            var playerB = PlayerState.Initial(2, deckB).WithShuffledDeck(rng);

            for (int i = 0; i < 4; i++) playerA = playerA.WithCardDrawn();
            for (int i = 0; i < 4; i++) playerB = playerB.WithCardDrawn();

            return new GameState(
                turnNumber: 1,
                currentPhase: GamePhase.Mulligan,
                activePlayerId: startingPlayerId,
                board: BoardState.Empty(),
                playerA: playerA,
                playerB: playerB,
                roundStartingPlayerId: startingPlayerId,
                combatLineIndex: 0,
                combatStep: 0
            );
        }
        #endregion

        #region State Modification Methods
        /// <summary>
        /// Creates a new GameState with the specified properties modified.
        /// </summary>
        /// <param name="turnNumber">Optional new turn number.</param>
        /// <param name="currentPhase">Optional new game phase.</param>
        /// <param name="activePlayerId">Optional new active player ID.</param>
        /// <param name="board">Optional new board state.</param>
        /// <param name="playerA">Optional new player A state.</param>
        /// <param name="playerB">Optional new player B state.</param>
        /// <param name="playersReady">Optional new players ready list.</param>
        /// <param name="pendingInteraction">Optional new pending interaction.</param>
        /// <param name="spellStack">Optional new spell stack.</param>
        /// <param name="roundStartingPlayerId">Optional new round starting player ID.</param>
        /// <param name="clearPending">Whether to clear any pending interaction.</param>
        /// <returns>A new GameState with the specified modifications.</returns>
        public GameState With(
            int? turnNumber = null,
            GamePhase? currentPhase = null,
            int? activePlayerId = null,
            BoardState? board = null,
            PlayerState? playerA = null,
            PlayerState? playerB = null,
            IEnumerable<int>? playersReady = null,
            PendingInteraction? pendingInteraction = null,
            IEnumerable<CardInstance>? spellStack = null,
            int? roundStartingPlayerId = null,
            bool clearPending = false,
            int? combatLineIndex = null,
            int? combatStep = null)
        {
            return new GameState(
                turnNumber ?? this.TurnNumber,
                currentPhase ?? this.CurrentPhase,
                activePlayerId ?? this.ActivePlayerId,
                board ?? this.Board,
                playerA ?? this.PlayerA,
                playerB ?? this.PlayerB,
                playersReady ?? this.PlayersReady,
                clearPending ? null : (pendingInteraction ?? this.PendingInteraction),
                spellStack ?? this.SpellStack,
                roundStartingPlayerId ?? this.RoundStartingPlayerId,
                combatLineIndex: combatLineIndex ?? this.CombatLineIndex,
                combatStep: combatStep ?? this.CombatStep
            );
        }

        /// <summary>
        /// Marks a player as ready (e.g., after mulligan).
        /// </summary>
        /// <param name="playerId">The ID of the player to mark as ready.</param>
        /// <returns>A new GameState with the player added to the ready list.</returns>
        public GameState MarkPlayerAsReady(int playerId)
        {
            var newList = new List<int>(PlayersReady);
            if (!newList.Contains(playerId)) newList.Add(playerId);
            return With(playersReady: newList);
        }
        #endregion

        #region Player Access Methods
        /// <summary>
        /// Gets the state of the specified player.
        /// </summary>
        /// <param name="playerId">The ID of the player (1 or 2).</param>
        /// <returns>The PlayerState for the specified player.</returns>
        public PlayerState GetPlayer(int playerId) => playerId == 1 ? PlayerA : PlayerB;

        /// <summary>
        /// Gets the state of the opponent of the specified player.
        /// </summary>
        /// <param name="playerId">The ID of the player to get the opponent of.</param>
        /// <returns>The PlayerState for the opponent.</returns>
        public PlayerState GetOpponent(int playerId) => playerId == 1 ? PlayerB : PlayerA;

        /// <summary>
        /// Updates the state of a player.
        /// </summary>
        /// <param name="newPlayerState">The new player state.</param>
        /// <returns>A new GameState with the updated player.</returns>
        public GameState UpdatePlayer(PlayerState newPlayerState) =>
            newPlayerState.PlayerId == 1 ? With(playerA: newPlayerState) : With(playerB: newPlayerState);

        /// <summary>
        /// Updates the board state.
        /// </summary>
        /// <param name="newBoard">The new board state.</param>
        /// <returns>A new GameState with the updated board.</returns>
        public GameState UpdateBoard(BoardState newBoard) => With(board: newBoard);
        #endregion
    }
}