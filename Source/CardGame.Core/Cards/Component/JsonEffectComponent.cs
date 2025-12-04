using System;
using System.Linq; // Ważne dla LINQ

using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Models;
using CardGame.Core.Events.Interfaces;
using CardGame.Core.Events;
using CardGame.Core.State.Models;

namespace CardGame.Core.Cards.Component
{
    public class JsonEffectComponent : ICardComponent
    {
        private readonly EffectData _effectData;
        private readonly int _ownerCardInstanceId;

        public JsonEffectComponent(EffectData effectData, int ownerCardInstanceId)
        {
            _effectData = effectData;
            _ownerCardInstanceId = ownerCardInstanceId;
        }

        public bool ShouldTrigger(IGameEvent gameEvent, GameState state)
        {
            switch (_effectData.Trigger)
            {
                case TriggerType.OnPlayed:
                    if (gameEvent is CardPlayedEvent cpe && cpe.Card.InstanceId == _ownerCardInstanceId)
                        return true;
                    break;
                    // Tu w przyszłości OnDeath...
            }
            return false;
        }

        public GameState Resolve(IGameEvent gameEvent, GameState currentState)
        {
            GameState workingState = currentState;

            // Log diagnostyczny
            Console.WriteLine($"[DEBUG] Rozpoczynam wykonywanie efektów dla karty ID: {_ownerCardInstanceId}");

            foreach (var action in _effectData.Actions)
            {
                // Przekazujemy gameEvent głębiej, żeby łatwiej znaleźć właściciela!
                workingState = ExecuteSingleAction(action, workingState, gameEvent);
            }

            return workingState;
        }

        private GameState ExecuteSingleAction(ActionData action, GameState state, IGameEvent contextEvent)
        {
            Console.WriteLine($"[DEBUG] Akcja: {action.Type}, Cel: {action.Target}, Wartość: {action.Amount}");

            // 1. ZNAJDŹ CELE
            var targets = ResolveTargets(action.Target, state, contextEvent);

            if (targets.TargetPlayer == null && targets.TargetUnit == null)
            {
                Console.WriteLine("[DEBUG] OSTRZEŻENIE: Nie znaleziono celu! Akcja pominięta.");
                return state;
            }

            // 2. WYKONAJ LOGIKĘ
            switch (action.Type)
            {
                case ActionType.ApplyStatus:
                    if (targets.TargetUnit != null)
                    {
                        // 1. Parsujemy string z JSONa na Enum (np. "Marked" -> Keyword.Marked)
                        if (Enum.TryParse<Keyword>(action.StringParam, out var keyword))
                        {
                            // 2. Dodajemy keyword do statystyk jednostki
                            // (Wymaga metody WithKeyword w CardInstance, którą dodaliśmy wcześniej w CardStats,
                            // ale musimy ją wystawić w CardInstance).

                            // Zaraz dodamy metodę pomocniczą do CardInstance, na razie załóżmy że jest:
                            var newUnit = targets.TargetUnit.WithKeywordAdded(keyword);

                            Console.WriteLine($"[EFEKT] Nadano status {keyword} jednostce {newUnit.Definition.Name}");

                            // 3. Aktualizujemy planszę
                            return state.UpdateBoard(state.Board.UpdateUnit(newUnit));
                        }
                        else
                        {
                            Console.WriteLine($"[BŁĄD] Nieznany status: {action.StringParam}");
                        }
                    }
                    break;
                case ActionType.DealDamage:
                    if (targets.TargetPlayer != null)
                    {
                        var newPlayer = targets.TargetPlayer.WithDamageTaken(action.Amount);
                        Console.WriteLine($"[EFEKT] Zadano {action.Amount} dmg graczowi {newPlayer.PlayerId}");
                        return state.UpdatePlayer(newPlayer);
                    }
                    break;

                case ActionType.Heal:
                    if (targets.TargetPlayer != null)
                    {
                        var newPlayer = targets.TargetPlayer.WithHealthRestored(action.Amount);
                        Console.WriteLine($"[EFEKT] Uleczono gracza {newPlayer.PlayerId} o {action.Amount}");
                        return state.UpdatePlayer(newPlayer);
                    }
                    break;

                case ActionType.AddCardToHand:
                    if (targets.TargetPlayer != null)
                    {
                        // Tworzenie tokena
                        // Uwaga: To wymaga, żeby ID (ValueParam) istniało w bazie JSON!
                        // W teście użyliśmy ID 900 dla "The Alluring Goblet"
                        try
                        {
                            var tokenDef = CardGame.Core.Cards.Data.CardLibrary.Instance.CreateDefinition(action.ValueParam);
                            int newId = new Random().Next(10000, 99999);
                            var tokenCard = new CardInstance(newId, targets.TargetPlayer.PlayerId, tokenDef);

                            var newPlayer = targets.TargetPlayer.WithCardAddedToHand(tokenCard);
                            Console.WriteLine($"[EFEKT] Dodano kartę '{tokenDef.Name}' do ręki gracza {newPlayer.PlayerId}");
                            return state.UpdatePlayer(newPlayer);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[BŁĄD EFEKTU] Nie udało się stworzyć karty ID {action.ValueParam}: {ex.Message}");
                        }
                    }
                    break;
            }

            return state;
        }

