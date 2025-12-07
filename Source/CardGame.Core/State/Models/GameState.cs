using System;
using System.Collections.Generic;
using CardGame.Core.Application;
using CardGame.Core.Cards.Models;
using CardGame.Core.State.Enums;

namespace CardGame.Core.State.Models
{
    public class GameState
    {
        // --- Podstawowe liczniki ---
        public int TurnNumber { get; }
        public GamePhase CurrentPhase { get; }
        public int ActivePlayerId { get; }

        // --- Kontenery ---
        public BoardState Board { get; }
        public PlayerState PlayerA { get; }
        public PlayerState PlayerB { get; }

        // --- Mulligan ---
        public IReadOnlyList<int> PlayersReady { get; }

        // Konstruktor
        public GameState(
            int turnNumber,
            GamePhase currentPhase,
            int activePlayerId,
            BoardState board,
            PlayerState playerA,
            PlayerState playerB,
            // ZMIANA: Przyjmujemy IEnumerable, żeby naprawić błąd konwersji
            IEnumerable<int>? playersReady = null)
        {
            TurnNumber = turnNumber;
            CurrentPhase = currentPhase;
            ActivePlayerId = activePlayerId;
            Board = board;
            PlayerA = playerA;
            PlayerB = playerB;
            // Konwertujemy na Listę wewnętrznie
            PlayersReady = playersReady != null ? new List<int>(playersReady) : new List<int>();
        }

        // --- Metoda Fabrykująca ---
        public static GameState Initial(int startingPlayerId, List<CardInstance> deckA, List<CardInstance> deckB, 
            DeterministicRng rng)
        {

            var pA = PlayerState.Initial(1, deckA).WithShuffledDeck(rng); // <--- TASOWANIE
            var pB = PlayerState.Initial(2, deckB).WithShuffledDeck(rng);

            // Rozdajemy rękę startową (4 karty) dla Mulligana
            for (int i = 0; i < 4; i++) pA = pA.WithCardDrawn();
            for (int i = 0; i < 4; i++) pB = pB.WithCardDrawn();

            return new GameState(
                turnNumber: 1,
                currentPhase: GamePhase.Mulligan, // Startujemy od Mulligana
                activePlayerId: startingPlayerId,
                board: BoardState.Empty(),
                playerA: pA,
                playerB: pB,
                playersReady: new List<int>()
            );
        }

        // --- Metoda "With" ---
        public GameState With(
            int? turnNumber = null,
            GamePhase? currentPhase = null,
            int? activePlayerId = null,
            BoardState? board = null,
            PlayerState? playerA = null,
            PlayerState? playerB = null,
            IEnumerable<int>? playersReady = null) // Parametr opcjonalny
        {
            return new GameState(
                turnNumber ?? this.TurnNumber,
                currentPhase ?? this.CurrentPhase,
                activePlayerId ?? this.ActivePlayerId,
                board ?? this.Board,
                playerA ?? this.PlayerA,
                playerB ?? this.PlayerB,
                playersReady ?? this.PlayersReady // Teraz zadziała, bo konstruktor przyjmuje IEnumerable
            );
        }

        // --- Metody pomocnicze ---
        public PlayerState GetPlayer(int playerId)
        {
            if (playerId == 1) return PlayerA;
            if (playerId == 2) return PlayerB;
            throw new ArgumentException($"Invalid player ID: {playerId}");
        }

        public PlayerState GetOpponent(int playerId)
        {
            return playerId == 1 ? PlayerB : PlayerA;
        }

        public GameState UpdatePlayer(PlayerState newPlayerState)
        {
            if (newPlayerState.PlayerId == 1)
                return this.With(playerA: newPlayerState);
            else
                return this.With(playerB: newPlayerState);
        }

        public GameState UpdateBoard(BoardState newBoard)
        {
            return this.With(board: newBoard);
        }

        // Pomocnik do oznaczania gotowości w Mulliganie
        public GameState MarkPlayerAsReady(int playerId)
        {
            var newList = new List<int>(PlayersReady);
            if (!newList.Contains(playerId))
            {
                newList.Add(playerId);
            }
            return With(playersReady: newList);
        }
    }
}