using CardGame.Core.Cards.Data;
using CardGame.Core.Events;
using CardGame.Core.Events.Interfaces;
using CardGame.Core.State.Models;
using System.Linq;

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
              
                    return gameEvent is UnitSacrificedEvent use && use.Unit.InstanceId == sourceCardId;

                case TriggerType.OnOtherUnitSacrificed:
                 
                    if (gameEvent is UnitSacrificedEvent useOther && useOther.Unit.InstanceId != sourceCardId)
                    {
                        int myOwnerId = GetOwnerOfInstance(state, sourceCardId);
                        return useOther.OwnerId == myOwnerId;
                    }
                    return false;

                case TriggerType.OnFriendlyUnitDied:
            
                    int ownerId = GetOwnerOfInstance(state, sourceCardId);

                    if (gameEvent is UnitDiedEvent ude2 && ude2.Unit.InstanceId != sourceCardId)
                        return ude2.OwnerId == ownerId;

                    if (gameEvent is UnitSacrificedEvent use2 && use2.Unit.InstanceId != sourceCardId)
                        return use2.OwnerId == ownerId;

                    return false;

                default:
                    return false;
            }
        }

     
        private static int GetOwnerOfInstance(GameState state, int instanceId)
        {
       
            var unitOnBoard = state.Board.GetAllUnits().FirstOrDefault(u => u.InstanceId == instanceId);
            if (unitOnBoard != null) return unitOnBoard.OwnerPlayerId;

       
            if (state.PlayerA.Hand.Any(c => c.InstanceId == instanceId)) return 1;
            if (state.PlayerB.Hand.Any(c => c.InstanceId == instanceId)) return 2;

        
            var spellOnStack = state.SpellStack.FirstOrDefault(s => s.InstanceId == instanceId);
            if (spellOnStack != null) return spellOnStack.OwnerPlayerId;

            return -1;
        }
    }
}