        // Pomocnicza klasa
        private class EffectTargets { public PlayerState? TargetPlayer; public CardInstance? TargetUnit; }

        private EffectTargets ResolveTargets(TargetType type, GameState state, IGameEvent contextEvent)
        {
            var result = new EffectTargets();
            int OwnerPlayerId = -1;

            // --- DETEKTYW WŁAŚCICIELA ---
            // Musimy ustalić, do kogo należy ta karta, żeby wiedzieć co to "Friendly" a co "Enemy".

            // Sposób 1: Sprawdź w evencie (najpewniejsze dla OnPlayed)
            if (contextEvent is CardPlayedEvent cpe && cpe.Card.InstanceId == _ownerCardInstanceId)
            {
                OwnerPlayerId = cpe.PlayerId;
            }
            // Sposób 2: Sprawdź na planszy (dla efektów pasywnych)
            else
            {
                var unitOnBoard = state.Board.GetAllUnits().FirstOrDefault(u => u.InstanceId == _ownerCardInstanceId);
                if (unitOnBoard != null)
                {
                    OwnerPlayerId = unitOnBoard.OwnerPlayerId;
                }
            }

            // Sposób 3: Ostateczność - sprawdź ręce
            if (OwnerPlayerId == -1)
            {
                if (state.PlayerA.Hand.Any(c => c.InstanceId == _ownerCardInstanceId)) OwnerPlayerId = 1;
                else if (state.PlayerB.Hand.Any(c => c.InstanceId == _ownerCardInstanceId)) OwnerPlayerId = 2;
            }

            Console.WriteLine($"[DEBUG] Wykryto właściciela karty: Gracz {OwnerPlayerId}");

            if (OwnerPlayerId == -1) return result; // Nie udało się ustalić właściciela

            int opponentId = (OwnerPlayerId == 1) ? 2 : 1;

            switch (type)
            {
                case TargetType.TargetEnemyUnit:
                    // TYMCZASOWE: Automatyczny wybór pierwszego wroga
                    // W prawdziwej grze tutaj byłoby odwołanie do TargetSelectionSystem
                    var enemies = state.Board.GetAllUnits().Where(u => u.OwnerPlayerId == opponentId).ToList();
                    if (enemies.Any())
                    {
                        result.TargetUnit = enemies.First();
                        Console.WriteLine($"[AUTO-TARGET] Namierzono wroga: {result.TargetUnit.Definition.Name}");
                    }
                    break;
                case TargetType.FriendlyHero:
                    result.TargetPlayer = state.GetPlayer(OwnerPlayerId);
                    break;
                case TargetType.EnemyHero:
                    result.TargetPlayer = state.GetPlayer(opponentId);
                    break;
                case TargetType.Self:
                    result.TargetPlayer = state.GetPlayer(OwnerPlayerId);
                    break;
            }

            return result;
        }
    }
}