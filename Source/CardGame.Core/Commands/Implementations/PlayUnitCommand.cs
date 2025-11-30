using System;
using System.Linq;
using CardGame.Core.Commands.Interfaces;
using CardGame.Core.State.Models; 

namespace CardGame.Core.Commands.Implementations
{
    public class PlayUnitCommand : IGameCommand
    {
        public int PlayerId { get; }
        public int CardInstanceId { get; }  // ID konkretnej karty w ręce
        public int TargetLineIndex { get; } // Na którą linię (0-3)

        public PlayUnitCommand(int playerId, int cardInstanceId, int targetLineIndex)
        {
            PlayerId = playerId;
            CardInstanceId = cardInstanceId;
            TargetLineIndex = targetLineIndex;
        }

        public GameState Execute(GameState currentState)
        {
            // 1. Pobierz dane gracza
            var playerState = currentState.GetPlayer(PlayerId);

            // 2. Znajdź kartę w ręce
            var card = playerState.Hand.FirstOrDefault(c => c.InstanceId == CardInstanceId);

            if (card == null)
            {
                throw new InvalidOperationException($"Gracz {PlayerId} nie ma karty o ID {CardInstanceId} w ręce!");
            }

            // 3. Walidacja kosztu (BLOOD)
            if (!playerState.CanPlayCard(card.CurrentStats.BloodCost))
            {
                throw new InvalidOperationException($"Za mało krwi! Potrzeba: {card.CurrentStats.BloodCost}, Posiadasz: {playerState.CurrentBlood}");
            }

            // 4. Walidacja Planszy (czy slot jest wolny)
            var targetLine = currentState.Board.Lines[TargetLineIndex];
            if (!targetLine.IsSlotEmpty(PlayerId))
            {
                throw new InvalidOperationException($"Linia {TargetLineIndex} jest już zajęta!");
            }

            // --- WYKONANIE (Tworzenie nowego stanu) ---

            // A. Gracz płaci KREW i traci kartę z ręki
            var newPlayerState = playerState
                .WithBloodSpent(card.CurrentStats.BloodCost) 
                .WithCardRemovedFromHand(card);             

            // B. Plansza otrzymuje jednostkę
            var newBoardState = currentState.Board
                .WithUnitPlacedAt(TargetLineIndex, PlayerId, card);

            // C. Składamy to w całość
            if (PlayerId == 1)
            {
                return currentState.With(playerA: newPlayerState, board: newBoardState);
            }
            else
            {
                return currentState.With(playerB: newPlayerState, board: newBoardState);
            }
        }
    }
}