using System;
using System.Collections.Generic;
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

        // --- Kontenery (Tu brakowało Board!) ---
        public BoardState Board { get; }
        public PlayerState PlayerA { get; }
        public PlayerState PlayerB { get; }

        // Konstruktor
        public GameState(
            int turnNumber,
            GamePhase currentPhase,
            int activePlayerId,
            BoardState board,
            PlayerState playerA,
            PlayerState playerB)
        {
            TurnNumber = turnNumber;
            CurrentPhase = currentPhase;
            ActivePlayerId = activePlayerId;
            Board = board;
            PlayerA = playerA;
            PlayerB = playerB;
        }

        // --- Metoda Fabrykująca ---
        public static GameState Initial(int startingPlayerId, List<CardInstance> deckA, List<CardInstance> deckB)
        {
            return new GameState(
                turnNumber: 1,
                currentPhase: GamePhase.UnitOnly,
                activePlayerId: startingPlayerId,
                board: BoardState.Empty(),
                playerA: PlayerState.Initial(1, deckA),
                playerB: PlayerState.Initial(2, deckB)
            );
        }

        // --- Metoda "With" (Kopia ze zmianami) ---
        public GameState With(
            int? turnNumber = null,
            GamePhase? currentPhase = null,
            int? activePlayerId = null,
            BoardState? board = null,
            PlayerState? playerA = null,
            PlayerState? playerB = null)
        {
            return new GameState(
                turnNumber ?? this.TurnNumber,
                currentPhase ?? this.CurrentPhase,
                activePlayerId ?? this.ActivePlayerId,

               
                board ?? this.Board,

                playerA ?? this.PlayerA,
                playerB ?? this.PlayerB
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


    }


}