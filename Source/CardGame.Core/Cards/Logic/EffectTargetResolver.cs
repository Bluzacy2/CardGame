using System;
using System.Linq;

using CardGame.Core.Cards.Data;
using CardGame.Core.Events;
using CardGame.Core.Cards.Models;
using System.Collections.Generic;
using CardGame.Core.Events.Interfaces;
using CardGame.Core.State.Models;


namespace CardGame.Core.Cards.Logic
{
    public class EffectTargets
    {
        public PlayerState? TargetPlayer;
        public List<CardInstance> UnitTargets { get; set; } = new();
        public CardInstance? TargetUnit => UnitTargets.FirstOrDefault();

    }

    public static class EffectTargetResolver
    {
        public static EffectTargets Resolve(TargetType type, GameState state, IGameEvent contextEvent, int sourceCardId)
        {
            var result = new EffectTargets();

            // 1. Ustal właściciela źródła
            int ownerId = DetermineSourceOwner(state, contextEvent, sourceCardId);
            if (ownerId == -1) return result;

            int opponentId = (ownerId == 1) ? 2 : 1;

            switch (type)
            {
                // --- CELE POJEDYNCZE (WYBIERANE) ---
                case TargetType.SelectedTarget:
                case TargetType.TargetEnemyUnit:
                case TargetType.TargetFriendlyUnit:

                    int? targetId = null;
                    if (contextEvent is CardPlayedEvent cpe) targetId = cpe.SelectedTargetId;
                    if (contextEvent is TargetSelectedEvent tse) targetId = tse.SelectedTargetId;

                    if (targetId.HasValue)
                    {
                        var tUnit = FindUnitOnBoard(state, targetId.Value);
                        if (tUnit != null)
                        {
                            bool isValid = true;
                            if (type == TargetType.TargetEnemyUnit && tUnit.OwnerPlayerId != opponentId) isValid = false;
                            if (type == TargetType.TargetFriendlyUnit && tUnit.OwnerPlayerId != ownerId) isValid = false;

                            if (isValid)
                            {
                                // Dodajemy do listy
                                result.UnitTargets.Add(tUnit);
                            }
                        }
                    }
                    break;

                // --- CELE GRACZA ---
                case TargetType.FriendlyHero:
                    result.TargetPlayer = state.GetPlayer(ownerId);
                    break;

                case TargetType.EnemyHero:
                    result.TargetPlayer = state.GetPlayer(opponentId);
                    break;

                // --- KONTEKSTOWE (SELF) ---
                case TargetType.Self:
                    var onBoard = FindUnitOnBoard(state, sourceCardId);
                    if (onBoard != null)
                    {
                        result.UnitTargets.Add(onBoard);
                        result.TargetPlayer = state.GetPlayer(ownerId);
                    }
                    else
                    {
                        // Szukamy w ręce (dla efektów z ręki)
                        var inHand = state.GetPlayer(ownerId).Hand.FirstOrDefault(u => u.InstanceId == sourceCardId);
                        if (inHand != null)
                        {
                            result.UnitTargets.Add(inHand);
                            result.TargetPlayer = state.GetPlayer(ownerId);
                        }
                        else
                        {
                            // Jeśli nie ma jednostki (np. czar już poszedł na cmentarz), ustawiamy chociaż gracza
                            result.TargetPlayer = state.GetPlayer(ownerId);
                        }
                    }
                    break;

                // --- CELE ZBIOROWE (AOE) - NOWOŚĆ ---

                case TargetType.AllEnemyUnits:
                    var enemies = state.Board.GetAllUnits().Where(u => u.OwnerPlayerId == opponentId);
                    result.UnitTargets.AddRange(enemies);
                    break;

                case TargetType.AllFriendlyUnits:
                    // (Upewnij się, że w Enumie nie masz literówki 'ALlFriendlyUnits', jeśli tak - popraw tutaj nazwę)
                    var friends = state.Board.GetAllUnits().Where(u => u.OwnerPlayerId == ownerId);
                    result.UnitTargets.AddRange(friends);
                    break;

                case TargetType.OtherFriendlyUnits:
                    // Wszyscy sojusznicy OPRÓCZ źródła (np. "Give OTHER units +1/+1")
                    var others = state.Board.GetAllUnits()
                        .Where(u => u.OwnerPlayerId == ownerId && u.InstanceId != sourceCardId);
                    result.UnitTargets.AddRange(others);
                    break;
            }

            return result;
        }

        // ... Metody DetermineSourceOwner i FindUnitOnBoard pozostają bez zmian ...
        // (Jeśli ich nie masz pod ręką, mogę je wkleić ponownie)
        public static int DetermineSourceOwner(GameState state, IGameEvent contextEvent, int sourceCardId)
        {
            if (contextEvent is CardPlayedEvent cpe && cpe.Card.InstanceId == sourceCardId) return cpe.PlayerId;
            if (contextEvent is TargetSelectedEvent tse && tse.SourceCardId == sourceCardId) return tse.PlayerId;
            if (contextEvent is UnitDiedEvent ude && ude.Unit.InstanceId == sourceCardId) return ude.Unit.OwnerPlayerId;
            if (contextEvent is UnitSacrificedEvent use && use.Unit.InstanceId == sourceCardId) return use.Unit.OwnerPlayerId;

            var unitOnBoard = FindUnitOnBoard(state, sourceCardId);
            if (unitOnBoard != null) return unitOnBoard.OwnerPlayerId;

            if (state.PlayerA.Hand.Any(c => c.InstanceId == sourceCardId)) return 1;
            if (state.PlayerB.Hand.Any(c => c.InstanceId == sourceCardId)) return 2;

            // Szukamy też w Discard (ważne dla efektów pośmiertnych)
            if (state.PlayerA.DiscardPile.Any(c => c.InstanceId == sourceCardId)) return 1;
            if (state.PlayerB.DiscardPile.Any(c => c.InstanceId == sourceCardId)) return 2;

            return -1;
        }

        private static CardInstance? FindUnitOnBoard(GameState state, int unitId)
        {
            return state.Board.GetAllUnits().FirstOrDefault(u => u.InstanceId == unitId);
        }
    }
}
