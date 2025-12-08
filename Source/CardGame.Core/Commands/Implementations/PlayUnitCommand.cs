using System;
using System.Linq;
using CardGame.Core.Events;
using CardGame.Core.Application;
using CardGame.Core.State.Models;
using CardGame.Core.Commands.Interfaces;

namespace CardGame.Core.Commands.Implementations
{
    public class PlayUnitCommand : IGameCommand
    {
        public int PlayerId { get; }
        public int CardInstanceId { get; }  // ID konkretnej karty w ręce
        public int TargetLineIndex { get; } // Na którą linię (0-3)

        public int? SelectedTargetId { get; } // ID celu, jeśli dotyczy (np. dla czarów)

        public PlayUnitCommand(int playerId, int cardInstanceId, int targetLineIndex, int? selectedTargetId = null)
        {
            PlayerId = playerId;
            CardInstanceId = cardInstanceId;
            TargetLineIndex = targetLineIndex;
            SelectedTargetId = selectedTargetId;
        }

        public GameState Execute(GameState currentState, EventBus eventBus, GameContext context)
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
            var statsWithBuffs = card.CurrentStats + playerState.GlobalUnitBuffs;
            var cardToPlay = card.AddPermanentBuff(playerState.GlobalUnitBuffs);
            // -------------------------------------------------

            // Używamy cardToPlay zamiast card!
            var newBoardState = currentState.Board
                .WithUnitPlacedAt(TargetLineIndex, PlayerId, cardToPlay);

            // C. Publikujemy zdarzenie, że karta (jednostka), została zagrana -B.
            eventBus.Publish(new CardPlayedEvent(PlayerId, card, TargetLineIndex, SelectedTargetId));

            // D. Składamy to w całość
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