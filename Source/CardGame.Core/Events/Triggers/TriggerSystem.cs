using CardGame.Core.Application;
using CardGame.Core.Cards.Components.Implementations;
using CardGame.Core.Cards.Data;
using CardGame.Core.Events;
using CardGame.Core.Events.Interfaces;
using CardGame.Core.State.Models;
using System.Collections.Generic;
using System.Linq;

namespace CardGame.Core.Events.Triggers
{
    public class TriggerSystem
    {
        private class SourceInfo
        {
            public int Id;
            public EffectData Effect;
            public EffectZone Zone;
            public SourceInfo(int id, EffectData effect, EffectZone zone) { Id = id; Effect = effect; Zone = zone; }
        }

        public GameState ProcessEvents(GameState currentState, EventBus eventBus, GameContext context)
        {
            var workingState = currentState;

            while (eventBus.HasEvents)
            {
                if (workingState.PendingInteraction != null) break;

                var evt = eventBus.Pop();
                var types = GetRelevantTriggerTypes(evt);

                foreach (var type in types)
                {
                    // Budujemy mapę na NAJŚWIEŻSZYM stanie (to rozwiązuje WomboCombo)
                    var map = BuildTriggerMap(workingState);
                    if (!map.TryGetValue(type, out var sources)) continue;

                    foreach (var source in sources)
                    {
                        if (!IsStillValid(workingState, source)) continue;

                        var component = new JsonEffectComponent(source.Effect, source.Id);
                        if (component.ShouldTrigger(evt, workingState))
                        {
                            workingState = component.Resolve(evt, workingState, context);
                            if (workingState.PendingInteraction != null) return workingState;
                        }
                    }
                }
            }
            return workingState;
        }

        private bool IsStillValid(GameState s, SourceInfo info)
        {
            if (info.Zone == EffectZone.Board) return s.Board.GetAllUnits().Any(u => u.InstanceId == info.Id);
            if (info.Zone == EffectZone.Hand) return s.PlayerA.Hand.Concat(s.PlayerB.Hand).Any(c => c.InstanceId == info.Id);
            return true;
        }

        private Dictionary<TriggerType, List<SourceInfo>> BuildTriggerMap(GameState s)
        {
            var map = new Dictionary<TriggerType, List<SourceInfo>>();
            var all = s.Board.GetAllUnits().Select(u => (u, EffectZone.Board))
                .Concat(s.PlayerA.Hand.Concat(s.PlayerB.Hand).Select(c => (c, EffectZone.Hand)))
                .Concat(s.SpellStack.Select(sp => (sp, EffectZone.Any)));

            foreach (var (card, zone) in all)
            {
                foreach (var effect in card.Definition.Effects)
                {
                    if (effect.Trigger == TriggerType.OnPlayed || effect.Zone == EffectZone.Any || effect.Zone == zone)
                    {
                        if (!map.ContainsKey(effect.Trigger)) map[effect.Trigger] = new List<SourceInfo>();
                        map[effect.Trigger].Add(new SourceInfo(card.InstanceId, effect, zone));
                    }
                }
            }
            return map;
        }

        private List<TriggerType> GetRelevantTriggerTypes(IGameEvent evt)
        {
            var t = new List<TriggerType>();
            if (evt is CardPlayedEvent) t.Add(TriggerType.OnPlayed);
            if (evt is UnitDiedEvent) { t.Add(TriggerType.OnDeath); t.Add(TriggerType.OnFriendlyUnitDied); t.Add(TriggerType.OnKill); }
            if (evt is UnitSacrificedEvent) { t.Add(TriggerType.OnSacrificed); t.Add(TriggerType.OnOtherUnitSacrificed); t.Add(TriggerType.OnFriendlyUnitDied); }
            if (evt is UnitDamagedEvent) { t.Add(TriggerType.OnDamagTaken); t.Add(TriggerType.OnDamagedEnemyUnit); t.Add(TriggerType.OnDamagedEnemyHero); }
            if (evt is TurnStartedEvent) t.Add(TriggerType.OnTurnStart);
            if (evt is CardDrawnEvent) { t.Add(TriggerType.OnOpponentCardDrawn); t.Add(TriggerType.OnFriendlyCardDrawn); }
            if (evt is StatusAppliedEvent) t.Add(TriggerType.OnStatusApplied);
            if (evt is PreLineCombatEvent) t.Add(TriggerType.OnPreCombatLine);
            return t;
        }
    }
}