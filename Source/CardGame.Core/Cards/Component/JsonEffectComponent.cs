using CardGame.Core.Application;
using CardGame.Core.Cards.Component;
using CardGame.Core.Cards.Components;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Models;
using CardGame.Core.Events;
using CardGame.Core.Events.Interfaces;
using CardGame.Core.State.Models;
using System;
using System.Linq;

namespace CardGame.Core.Cards.Components.Implementations
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
                case TriggerType.OnDeath:
                    if (gameEvent is UnitDiedEvent ude && ude.Unit.InstanceId == _ownerCardInstanceId)
                        return true;
                    break;
                case TriggerType.OnSacrificed:
                    // Sprawdzamy czy to ta jednostka została poświęcona (jeśli efekt jest na niej samej, np. Black Cat)
                    if (gameEvent is UnitSacrificedEvent use && use.Unit.InstanceId == _ownerCardInstanceId)
                        return true;
                    // Jeśli efekt jest w ręce (The Moder), sprawdzamy to w TriggerSystem, 
                    // ale tutaj też warto mieć zabezpieczenie (OwnerId się zgadza)
                    if (gameEvent is UnitSacrificedEvent useHand && useHand.OwnerId == GetOwnerId(state)) // Metoda pomocnicza niżej
                        return true;
                    break;
                // ... OnFriendlyUnitDied (dla Queen) ...
                case TriggerType.OnFriendlyUnitDied:
                    if (gameEvent is UnitDiedEvent diedEvent)
                    {
                        var ownerId = GetOwnerId(state);
                        if (diedEvent.Unit.OwnerPlayerId == ownerId && diedEvent.Unit.InstanceId != _ownerCardInstanceId)
                            return true;
                    }
                    break;
            }
            return false;
        }

        public GameState Resolve(IGameEvent gameEvent, GameState currentState, GameContext context)
        {
            var workingState = currentState;
            Console.WriteLine($"[DEBUG] Rozpoczynam efekty ID: {_ownerCardInstanceId}");

            foreach (var action in _effectData.Actions)
            {
                workingState = ExecuteSingleAction(action, workingState, gameEvent, context);
            }
            return workingState;
        }

        private GameState ExecuteSingleAction(ActionData action, GameState state, IGameEvent contextEvent, GameContext context)
        {
            var targets = ResolveTargets(action.Target, state, contextEvent);

            if (targets.TargetPlayer == null && targets.TargetUnit == null)
            {
                // Wyjątek: AddCardToHand targetuje gracza, a może go nie znaleźć jeśli ResolveTargets zawiedzie
                // Ale SummonUnit targetuje Self (Unit).
                Console.WriteLine("[DEBUG] OSTRZEŻENIE: Nie znaleziono celu! Akcja pominięta.");
                return state;
            }

            switch (action.Type)
            {
                case ActionType.SacrificeUnit:
                    if (targets.TargetUnit != null)
                    {
                        // --- WALIDACJA: CZY TO MOJA JEDNOSTKA? ---
                        var myOwnerId = GetOwnerId(state);
                        if (targets.TargetUnit.OwnerPlayerId != myOwnerId)
                        {
                            Console.WriteLine($"[BŁĄD ZASAD] Próba poświęcenia wrogiej jednostki! Anulowano.");
                            return state;
                        }

                        Console.WriteLine($"[EFEKT] POŚWIĘCAM JEDNOSTKĘ: {targets.TargetUnit.Definition.Name}");

                        // 1. Publikuj Event
                        context.Events.Publish(new UnitSacrificedEvent(targets.TargetUnit));

                        // 2. Zabij
                        var deadUnit = targets.TargetUnit.TakeDamage(9999);
                        return state.UpdateBoard(state.Board.UpdateUnit(deadUnit));
                    }
                    break;

                case ActionType.SummonUnit:
                    if (contextEvent is UnitDiedEvent deathEvt && targets.TargetUnit != null)
                    {
                        var owner = state.GetPlayer(targets.TargetUnit.OwnerPlayerId);
                        var newOwner = owner.WithCardRemovedFromHand(targets.TargetUnit);
                        state = state.UpdatePlayer(newOwner);

                        // Wstaw na planszę
                        state = state.UpdateBoard(state.Board.WithUnitPlacedAt(deathEvt.LineIndex, owner.PlayerId, targets.TargetUnit));
                        Console.WriteLine($"[EFEKT] SummonUnit: {targets.TargetUnit.Definition.Name} wchodzi na linię {deathEvt.LineIndex}!");
                        return state;
                    }
                    break;

                case ActionType.BuffStats:
                    if (targets.TargetUnit != null) // Buff jednostki (np. ręka)
                    {
                        // Logika dla The Moder (-1 koszt)
                        // Musimy sprawdzić czy to redukcja kosztu czy statystyk
                        // Użyjmy np. pola Amount dla kosztu, a BuffAtk/Hp dla reszty

                        var currentStats = targets.TargetUnit.CurrentStats;

                        // Jeśli w JSON mamy Amount = -1, to zmniejszamy koszt
                        int newCost = currentStats.BloodCost + action.Amount; // -1 zmniejszy
                        int newAtk = currentStats.Attack + action.BuffAtk;
                        int newHp = currentStats.Health + action.BuffHp;

                        var newStats = new CardStats(newAtk, newHp, newCost, currentStats.Keywords);
                        var newUnit = targets.TargetUnit.WithStats(newStats);

                        // Gdzie jest ta jednostka? Ręka czy Stół?
                        // UpdateUnit w BoardState obsłuży stół.
                        // Ale The Moder jest w RĘCE!

                        // Sprawdźmy rękę:
                        var owner = state.GetPlayer(newUnit.OwnerPlayerId);
                        if (owner.Hand.Any(c => c.InstanceId == newUnit.InstanceId))
                        {
                            // Podmień w ręce (usuń starą, dodaj nową)
                            // To wymaga metody w PlayerState "ReplaceCard", albo remove+add
                            var tempOwner = owner.WithCardRemovedFromHand(targets.TargetUnit)
                                                 .WithCardAddedToHand(newUnit);
                            Console.WriteLine($"[EFEKT] Zbuffowano kartę w ręce: {newUnit.Definition.Name} (Nowy koszt: {newCost})");
                            return state.UpdatePlayer(tempOwner);
                        }
                        else
                        {
                            // Stół
                            return state.UpdateBoard(state.Board.UpdateUnit(newUnit));
                        }
                    }
                    break;

                
                case ActionType.DealDamage:
                    if (targets.TargetPlayer != null) return state.UpdatePlayer(targets.TargetPlayer.WithDamageTaken(action.Amount));
                    if (targets.TargetUnit != null) return state.UpdateBoard(state.Board.UpdateUnit(targets.TargetUnit.TakeDamage(action.Amount)));
                    break;
                case ActionType.Heal:
                    if (targets.TargetPlayer != null) return state.UpdatePlayer(targets.TargetPlayer.WithHealthRestored(action.Amount));
                    break;
                case ActionType.ModifyGlobalBuff:
                    if (targets.TargetPlayer != null)
                    {
                        var p = targets.TargetPlayer.WithGlobalBuffModifier(action.BuffAtk, action.BuffHp);
                        state = state.UpdatePlayer(p);
                        var myUnits = state.Board.GetAllUnits().Where(u => u.OwnerPlayerId == p.PlayerId);
                        foreach (var u in myUnits) state = state.UpdateBoard(state.Board.UpdateUnit(u.WithStats(u.CurrentStats + new CardStats(action.BuffAtk, action.BuffHp, 0))));
                        return state;
                    }
                    break;
                case ActionType.DrawCard:
                    if (targets.TargetPlayer != null) return state.UpdatePlayer(targets.TargetPlayer.WithCardDrawn());
                    break;
                case ActionType.AddCardToHand:
                    if (targets.TargetPlayer != null)
                    {
                        try
                        {
                            var tC = context.Factory.CreateCard(action.ValueParam, targets.TargetPlayer.PlayerId);
                            return state.UpdatePlayer(targets.TargetPlayer.WithCardAddedToHand(tC));
                        }
                        catch { }
                    }
                    break;
                case ActionType.ApplyStatus:
                    if (targets.TargetUnit != null && Enum.TryParse<Keyword>(action.StringParam, out var k))
                        return state.UpdateBoard(state.Board.UpdateUnit(targets.TargetUnit.WithKeywordAdded(k)));
                    break;
                case ActionType.AbsorbStats:
                    if (targets.TargetUnit != null) // Ofiara
                    {
                        // 1. Znajdź SIEBIE (Niedźwiedzia) na planszy
                        // (Zakładamy, że Niedźwiedź jest na stole, bo to efekt OnPlayed, a jednostka wchodzi na stół przed triggerem)
                        var meOnBoard = state.Board.GetAllUnits().FirstOrDefault(u => u.InstanceId == _ownerCardInstanceId);

                        if (meOnBoard != null)
                        {
                            var victimStats = targets.TargetUnit.CurrentStats;
                            var myStats = meOnBoard.CurrentStats;

                            // 2. Matematyka (Dodajemy Atak i HP ofiary)
                            int newAtk = myStats.Attack + victimStats.Attack;
                            int newHp = myStats.Health + victimStats.Health;

                            // Tworzymy nowe statystyki (zachowując koszt i keywordy Niedźwiedzia)
                            var newStats = new CardStats(newAtk, newHp, myStats.BloodCost, myStats.Keywords);

                            // 3. Aktualizujemy Siebie
                            var biggerMe = meOnBoard.WithStats(newStats);

                            Console.WriteLine($"[EFEKT] AbsorbStats: {biggerMe.Definition.Name} rośnie do {newAtk}/{newHp} (zjadł {targets.TargetUnit.Definition.Name})");

                            return state.UpdateBoard(state.Board.UpdateUnit(biggerMe));
                        }
                        else
                        {
                            Console.WriteLine("[BŁĄD] Nie mogę zaabsorbować statystyk, bo nie ma mnie na stole!");
                        }
                    }
                    break;
            }

            return state;
        }

        // Pomocnik do ustalania właściciela tej karty (Effect Source)
        private int GetOwnerId(GameState state)
        {
            // 1. Stół
            var onBoard = state.Board.GetAllUnits().FirstOrDefault(u => u.InstanceId == _ownerCardInstanceId);
            if (onBoard != null) return onBoard.OwnerPlayerId;

            // 2. Ręka
            if (state.PlayerA.Hand.Any(c => c.InstanceId == _ownerCardInstanceId)) return 1;
            if (state.PlayerB.Hand.Any(c => c.InstanceId == _ownerCardInstanceId)) return 2;

            return -1;
        }

        private EffectTargets ResolveTargets(TargetType type, GameState state, IGameEvent contextEvent)
        {
            var result = new EffectTargets();
            int ownerId = GetOwnerId(state);

            // Fallback: Jeśli karta zginęła (nie ma jej nigdzie), szukamy w evencie
            if (ownerId == -1)
            {
                if (contextEvent is UnitDiedEvent ude && ude.Unit.InstanceId == _ownerCardInstanceId) ownerId = ude.Unit.OwnerPlayerId;
                else if (contextEvent is CardPlayedEvent cpe && cpe.Card.InstanceId == _ownerCardInstanceId) ownerId = cpe.PlayerId;
            }

            if (ownerId == -1) return result;
            int opponentId = (ownerId == 1) ? 2 : 1;

            switch (type)
            {
                case TargetType.SelectedTarget:
                    {
                        // UŻYWAMY NAWIASÓW KLAMROWYCH {} ŻEBY NAPRAWIĆ BŁĄD ZMIENNYCH
                        if (contextEvent is CardPlayedEvent cpe && cpe.SelectedTargetId.HasValue)
                        {
                            var tUnit = state.Board.GetAllUnits().FirstOrDefault(u => u.InstanceId == cpe.SelectedTargetId.Value);
                            if (tUnit != null) result.TargetUnit = tUnit;
                        }
                        break;
                    }

                case TargetType.TargetEnemyUnit:
                    {
                        if (contextEvent is CardPlayedEvent cpe && cpe.SelectedTargetId.HasValue)
                        {
                            var tUnit = state.Board.GetAllUnits().FirstOrDefault(u => u.InstanceId == cpe.SelectedTargetId.Value);
                            if (tUnit != null && tUnit.OwnerPlayerId == opponentId) result.TargetUnit = tUnit;
                        }
                        break;
                    }

                case TargetType.TargetFriendlyUnit:
                    {
                        if (contextEvent is CardPlayedEvent cpe && cpe.SelectedTargetId.HasValue)
                        {
                            var tUnit = state.Board.GetAllUnits().FirstOrDefault(u => u.InstanceId == cpe.SelectedTargetId.Value);
                            // Walidacja: Musi być Friendly
                            if (tUnit != null && tUnit.OwnerPlayerId == ownerId) result.TargetUnit = tUnit;
                            else Console.WriteLine("[TARGET ERROR] Wymagany sojusznik, wybrano wroga/nic.");
                        }
                        break;
                    }

                case TargetType.FriendlyHero:
                    result.TargetPlayer = state.GetPlayer(ownerId);
                    break;
                case TargetType.EnemyHero:
                    result.TargetPlayer = state.GetPlayer(opponentId);
                    break;
                case TargetType.Self:
                    // Self może być w ręce (The Moder) lub na stole
                    var onBoard = state.Board.GetAllUnits().FirstOrDefault(u => u.InstanceId == _ownerCardInstanceId);
                    if (onBoard != null) { result.TargetUnit = onBoard; }
                    else
                    {
                        var inHand = state.GetPlayer(ownerId).Hand.FirstOrDefault(u => u.InstanceId == _ownerCardInstanceId);
                        if (inHand != null) result.TargetUnit = inHand;
                    }
                    break;
            }
            return result;
        }

        private class EffectTargets { public PlayerState? TargetPlayer; public CardInstance? TargetUnit; }
    }
}