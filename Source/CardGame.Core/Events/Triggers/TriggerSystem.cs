using CardGame.Core.Application;
using CardGame.Core.Cards.Components.Implementations;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Models;
using CardGame.Core.Events;
using CardGame.Core.Events.Interfaces;
using CardGame.Core.State.Models;
using System.Collections.Generic;
using System.Linq;

namespace CardGame.Core.Events.Triggers
{
    /// <summary>
    /// Handles event-triggered effects and processes them according to game state.
    /// </summary>
    public class TriggerSystem
    {
        #region Nested Types
        private class SourceInfo
        {
            public int Id;
            public EffectData Effect;
            public EffectZone Zone;

            public SourceInfo(int id, EffectData effect, EffectZone zone)
            {
                Id = id;
                Effect = effect;
                Zone = zone;
            }
        }
        #endregion

        #region Event Processing
        /// <summary>
        /// Processes pending events and activates relevant triggers until an interaction is required.
        /// </summary>
        /// <param name="currentState">The current game state.</param>
        /// <param name="eventBus">The event bus containing pending events.</param>
        /// <param name="context">The game context for effect resolution.</param>
        /// <returns>The updated game state after processing triggers.</returns>
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
                    var map = BuildTriggerMap(workingState, evt);
                    if (!map.TryGetValue(type, out var sources)) continue;

                    foreach (var source in sources)
                    {
                        if (!IsStillValid(workingState, source)) continue;

                        var component = new JsonEffectComponent(source.Effect, source.Id);
                        if (component.ShouldTrigger(evt, workingState))
                        {
                            eventBus.Publish(new TriggerActivatedEvent(source.Id, workingState.GetPlayer(workingState.ActivePlayerId).PlayerId));
                            workingState = component.Resolve(evt, workingState, context);
                            if (workingState.PendingInteraction != null) return workingState;
                        }
                    }
                }
            }
            return workingState;
        }
        #endregion

        #region Validation Methods
        private bool IsStillValid(GameState state, SourceInfo info)
        {
            if (info.Effect.Trigger == TriggerType.OnDeath ||
                info.Effect.Trigger == TriggerType.OnSacrificed ||
                info.Effect.Trigger == TriggerType.OnOtherUnitSacrificed ||
                info.Effect.Trigger == TriggerType.OnFriendlyUnitDied)
                return true;

            if (info.Zone == EffectZone.Board)
                return state.Board.GetAllUnits().Any(u => u.InstanceId == info.Id);

            if (info.Zone == EffectZone.Hand)
                return state.PlayerA.Hand.Concat(state.PlayerB.Hand).Any(c => c.InstanceId == info.Id);

            return true;
        }
        #endregion

        #region Trigger Mapping
        private Dictionary<TriggerType, List<SourceInfo>> BuildTriggerMap(GameState state, IGameEvent currentEvent)
        {
            var map = new Dictionary<TriggerType, List<SourceInfo>>();

            int? transitioningUnitId = null;
            if (currentEvent is UnitDiedEvent ude) transitioningUnitId = ude.Unit.InstanceId;
            else if (currentEvent is UnitSacrificedEvent use) transitioningUnitId = use.Unit.InstanceId;

            var all = state.Board.GetAllUnits()
                .Where(u => u.InstanceId != transitioningUnitId)
                .Select(u => (u, zone: EffectZone.Board))
                .Concat(state.PlayerA.Hand.Concat(state.PlayerB.Hand).Select(c => (c, zone: EffectZone.Hand)))
                .Concat(state.SpellStack.Select(sp => (sp, zone: EffectZone.Any)));

            if (currentEvent is UnitDiedEvent ude2)
            {
                all = all.Append((ude2.Unit, zone: EffectZone.Graveyard));
            }
            else if (currentEvent is UnitSacrificedEvent use2)
            {
                all = all.Append((use2.Unit, zone: EffectZone.Graveyard));
            }

            foreach (var (card, currentZone) in all)
            {
                foreach (var effect in card.Definition.Effects)
                {
                    bool zoneMatches = effect.Zone == currentZone || effect.Zone == EffectZone.Any;
                    bool isDeathRelatedTrigger = effect.Trigger == TriggerType.OnDeath ||
                                                 effect.Trigger == TriggerType.OnSacrificed;
                    bool shouldInclude = effect.Trigger == TriggerType.OnPlayed ||
                                         zoneMatches ||
                                         (isDeathRelatedTrigger && currentZone == EffectZone.Graveyard);

                    if (shouldInclude)
                    {
                        if (!map.ContainsKey(effect.Trigger))
                        {
                            map[effect.Trigger] = new List<SourceInfo>();
                        }

                        map[effect.Trigger].Add(new SourceInfo(card.InstanceId, effect, currentZone));
                    }
                }
            }

            return map;
        }
        #endregion

        #region Event Type Mapping
        private List<TriggerType> GetRelevantTriggerTypes(IGameEvent gameEvent)
        {
            var types = new List<TriggerType>();

            if (gameEvent is CardPlayedEvent)
            {
                types.Add(TriggerType.OnPlayed);
                types.Add(TriggerType.OnFriendlyActionPlayed);
                types.Add(TriggerType.OnSummoned);
            }
            if (gameEvent is UnitDiedEvent)
            {
                types.Add(TriggerType.OnDeath);
                types.Add(TriggerType.OnFriendlyUnitDied);
                types.Add(TriggerType.OnKill);
            }
            if (gameEvent is UnitSacrificedEvent)
            {
                types.Add(TriggerType.OnSacrificed);
                types.Add(TriggerType.OnOtherUnitSacrificed);
                types.Add(TriggerType.OnFriendlyUnitDied);
            }
            if (gameEvent is UnitDamagedEvent)
            {
                types.Add(TriggerType.OnDamagTaken);
                types.Add(TriggerType.OnDamagedEnemyUnit);
                types.Add(TriggerType.OnDamagedEnemyHero);
            }
            if (gameEvent is TurnStartedEvent)
                types.Add(TriggerType.OnTurnStart);

            if (gameEvent is CardDrawnEvent)
            {
                types.Add(TriggerType.OnOpponentCardDrawn);
                types.Add(TriggerType.OnFriendlyCardDrawn);
            }
            if (gameEvent is StatusAppliedEvent)
                types.Add(TriggerType.OnStatusApplied);

            if (gameEvent is PreLineCombatEvent)
                types.Add(TriggerType.OnPreCombatLine);

            if (gameEvent is CardMovedEvent cm && cm.To == CardZone.Board)
            {
                types.Add(TriggerType.OnSummoned); 
            }

            return types;
        }
        #endregion
    }
}