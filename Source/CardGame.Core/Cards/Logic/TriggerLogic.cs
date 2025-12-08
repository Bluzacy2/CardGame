using CardGame.Core.Cards.Data;
using CardGame.Core.Events;
using CardGame.Core.Events.Interfaces;
using CardGame.Core.State.Models;

namespace CardGame.Core.Cards.Logic
{
    public static class TriggerLogic
    {
        public static bool Check(TriggerType triggerType, IGameEvent gameEvent, GameState state, int sourceCardId)
        {
            switch (triggerType)
            {
                case TriggerType.OnPlayed:
                    return gameEvent is CardPlayedEvent cpe && cpe.Card.InstanceId == sourceCardId;

                case TriggerType.OnDeath:
                    return gameEvent is UnitDiedEvent ude && ude.Unit.InstanceId == sourceCardId;

                case TriggerType.OnSacrificed:
                    // Przypadek A: To ta karta została poświęcona (np. Black Cat)
                    if (gameEvent is UnitSacrificedEvent use && use.Unit.InstanceId == sourceCardId)
                        return true;

                    // Przypadek B: Karta jest w ręce/stole i patrzy jak inna jest poświęcana (np. The Moder)
                    if (gameEvent is UnitSacrificedEvent useOther)
                    {
                        int myOwnerId = EffectTargetResolver.DetermineSourceOwner(state, gameEvent, sourceCardId);
                        // Reagujemy tylko na poświęcenia WŁASNYCH jednostek
                        if (useOther.OwnerId == myOwnerId) return true;
                    }
                    return false;

                case TriggerType.OnFriendlyUnitDied:
                    if (gameEvent is UnitDiedEvent diedEvent)
                    {
                        int myOwnerId = EffectTargetResolver.DetermineSourceOwner(state, gameEvent, sourceCardId);

                        // Czy zginęła jednostka mojego gracza, ale NIE ta sama, która ma ten efekt?
                        if (diedEvent.Unit.OwnerPlayerId == myOwnerId && diedEvent.Unit.InstanceId != sourceCardId)
                        {
                            return true;
                        }
                    }
                    return false;

                // Tutaj w przyszłości dodasz: OnTurnStart, OnDamageTaken itp.

                default:
                    return false;
            }
        }
    }
}