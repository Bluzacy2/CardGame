using System;
using System.Collections.Generic;
using System.Linq;
using CardGame.Core.Application;
using CardGame.Core.Cards.Models;
using CardGame.Core.State.Enums;

namespace CardGame.Core.State.Models
{
    public class GameState
    {
        public int TurnNumber { get; }
        public GamePhase CurrentPhase { get; }
        public int ActivePlayerId { get; }
        public BoardState Board { get; }
        public PlayerState PlayerA { get; }
        public PlayerState PlayerB { get; }
        public IReadOnlyList<int> PlayersReady { get; }
        public PendingInteraction? PendingInteraction { get; }

        // ZMIANA: Zamiast jednego czaru, mamy stos czarów w trakcie rozpatrywania
        public IReadOnlyList<CardInstance> SpellStack { get; }

        public GameState(
            int turnNumber,
            GamePhase currentPhase,
            int activePlayerId,
            BoardState board,
            PlayerState playerA,
            PlayerState playerB,
            IEnumerable<int>? playersReady = null,
            PendingInteraction? pendingInteraction = null,
            IEnumerable<CardInstance>? spellStack = null)
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
        }

        public static GameState Initial(int startingPlayerId, List<CardInstance> deckA, List<CardInstance> deckB, DeterministicRng rng)
        {
            var pA = PlayerState.Initial(1, deckA).WithShuffledDeck(rng);
            var pB = PlayerState.Initial(2, deckB).WithShuffledDeck(rng);
            for (int i = 0; i < 4; i++) pA = pA.WithCardDrawn();
            for (int i = 0; i < 4; i++) pB = pB.WithCardDrawn();

            return new GameState(1, GamePhase.Mulligan, startingPlayerId, BoardState.Empty(), pA, pB);
        }

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
            bool clearPending = false,
            bool clearSpellStack = false)
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
                clearSpellStack ? new List<CardInstance>() : (spellStack ?? this.SpellStack)
            );
        }

        public PlayerState GetPlayer(int playerId) => playerId == 1 ? PlayerA : PlayerB;
        public PlayerState GetOpponent(int playerId) => playerId == 1 ? PlayerB : PlayerA;
        public GameState UpdatePlayer(PlayerState newPlayerState) => newPlayerState.PlayerId == 1 ? With(playerA: newPlayerState) : With(playerB: newPlayerState);
        public GameState UpdateBoard(BoardState newBoard) => With(board: newBoard);
        public GameState MarkPlayerAsReady(int playerId)
        {
            var newList = new List<int>(PlayersReady);
            if (!newList.Contains(playerId)) newList.Add(playerId);
            return With(playersReady: newList);
        }
    }
}