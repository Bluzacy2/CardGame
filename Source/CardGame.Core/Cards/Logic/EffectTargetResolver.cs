using System;
using System.Linq;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Models;
using CardGame.Core.Events;
using CardGame.Core.Events.Interfaces;
using CardGame.Core.State.Models;

namespace CardGame.Core.Cards.Logic
{
    // Struktura pomocnicza do zwracania wyników namierzania
    public class EffectTargets
    {
        public PlayerState? TargetPlayer;
        public CardInstance? TargetUnit;
    }

    public static class EffectTargetResolver
    {
        public static EffectTargets Resolve(TargetType type, GameState state, IGameEvent contextEvent, int sourceCardId)
        {
            var result = new EffectTargets();

            // KROK 1: Ustal, kto jest właścicielem karty wywołującej efekt (Source Owner)
            int ownerId = DetermineSourceOwner(state, contextEvent, sourceCardId);

            // Jeśli nie udało się ustalić właściciela, nie możemy bezpiecznie rozwiązać celów "Friendly/Enemy"
            if (ownerId == -1)
            {
                Console.WriteLine($"[TARGET ERROR] Nie udało się ustalić właściciela karty źródłowej (ID: {sourceCardId})");
                return result;
            }

            int opponentId = (ownerId == 1) ? 2 : 1;

            switch (type)
            {
                // Cel wybrany jawnie przez gracza (wskazanie myszką / komendą)
                case TargetType.SelectedTarget:
                    if (contextEvent is CardPlayedEvent cpe && cpe.SelectedTargetId.HasValue)
                    {
                        // Szukamy na stole
                        var targetUnit = FindUnitOnBoard(state, cpe.SelectedTargetId.Value);
                        if (targetUnit != null)
                        {
                            result.TargetUnit = targetUnit;
                        }
                        else
                        {
                            Console.WriteLine($"[TARGET INFO] Wybranego celu ID {cpe.SelectedTargetId.Value} nie ma na stole.");
                        }
                    }
                    break;

                // Automatyczne namierzanie wroga (dla prostych efektów)
                case TargetType.TargetEnemyUnit:
                    // Jeśli gracz wskazał cel, używamy go
                    if (contextEvent is CardPlayedEvent cpeEnemy && cpeEnemy.SelectedTargetId.HasValue)
                    {
                        var tUnit = FindUnitOnBoard(state, cpeEnemy.SelectedTargetId.Value);
                        // Walidacja: Czy to faktycznie wróg?
                        if (tUnit != null && tUnit.OwnerPlayerId == opponentId)
                        {
                            result.TargetUnit = tUnit;
                        }
                    }
                    break;

                // Automatyczne namierzanie sojusznika
                case TargetType.TargetFriendlyUnit:
                    // Jeśli gracz wskazał cel
                    if (contextEvent is CardPlayedEvent cpeFriendly && cpeFriendly.SelectedTargetId.HasValue)
                    {
                        var tUnit = FindUnitOnBoard(state, cpeFriendly.SelectedTargetId.Value);
                        // Walidacja: Czy to sojusznik?
                        if (tUnit != null && tUnit.OwnerPlayerId == ownerId)
                        {
                            result.TargetUnit = tUnit;
                        }
                    }
                    break;

                // Cele globalne / Gracz
                case TargetType.FriendlyHero:
                    result.TargetPlayer = state.GetPlayer(ownerId);
                    break;

                case TargetType.EnemyHero:
                    result.TargetPlayer = state.GetPlayer(opponentId);
                    break;

                // Źródło efektu (Siebie)
                case TargetType.Self:
                    // 1. Szukamy na stole (najczęstszy przypadek)
                    var onBoard = FindUnitOnBoard(state, sourceCardId);
                    if (onBoard != null)
                    {
                        result.TargetUnit = onBoard;
                        result.TargetPlayer = state.GetPlayer(ownerId);
                    }
                    else
                    {
                        // 2. Szukamy w ręce (dla efektów "While in Hand" lub SummonUnit)
                        var inHand = state.GetPlayer(ownerId).Hand.FirstOrDefault(u => u.InstanceId == sourceCardId);
                        if (inHand != null)
                        {
                            result.TargetUnit = inHand;
                            result.TargetPlayer = state.GetPlayer(ownerId);
                        }
                        // 3. W ostateczności TargetPlayer jest ustawiony (dla efektów Draw/Tutor z cmentarza)
                        else
                        {
                            result.TargetPlayer = state.GetPlayer(ownerId);
                        }
                    }
                    break;

                   
            }

            return result;
        }

        // --- METODY POMOCNICZE ---

        public static int DetermineSourceOwner(GameState state, IGameEvent contextEvent, int sourceCardId)
        {
            // Priorytet 1: Event (To jest pewne źródło prawdy o momencie akcji)
            if (contextEvent is CardPlayedEvent cpe && cpe.Card.InstanceId == sourceCardId)
                return cpe.PlayerId;

            if (contextEvent is UnitDiedEvent ude && ude.Unit.InstanceId == sourceCardId)
                return ude.Unit.OwnerPlayerId;

            if (contextEvent is UnitSacrificedEvent use && use.Unit.InstanceId == sourceCardId)
                return use.Unit.OwnerPlayerId;

            // Priorytet 2: Stół (Dla efektów pasywnych / aktywowanych później)
            var unitOnBoard = FindUnitOnBoard(state, sourceCardId);
            if (unitOnBoard != null)
                return unitOnBoard.OwnerPlayerId;

            // Priorytet 3: Ręka (Dla efektów z ręki)
            if (state.PlayerA.Hand.Any(c => c.InstanceId == sourceCardId)) return 1;
            if (state.PlayerB.Hand.Any(c => c.InstanceId == sourceCardId)) return 2;

            // Priorytet 4: Cmentarz (Dla efektów działających po śmierci, jeśli event nie wystarczył)
            if (state.PlayerA.DiscardPile.Any(c => c.InstanceId == sourceCardId)) return 1;
            if (state.PlayerB.DiscardPile.Any(c => c.InstanceId == sourceCardId)) return 2;

            return -1; // Nie znaleziono
        }

        private static CardInstance? FindUnitOnBoard(GameState state, int unitId)
        {
            return state.Board.GetAllUnits().FirstOrDefault(u => u.InstanceId == unitId);
        }
    }
}