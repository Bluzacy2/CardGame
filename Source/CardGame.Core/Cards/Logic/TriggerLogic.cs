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
            var source = state.Board.GetAllUnits().FirstOrDefault(u => u.InstanceId == sourceCardId)
                      ?? state.PlayerA.Hand.Concat(state.PlayerB.Hand).FirstOrDefault(c => c.InstanceId == sourceCardId)
                      ?? state.SpellStack.FirstOrDefault(s => s.InstanceId == sourceCardId);

            if (source == null) return false;
            if (source.IsSilenced && effect.Trigger != TriggerType.OnPlayed) return false;

            int myOwnerId = source.OwnerPlayerId;

            if (effect.Condition != null)
            {
                var cond = effect.Condition;
                if (cond.Condition == ConditionType.IsStatus && gameEvent is StatusAppliedEvent sae)
                {
                    if (!string.Equals(sae.Status.ToString(), cond.TargetParam, System.StringComparison.OrdinalIgnoreCase)) return false;
                }
                if (cond.Condition == ConditionType.IsSelf)
                {
                    CardInstance? actor = null;
                    if (gameEvent is UnitDamagedEvent ude) actor = ude.Source;
                    if (gameEvent is UnitDiedEvent udied) actor = (effect.Trigger == TriggerType.OnKill) ? state.Board.GetAllUnits().Concat(state.PlayerA.DiscardPile).Concat(state.PlayerB.DiscardPile).FirstOrDefault(u => u.InstanceId == udied.KillerInstanceId) : udied.Unit;
                    if (actor == null || actor.InstanceId != sourceCardId) return false;
                }
                if (cond.Condition == ConditionType.IsSubtype)
                {
                    CardInstance? subject = null;
                    if (gameEvent is UnitDamagedEvent ude) subject = ude.Source;
                    else if (gameEvent is UnitDiedEvent ud) subject = ud.Unit;
                    else if (gameEvent is CardPlayedEvent cp) subject = cp.Card;
                    if (subject == null || !subject.Definition.Subtypes.Contains(cond.TargetParam)) return false;
                }
            }

            switch (effect.Trigger)
            {
                case TriggerType.OnPlayed: return gameEvent is CardPlayedEvent cpe && cpe.Card.InstanceId == sourceCardId;
                case TriggerType.OnDeath: return gameEvent is UnitDiedEvent ude && ude.Unit.InstanceId == sourceCardId;
                case TriggerType.OnKill: return gameEvent is UnitDiedEvent uk && uk.KillerInstanceId == sourceCardId;
                case TriggerType.OnSacrificed: return gameEvent is UnitSacrificedEvent use && use.Unit.InstanceId == sourceCardId;
                case TriggerType.OnFriendlyUnitDied:
                    if (gameEvent is UnitDiedEvent fde) return fde.Unit.InstanceId != sourceCardId && fde.OwnerId == myOwnerId;
                    if (gameEvent is UnitSacrificedEvent use2) return use2.Unit.InstanceId != sourceCardId && use2.OwnerId == myOwnerId;
                    return false;
                case TriggerType.OnDamagedEnemyHero:
                    return gameEvent is UnitDamagedEvent heroDmg && heroDmg.Source != null && heroDmg.Source.OwnerPlayerId == myOwnerId && heroDmg.Unit == null;
                case TriggerType.OnDamagedEnemyUnit: 
                    return gameEvent is UnitDamagedEvent unitDmg && unitDmg.Source?.InstanceId == sourceCardId && unitDmg.Unit != null && unitDmg.Unit.OwnerPlayerId != myOwnerId;
                case TriggerType.OnOpponentCardDrawn: return gameEvent is CardDrawnEvent od && od.PlayerId != myOwnerId;
                case TriggerType.OnFriendlyCardDrawn: return gameEvent is CardDrawnEvent fd && fd.PlayerId == myOwnerId;
                case TriggerType.OnPreCombatLine: return gameEvent is PreLineCombatEvent ple && state.Board.Lines[ple.LineIndex].GetAllUnits().Any(u => u.InstanceId == sourceCardId);
                case TriggerType.OnStatusApplied: return gameEvent is StatusAppliedEvent;
                default: return false;
            }
        }
    }
}