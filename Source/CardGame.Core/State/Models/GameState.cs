using CardGame.Core.State.Enums;
using CardGame.Core.Cards.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CardGame.Core.State.Models
{
    public class GameState
    {
        // 1. Podstawewe informacje o grze (tury, ID aktualnego gracza itp.)
        public int TurnNumber { get; }
        public GamePhase CurrentPhase { get; }
        public int ActivePlayerId { get; }

        // 2. Informacje o Stanach gry
        public BoardState BoardState { get; }
        public PlayerState PlayerA { get; }
        public PlayerState PlayerB { get; }

        public GameState(
            int turnNummber,
            GamePhase currentPhase,
            int activePlayerId,
            BoardState boardState,
            PlayerState playerA,
            PlayerState playerB)
        {
            TurnNumber = turnNummber;
            CurrentPhase = currentPhase;
            ActivePlayerId = activePlayerId;
            BoardState = boardState;
            PlayerA = playerA;
            PlayerB = playerB;
        }

        public static GameState Initial(
            int startingPlayerId,
            List<CardInstance> deckA,
            List<CardInstance> deckB)
        {
            return new GameState(
                turnNummber: 1,
                currentPhase: GamePhase.UnitOnly,
                activePlayerId: startingPlayerId,
                boardState: BoardState.Empty(),
                playerA: PlayerState.Initial(playerId: 1, startinDeck: deckA),
                playerB: PlayerState.Initial(playerId: 2, startinDeck: deckB)
             );
        }

        /* WAŻNE: Dla Immutable GameState, aktualizujemy stan poprzez
        tworzenie nowych instancji z TYLKO zmienionymi właściwościami. */
        public GameState With(
            int? turnNummber = null,
            GamePhase? currentPhase = null,
            int? activePlayerId = null,
            BoardState? board = null,
            PlayerState? playerA = null,
            PlayerState? playerB = null)
        {
            return new GameState(
                turnNummber: turnNummber ?? TurnNumber,
                currentPhase: currentPhase ?? CurrentPhase,
                activePlayerId: activePlayerId ?? ActivePlayerId,
                boardState: board ?? BoardState,
                playerA: playerA ?? PlayerA,
                playerB: playerB ?? PlayerB
             );
        }

        public PlayerState GetPlayerState(int playerId)
        {
            if (playerId == 1) return PlayerA;
            else if (playerId == 2)
                return PlayerB;
            else
                throw new ArgumentException("Invalid player ID");
        }

        public PlayerState GetOpponentState(int playerId)
        {
            if (playerId == 1) return PlayerB;
            else if (playerId == 2)
                return PlayerA;
            else
                throw new ArgumentException("Invalid player ID");
        }
    }
}
