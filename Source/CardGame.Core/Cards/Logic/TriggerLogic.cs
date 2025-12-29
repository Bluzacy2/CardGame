using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Models;
using CardGame.Core.Events;
using CardGame.Core.Events.Interfaces;
using CardGame.Core.State.Models;
using System.Linq;

namespace CardGame.Core.Cards.Logic
{
    public static class TriggerLogic
    {
        public static bool Check(EffectData effect, IGameEvent gameEvent, GameState state, int sourceCardId)
        {
            // Znajdź kartę, która posiada dany efekt (na planszy, w ręce lub na stosie czarów)
            var source = state.Board.GetAllUnits().FirstOrDefault(u => u.InstanceId == sourceCardId)
                      ?? state.PlayerA.Hand.Concat(state.PlayerB.Hand).FirstOrDefault(c => c.InstanceId == sourceCardId)
                      ?? state.SpellStack.FirstOrDefault(s => s.InstanceId == sourceCardId);

            if (source == null) return false;
            
            // Jednostki wyciszone nie aktywują triggerów, chyba że jest to OnPlayed (który odpala się w momencie wejścia)
            if (source.IsSilenced && effect.Trigger != TriggerType.OnPlayed) return false;

            int myOwnerId = source.OwnerPlayerId;

            // Weryfikacja dodatkowych warunków (Conditions)
            if (effect.Condition != null)
            {
                var cond = effect.Condition;
                
                // Warunek: Czy nadany status jest konkretnego typu (np. Marked)
                if (cond.Condition == ConditionType.IsStatus && gameEvent is StatusAppliedEvent sae)
                {
                    if (!string.Equals(sae.Status.ToString(), cond.TargetParam, System.StringComparison.OrdinalIgnoreCase)) return false;
                }
                
                // Warunek: Czy podmiotem zdarzenia jest ta sama karta, która posiada efekt
                if (cond.Condition == ConditionType.IsSelf)
                {
                    int actorId = -1;
                    if (gameEvent is UnitDamagedEvent ude) actorId = ude.Source?.InstanceId ?? -1;
                    else if (gameEvent is UnitDiedEvent udied) actorId = (effect.Trigger == TriggerType.OnKill) ? udied.KillerInstanceId ?? -1 : udied.Unit.InstanceId;
                    else if (gameEvent is CardPlayedEvent cpe) actorId = cpe.Card.InstanceId;
                    else if (gameEvent is UnitSacrificedEvent use) actorId = use.Unit.InstanceId;

                    if (actorId != sourceCardId) return false;
                }
                
                // Warunek: Czy karta biorąca udział w zdarzeniu ma konkretny podtyp (np. Monster)
                if (cond.Condition == ConditionType.IsSubtype)
                {
                    CardInstance? subject = null;
                    if (gameEvent is UnitDamagedEvent ude) subject = ude.Source;
                    else if (gameEvent is UnitDiedEvent ud) subject = ud.Unit;
                    else if (gameEvent is CardPlayedEvent cp) subject = cp.Card;
                    
                    if (subject == null || !subject.Definition.Subtypes.Contains(cond.TargetParam)) return false;
                }
                
                // Warunek: Czy podmiotem jest przeciwnik
                if (cond.Condition == ConditionType.IsEnemy)
                {
                    if (gameEvent.SourcePlayerId == myOwnerId) return false;
                }
            }

            // Dopasowanie typu triggera do zaistniałego zdarzenia
            switch (effect.Trigger)
            {
                case TriggerType.OnPlayed: 
                    return gameEvent is CardPlayedEvent cpe && cpe.Card.InstanceId == sourceCardId;
                
                case TriggerType.OnDeath: 
                    return gameEvent is UnitDiedEvent ude && ude.Unit.InstanceId == sourceCardId;
                
                case TriggerType.OnKill: 
                    return gameEvent is UnitDiedEvent uk && uk.KillerInstanceId == sourceCardId;
                
                case TriggerType.OnSacrificed: 
                    return gameEvent is UnitSacrificedEvent use && use.Unit.InstanceId == sourceCardId;
                
                case TriggerType.OnFriendlyUnitDied:
                    if (gameEvent is UnitDiedEvent fde) return fde.Unit.InstanceId != sourceCardId && fde.OwnerId == myOwnerId;
                    if (gameEvent is UnitSacrificedEvent use2) return use2.Unit.InstanceId != sourceCardId && use2.OwnerId == myOwnerId;
                    return false;
                
                case TriggerType.OnDamagedEnemyHero:
                    return gameEvent is UnitDamagedEvent heroDmg && heroDmg.Source != null && heroDmg.Source.OwnerPlayerId == myOwnerId && heroDmg.Unit == null;
                
                case TriggerType.OnDamagedEnemyUnit: 
                    return gameEvent is UnitDamagedEvent unitDmg && unitDmg.Source?.InstanceId == sourceCardId && unitDmg.Unit != null && unitDmg.Unit.OwnerPlayerId != myOwnerId;
                
                case TriggerType.OnOpponentCardDrawn: 
                    return gameEvent is CardDrawnEvent od && od.PlayerId != myOwnerId;
                
                case TriggerType.OnFriendlyCardDrawn: 
                    return gameEvent is CardDrawnEvent fd && fd.PlayerId == myOwnerId;
                
                case TriggerType.OnPreCombatLine: 
                    return gameEvent is PreLineCombatEvent ple && state.Board.Lines[ple.LineIndex].GetAllUnits().Any(u => u.InstanceId == sourceCardId);
                
                case TriggerType.OnStatusApplied: 
                    return gameEvent is StatusAppliedEvent;
                
                case TriggerType.OnDamagTaken:
                    return gameEvent is UnitDamagedEvent udt && udt.Unit?.InstanceId == sourceCardId;

                default: 
                    return false;
            }
        }
    }
}