using CardGame.ConsoleApp.Evolution.V2_NewGen;
using CardGame.Core.AI.Interfaces;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Models;
using CardGame.Core.State.Enums;
using CardGame.Core.State.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CardGame.Core.AI.Strategies
{
    public class NewEvolvableStrategy : IAIStrategy
    {
        private readonly NewDna _dna;

        public NewEvolvableStrategy(NewDna dna)
        {
            _dna = dna ?? throw new ArgumentNullException(nameof(dna));
        }

        public float Evaluate(GameState state, int botId)
        {
            var p = state.GetPlayer(botId);
            var o = state.GetOpponent(botId);

            if (o.Health <= 0) return 8000f + (_dna.Genes[NewDna.LIFE_LETHAL_VISION] * 100f);
            if (p.Health <= 0) return -8000f;

            // STYLE LENSES
            float futureLens = (_dna.Genes[NewDna.STYLE_FUTURE_PLANNING_BIAS] / 5f) + 0.1f;
            float tempoLens = (_dna.Genes[NewDna.STYLE_TEMPO_PREFERENCE] / 5f) + 0.1f;
            float midLens = (_dna.Genes[NewDna.STYLE_MIDRANGE] / 5f) + 0.1f;
            float ctrlLens = (_dna.Genes[NewDna.STYLE_CONTROL] / 5f) + 0.1f;
            float comboLens = (_dna.Genes[NewDna.STYLE_COMBO] / 5f) + 0.1f;
            float aggroLens = (_dna.Genes[NewDna.STYLE_AGGRO] / 5f) + 0.1f;
            float burnLens = (_dna.Genes[NewDna.STYLE_BURN] / 5f) + 0.1f;

            float totalScore = 0f;
            totalScore += EvaluateSurvival(p, o, state, ctrlLens, aggroLens, burnLens);
            totalScore += EvaluateResources(p, o, state, comboLens, ctrlLens, futureLens, aggroLens, midLens, burnLens);
            totalScore += EvaluateBoardState(p, o, state, midLens, tempoLens, aggroLens);
            totalScore += EvaluateTacticalExecution(p, o, state, comboLens, futureLens, tempoLens, ctrlLens, aggroLens);
            totalScore += EvaluatePsychology(p, o, state, ctrlLens, aggroLens);

            return totalScore;
        }

        private float EvaluateSurvival(PlayerState p, PlayerState o, GameState state,
            float ctrlM, float aggroM, float burnM)
        {
            float score = 0f;
            float selfHp = p.Health;
            float oppHp = o.Health;

            // 1. BASIC HP VALUE WITH LETHAL BUFFER
            float virtualHp = selfHp - (_dna.Genes[NewDna.LIFE_LETHAL_BUFFER] / 2.5f);
            score += virtualHp * _dna.Genes[NewDna.LIFE_SELF_HP_VALUE] * 15f * ctrlM;

            // 2. OVERHEAL APPRECIATION
            if (selfHp > 20f)
                score += (selfHp - 20f) * _dna.Genes[NewDna.LIFE_OVERHEAL_APPRECIATION] * 25f;

            // 3. PANIC THRESHOLD
            float panicPoint = _dna.Genes[NewDna.LIFE_PANIC_THRESHOLD] + 2f;
            if (selfHp <= panicPoint)
            {
                float dangerGap = (panicPoint - selfHp) + 1f;
                float riskMitigation = _dna.Genes[NewDna.LIFE_RISK_APPETITE_AT_LETHAL] * 0.08f;
                float confidenceFactor = 1.0f - (_dna.Genes[NewDna.COMBAT_LETHAL_CONFIDENCE] / 15f);
                score -= (float)Math.Pow(dangerGap, 2.2f) * 150f * (1f - riskMitigation) * confidenceFactor;
            }

            // 4. LETHAL VISION
            float currentBurst = CalculatePotentialBurst(p, state);
            if (currentBurst >= oppHp)
            {
                score += 3000f * (_dna.Genes[NewDna.LIFE_LETHAL_VISION] / 5f);
            }
            else if (currentBurst >= oppHp * 0.6f)
            {
                score += (currentBurst / oppHp) * _dna.Genes[NewDna.LIFE_LETHAL_VISION] * 80f * aggroM;
            }

            // 5. OPPONENT HP WEIGHT
            score -= oppHp * _dna.Genes[NewDna.LIFE_OPPONENT_HP_WEIGHT] * (aggroM + burnM) * 5f;

            // 6. VULNERABILITY ANALYSIS
            float vulnerability = _dna.Genes[NewDna.LIFE_VULNERABILITY_SENSE] / 10f;
            float aggression = _dna.Genes[NewDna.BOARD_PLACEMENT_REACTION_TYPE] / 10f;
            float netHoleAttitude = aggression - vulnerability;

            for (int i = 0; i < 4; i++)
            {
                var line = state.Board.Lines[i];
                var myU = p.PlayerId == 1 ? line.Player1Unit : line.Player2Unit;
                var enU = p.PlayerId == 1 ? line.Player2Unit : line.Player1Unit;

                if (enU != null)
                {
                    bool canHitFace = CanUnitHitFace(myU, enU);
                    if (canHitFace)
                    {
                        float sequenceWeight = 1.6f - (i * 0.2f);
                        float urgency = 1f + (_dna.Genes[NewDna.LIFE_COMBAT_ORDER_URGENCY] / 10f);
                        float holeDanger = enU.CurrentStats.Attack * sequenceWeight * urgency * 35f;
                        score -= holeDanger * (1f - netHoleAttitude * 0.5f);
                    }
                }
            }

            // 7. INDIRECT DAMAGE FOCUS
            float reactiveBias = _dna.Genes[NewDna.TARGETING_REACTIVE_VS_PROACTIVE] / 10f;
            foreach (var u in state.Board.GetAllUnits().Where(u => u.OwnerPlayerId != p.PlayerId))
            {
                if (CanDealDirectHeroDamage(u))
                {
                    score -= 50f * _dna.Genes[NewDna.LIFE_INDIRECT_DAMAGE_FOCUS] * (1f + reactiveBias);
                }
            }

            // 8. HEAL PRIORITY
            if (selfHp < 15f)
            {
                float healInHand = p.Hand
                    .SelectMany(c => c.Definition.Effects)
                    .SelectMany(e => e.Actions)
                    .Where(a => a.Type == ActionType.Heal &&
                           (a.Target == TargetType.FriendlyHero || a.Target == TargetType.Self))
                    .Sum(a => a.Amount);
                score += healInHand * _dna.Genes[NewDna.LIFE_HEAL_PRIORITY_HERO] * 25f;
            }

            return score;
        }

        private float EvaluateResources(PlayerState p, PlayerState o, GameState state,
            float comboM, float ctrlM, float futureM, float aggroM, float midM, float burnM)
        {
            float score = 0f;
            var hand = p.Hand;
            int handCount = hand.Count;

            // 1. HAND SIZE OPTIMIZATION
            score += handCount * _dna.Genes[NewDna.LOGIC_HAND_SIZE_OPTIMIZATION] * 15f * (ctrlM + comboM + 0.5f);

            // 2. OVERDRAW RISK
            if (handCount >= 7)
                score -= 250f * (11f - _dna.Genes[NewDna.LOGIC_OVERDRAW_RISK_TOLERANCE]);

            // 3. CARD TYPE PREFERENCE
            float spellPref = (ctrlM + burnM) / 2f;
            float unitPref = (aggroM + midM) / 2f;
            float faceBias = _dna.Genes[NewDna.TARGETING_FACE_VS_BOARD_BIAS] / 10f;

            // 4. DECK THINNING FOCUS
            if (p.DrawPile.Count < 15)
                score += (15 - p.DrawPile.Count) * _dna.Genes[NewDna.LOGIC_DECK_THINNING_FOCUS] * 8f;

            // 5. DISCARD PILE RECYCLE
            int usefulInDiscard = p.DiscardPile.Count(c =>
                c.Definition.Effects.Any(e => e.Trigger == TriggerType.OnDeath ||
                    e.Actions.Any(a => a.Type == ActionType.ReturnToHand)));
            score += usefulInDiscard * _dna.Genes[NewDna.LOGIC_DISCARD_PILE_RECYCLE] * 10f;

            // 6. EVALUATE EACH CARD IN HAND
            foreach (var card in hand)
            {
                float cardVal = EvaluateCardValue(card, p, o, state, spellPref, unitPref, faceBias);
                score += cardVal;
            }

            // 7. MANA CURVE OPTIMIZATION
            if (handCount > 0)
            {
                float avgCost = (float)hand.Average(c => c.CurrentStats.BloodCost);
                float curveGoal = 11f - _dna.Genes[NewDna.LOGIC_MANA_CURVE_BIAS];
                float efficiencyMod = 1f + (_dna.Genes[NewDna.COMBAT_TRADING_EFFICIENCY] / 20f);
                score -= Math.Abs(avgCost - curveGoal) * 30f * futureM * efficiencyMod;
            }

            // 8. DRAW URGENCY
            if (handCount <= 2)
                score -= (3 - handCount) * _dna.Genes[NewDna.LOGIC_DRAW_URGENCY] * 60f;

            return score;
        }

        private float EvaluateCardValue(CardInstance card, PlayerState p, PlayerState o, GameState state,
            float spellPref, float unitPref, float faceBias)
        {
            float cardVal = (card.Definition.Type == CardType.Unit) ? unitPref * 20f : spellPref * 20f;

            // BASE STATS
            var stats = card.CurrentStats;
            cardVal += stats.Attack * 8f + stats.Health * 5f;

            // COST EFFICIENCY
            float costEfficiency = (stats.Attack + stats.Health) / Math.Max(1f, stats.BloodCost);
            cardVal += costEfficiency * 15f;

            // CHOICE CARDS - evaluate best option
            if (card.Definition.Effects.Any(e => e.Targeting == TargetType.Choice))
            {
                cardVal += EvaluateChoiceCard(card, p, o, state);
            }

            // FACE DAMAGE POTENTIAL
            if (CanDealDirectHeroDamage(card))
            {
                cardVal += 25f * faceBias * (card.Definition.Type == CardType.Spell ? 1.5f : 1f);
            }

            // COMBO POTENTIAL
            if (_dna.Genes[NewDna.STYLE_COMBO] > 5f)
            {
                float probabilityFactor = _dna.Genes[NewDna.LOGIC_PROBABILITY_COGNITION] / 10f;

                // Cards that benefit from other cards
                bool triggersOnOtherActions = card.Definition.Effects.Any(e =>
                    e.Trigger == TriggerType.OnFriendlyActionPlayed ||
                    e.Trigger == TriggerType.OnOtherUnitSacrificed ||
                    e.Trigger == TriggerType.OnFriendlyUnitDied);

                if (triggersOnOtherActions)
                    cardVal *= (1f + probabilityFactor * 0.3f);
            }

            // TUTOR VALUE
            if (card.Definition.Effects.Any(e => e.Actions.Any(a => a.Type == ActionType.TutorCard)))
            {
                cardVal += _dna.Genes[NewDna.LOGIC_TUTOR_PRECISION] * 30f;
            }

            // PATIENCE FACTOR FOR EXPENSIVE CARDS
            if (stats.BloodCost > 3)
            {
                float patience = _dna.Genes[NewDna.LOGIC_PATIENCE_FACTOR] / 10f;
                cardVal *= (1f - (1f - patience) * 0.2f);
            }

            // ON-PLAYED EFFECTS
            var onPlayedValue = card.Definition.Effects
                .Where(e => e.Trigger == TriggerType.OnPlayed)
                .Sum(e =>
                {
                     // Sprawdzamy szansę na spełnienie warunku (np. HasSubtypeOnBoard)
                    float conditionProb = EstimateConditionLikelihood(e.Condition, card, p, o, state);

                    // Sumujemy wartość wszystkich akcji w tym efekcie
                    float actionsSum = e.Actions.Sum(a => EvaluateActionValue(a, card, p, o, state));

                    return conditionProb * actionsSum;
                });

            cardVal += onPlayedValue * _dna.Genes[NewDna.TRIGGER_PLAY_STRATEGY] * 0.2f;

            // GIVING RESOURCES PENALTY
            if (card.Definition.Effects.Any(e => e.Actions.Any(a =>
                a.Type == ActionType.GiveToOpponent ||
                (a.Type == ActionType.DrawCard && a.Target == TargetType.EnemyHero))))
            {
                cardVal -= _dna.Genes[NewDna.LOGIC_GIVING_RESOURCES_PENALTY] * 45f;
            }

            // SACRIFICE ACTIVATOR
            if (IsSacrificeActivator(card))
            {
                cardVal += 15f * _dna.Genes[NewDna.TRIGGER_SACRIFICE_VISION];
            }

            // REACTION FROM HAND
            if (card.Definition.Effects.Any(e => e.Zone == EffectZone.Hand &&
                e.Actions.Any(a => a.Type == ActionType.SummonUnit)))
            {
                cardVal += 50f * _dna.Genes[NewDna.LOGIC_REACTION_FROM_HAND_SENSE];
            }

            // COST REDUCTION EFFECTS
            var costReductionEffects = card.Definition.Effects
                .Where(e => (e.Zone == EffectZone.Hand || e.Zone == EffectZone.Any) &&
                       e.Actions.Any(a => a.Type == ActionType.BuffStats && a.Amount < 0));

            foreach (var eff in costReductionEffects)
            {
                float fuel = 0f;
                if (eff.Trigger == TriggerType.OnFriendlyActionPlayed)
                    fuel = p.Hand.Count(c => c.Definition.Type == CardType.Spell) * _dna.Genes[NewDna.TRIGGER_SETUP_RECOGNITION];
                else if (eff.Trigger == TriggerType.OnOtherUnitSacrificed || eff.Trigger == TriggerType.OnFriendlyUnitDied)
                    fuel = state.Board.GetAllUnits().Count(u => u.OwnerPlayerId == p.PlayerId) * _dna.Genes[NewDna.TRIGGER_SACRIFICE_VISION];

                int discount = card.Definition.BaseStats.BloodCost - stats.BloodCost;
                cardVal += (fuel * 15f) + (discount * _dna.Genes[NewDna.LOGIC_STRATEGY_COST_REDUCTION] * 30f);
            }

            // CONDITIONAL EFFECTS - adjust value based on condition likelihood
            foreach (var effect in card.Definition.Effects)
            {
                if (effect.Condition != null)
                {
                    float conditionLikelihood = EstimateConditionLikelihood(effect.Condition, card, p, o, state);
                    cardVal *= (0.5f + 0.5f * conditionLikelihood);
                }
            }

            return cardVal;
        }

        private float EvaluateChoiceCard(CardInstance card, PlayerState p, PlayerState o, GameState state)
        {
            float bestOptionValue = 0f;
            var choiceEffects = card.Definition.Effects.Where(e => e.Targeting == TargetType.Choice);

            foreach (var effect in choiceEffects)
            {
                foreach (var action in effect.Actions)
                {
                   
                    float optionValue = EvaluateActionValue(action, card, p, o, state);
                    if (optionValue > bestOptionValue) bestOptionValue = optionValue;
                }
            }

            return bestOptionValue;
        }

        private float EvaluateActionValue(ActionData action, CardInstance sourceCard,
            PlayerState p, PlayerState o, GameState state)
        {
            float value = 0f;

            switch (action.Type)
            {
                case ActionType.DealDamage:
                    value = EvaluateDamageAction(action, sourceCard, p, o, state);
                    break;

                case ActionType.DrawCard:
                    value = action.Amount * 25f * _dna.Genes[NewDna.LOGIC_DRAW_URGENCY];
                    break;

                case ActionType.DrawFromDiscard:
                    int usefulInDiscard = p.DiscardPile.Count(c =>
                        c.Definition.Effects.Any(e => e.Trigger == TriggerType.OnDeath ||
                            e.Actions.Any(a => a.Type == ActionType.ReturnToHand)));
                    value = Math.Min(action.Amount, usefulInDiscard) * 30f *
                            _dna.Genes[NewDna.LOGIC_DISCARD_PILE_RECYCLE];
                    break;

                case ActionType.Heal:
                    value = action.Amount * 20f * _dna.Genes[NewDna.LIFE_HEAL_PRIORITY_HERO];
                    if (p.Health < 15) value *= 1.5f;
                    break;

                case ActionType.BuffStats:
                    value = (action.BuffAtk * 8f + action.BuffHp * 5f) *
                            (action.Amount < 0 ? -1f : 1f);
                    break;

                case ActionType.SummonUnit:
                case ActionType.MakeAUnit:
                    value = 25f; // Base value for creating a unit
                    break;

                case ActionType.AddCardToHand:
                    value = 20f; // Base value for adding a card
                    break;

                case ActionType.ApplyStatus:
                    value = EvaluateStatusValue(action.StatusKeyword ?? Keyword.None, sourceCard, p, o, state);
                    break;

                case ActionType.Silence:
                    value = 40f * _dna.Genes[NewDna.TARGETING_STATUS_PHILOSOPHY];
                    break;

                case ActionType.TutorCard:
                    value = 30f * _dna.Genes[NewDna.LOGIC_TUTOR_PRECISION];
                    break;

                case ActionType.ReturnToHand:
                    value = 15f; // Base value for bounce
                    break;

                case ActionType.BonusAttack:
                    value = 35f * _dna.Genes[NewDna.KEYWORD_BONUS_ATTACK_VALUE];
                    break;

                case ActionType.SacrificeUnit:
                case ActionType.DestroyUnit:
                    value = EvaluateSacrificeAction(action, sourceCard, p, o, state);
                    break;
            }

            return value;
        }

        private float EvaluateDamageAction(ActionData action, CardInstance sourceCard,
            PlayerState p, PlayerState o, GameState state)
        {
            float value = 0f;

            switch (action.Target)
            {
                case TargetType.EnemyHero:
                    value = action.Amount * 15f * _dna.Genes[NewDna.TARGETING_FACE_VS_BOARD_BIAS];
                    // Finisher bonus
                    if (o.Health <= action.Amount)
                        value += 1000f;
                    break;

                case TargetType.TargetEnemyUnit:
                    // Find best potential target
                    float bestTargetValue = 0f;
                    foreach (var enemyUnit in state.Board.GetAllUnits().Where(u => u.OwnerPlayerId != p.PlayerId))
                    {
                        float killBonus = enemyUnit.CurrentStats.Health <= action.Amount ? 50f : 10f;
                        float threatValue = enemyUnit.CurrentStats.Attack * 8f +
                                          enemyUnit.CurrentStats.Health * 3f;
                        float unitValue = killBonus + threatValue;
                        bestTargetValue = Math.Max(bestTargetValue, unitValue);
                    }
                    value = bestTargetValue;
                    break;

                case TargetType.AllEnemyUnits:
                case TargetType.AllUnitsOnBoard:
                    int potentialKills = state.Board.GetAllUnits()
                        .Count(u => u.OwnerPlayerId != p.PlayerId &&
                               u.CurrentStats.Health <= action.Amount);
                    value = potentialKills * 60f * _dna.Genes[NewDna.TARGETING_AOE_SYMMETRY_TOLERANCE];
                    break;

                case TargetType.SelectedTarget:
                    // Generic selected target - average of unit and hero
                    value = action.Amount * 12f;
                    break;
            }

            return value;
        }

        private float EvaluateStatusValue(Keyword status, CardInstance sourceCard,
            PlayerState p, PlayerState o, GameState state)
        {
            switch (status)
            {
                case Keyword.Marked:
                    return 25f * _dna.Genes[NewDna.KEYWORD_MARKED_FOCUS];

                case Keyword.Stunned:
                    return 40f * (11f - _dna.Genes[NewDna.KEYWORD_STUN_DENIAL]);

                case Keyword.Burning:
                    return 20f;

                case Keyword.SoulGuard:
                    return 30f * _dna.Genes[NewDna.KEYWORD_SOULGUARD_STICKINESS];

                case Keyword.Unkillable:
                    return 50f * _dna.Genes[NewDna.KEYWORD_UNKILLABLE_RECURSION];

                case Keyword.Flying:
                    return 20f * _dna.Genes[NewDna.KEYWORD_FLYING_VALUE];

                default:
                    return 15f; // Generic status value
            }
        }

        private float EvaluateSacrificeAction(ActionData action, CardInstance sourceCard,
            PlayerState p, PlayerState o, GameState state)
        {
            float value = 0f;

            // Check if we have good sacrifice targets
            int goodTargets = state.Board.GetAllUnits()
                .Count(u => u.OwnerPlayerId == p.PlayerId && IsGoodSacrificeTarget(u));

            // Check if we have sacrifice payoff
            bool hasPayoff = p.Hand.Any(c => c.Definition.Effects.Any(e =>
                (e.Trigger == TriggerType.OnOtherUnitSacrificed ||
                 e.Trigger == TriggerType.OnFriendlyUnitDied) &&
                e.Zone == EffectZone.Hand)) ||
                state.Board.GetAllUnits().Any(u => u.OwnerPlayerId == p.PlayerId &&
                    u.Definition.Effects.Any(e => e.Trigger == TriggerType.OnOtherUnitSacrificed));

            if (goodTargets > 0 || hasPayoff)
            {
                value = 70f * _dna.Genes[NewDna.TRIGGER_SACRIFICE_VISION];
                value += goodTargets * 30f * _dna.Genes[NewDna.TRIGGER_SACRIFICE_VISION];
                if (hasPayoff) value += 50f * _dna.Genes[NewDna.TRIGGER_SACRIFICE_VISION];
            }
            else
            {
                // Penalty if no good targets
                value = -40f * _dna.Genes[NewDna.TRIGGER_SACRIFICE_VISION];
            }

            return value * (_dna.Genes[NewDna.TARGETING_FRIENDLY_SACRIFICE_VALUE] / 10f);
        }

        private float EvaluateEffectActions(EffectData effect, CardInstance sourceCard,
            PlayerState p, PlayerState o, GameState state)
        {
            float totalValue = 0f;

            foreach (var action in effect.Actions)
            {
                totalValue += EvaluateActionValue(action, sourceCard, p, o, state);
            }

            return totalValue;
        }

        private float EstimateConditionLikelihood(ConditionData? condition, CardInstance sourceCard,
            PlayerState p, PlayerState o, GameState state)
        {
            // Simplified condition likelihood estimation
            // In real implementation, you'd parse and evaluate the condition properly

            // For now, return a generic estimate based on game state
            if (condition == null) return 1.0f;

            // Check for common condition patterns


            if (condition.Condition == ConditionType.HasSubtypeOnBoard)
            {
                string subtype = condition.TargetParam ?? "";
                bool exists = state.Board.GetAllUnits().Any(u =>
                    u.OwnerPlayerId == p.PlayerId &&
                    u.Definition.Subtypes.Contains(subtype));
                return exists ? 1.0f : 0.0f;
            }

            if (condition.Condition == ConditionType.And && condition.SubConditions != null)
            {
                return condition.SubConditions.All(c => EstimateConditionLikelihood(c, sourceCard, p, o, state) > 0.5f) ? 1.0f : 0.0f;
            }

            // Default moderate likelihood
            return 0.6f;
        }

        private string? ExtractSubtypeFromCondition(ConditionData? condition)
        {
            // 1. Zmieniono typ parametru z Condition na ConditionData?
            if (condition == null) return null;

            // 2. W Twoim JSONie podtyp znajduje się w polu TargetParam, 
            // gdy warunek to IsSubtype lub HasSubtypeOnBoard.
            if (condition.Condition == ConditionType.IsSubtype ||
                condition.Condition == ConditionType.HasSubtypeOnBoard)
            {
                return condition.TargetParam;
            }

            // 3. Opcjonalnie: jeśli warunek jest złożony (np. "And"), 
            // możemy przeszukać podwarunki (częste u Gerarda ID: 24)
            if (condition.SubConditions != null)
            {
                foreach (var sub in condition.SubConditions)
                {
                    var result = ExtractSubtypeFromCondition(sub);
                    if (result != null) return result;
                }
            }

            return condition.TargetParam;
        }

        private float EvaluateBoardState(PlayerState p, PlayerState o, GameState state,
            float midM, float tempoM, float aggroM)
        {
            float score = 0f;
            var allUnits = state.Board.GetAllUnits();
            var myUnits = allUnits.Where(u => u.OwnerPlayerId == p.PlayerId).ToList();

            // 1. FORMATION STRATEGY
            float densityDNA = _dna.Genes[NewDna.BOARD_LANE_DENSITY_STRATEGY];
            float precombatFocus = _dna.Genes[NewDna.COMBAT_PRECOMBAT_PRIORITY] / 10f;

            // Wide formation bonus
            score += myUnits.Count * (10.1f - densityDNA) * 40f * midM * (1f + precombatFocus * 0.3f);

            // Tall formation bonus
            if (myUnits.Any())
                score += myUnits.Max(u => u.CurrentStats.Attack + u.CurrentStats.Health) *
                        densityDNA * 25f * midM;

            // 2. UNIT EVALUATION
            foreach (var u in allUnits)
            {
                bool isFriendly = u.OwnerPlayerId == p.PlayerId;
                var stats = u.CurrentStats;
                int lane = GetLaneOfUnit(state, u.InstanceId);

                // Basic stats vs effects weight
                float unitVal = (stats.Attack * 18f + stats.Health * 12f) *
                               (_dna.Genes[NewDna.UNIT_STAT_VS_EFFECT_WEIGHT] / 10f);

                // Engine detection
                bool isEngine = IsEngineUnit(u);
                if (isEngine)
                {
                    unitVal += 100f * _dna.Genes[NewDna.UNIT_ENGINE_DURABILITY_PRIORITY];
                    unitVal += stats.Health * _dna.Genes[NewDna.UNIT_ENGINE_DURABILITY_PRIORITY] * 12f;
                }

                // Subtype synergy
                foreach (var subtype in u.Definition.Subtypes)
                {
                    int matches = allUnits.Count(other =>
                        other.OwnerPlayerId == u.OwnerPlayerId &&
                        other.InstanceId != u.InstanceId &&
                        other.Definition.Subtypes.Contains(subtype));
                    unitVal += matches * _dna.Genes[NewDna.UNIT_SUBTYPE_COHESION_WEIGHT] * 15f;
                }

                // Protection synergy for high-value low-health units
                if (isFriendly && stats.Health <= 2 && stats.Attack > 3)
                    unitVal += _dna.Genes[NewDna.UNIT_PROTECTION_SYNERGY_BIAS] * 30f;

                // Replacement urgency for weak units
                if (isFriendly && stats.Attack <= 1 && stats.Health <= 1)
                    unitVal -= _dna.Genes[NewDna.UNIT_REPLACEMENT_URGENCY] * 20f;

                // Token valuation
                if (u.Definition.Subtypes.Contains("Token") && u.Definition.Type == CardType.Unit)
                    unitVal *= (_dna.Genes[NewDna.UNIT_TOKEN_VALUATION_LOGIC] / 5f);

                // Keyword evaluation
                unitVal += EvaluateUnitKeywords(u, stats, lane, p, o, state);

                // Effect evaluation
                unitVal += EvaluateUnitEffects(u, p, o, state);

                // Trading efficiency modifier
                float tradingEfficiency = _dna.Genes[NewDna.COMBAT_TRADING_EFFICIENCY] / 10f;
                unitVal *= (1f + tradingEfficiency * 0.2f);

                // Add to score with appropriate weights
                if (isFriendly)
                    score += unitVal * tempoM;
                else
                    score -= unitVal * (1f + (_dna.Genes[NewDna.TARGETING_UNIT_PRIORITY_TYPE] / 10f)) * aggroM;
            }

            // 3. GEOMETRY EVALUATION
            score += EvaluateBoardGeometry(p, o, state, aggroM);

            // 4. SPACE MANAGEMENT
            if (myUnits.Count >= 3)
            {
                float clogPenalty = _dna.Genes[NewDna.BOARD_SPACE_MANAGEMENT_LOGIC] * 40f;
                clogPenalty *= (1.1f - (_dna.Genes[NewDna.TRIGGER_DESTRUCTION_PREFERENCE] / 10f));
                score -= clogPenalty;
            }

            return score;
        }

        private float EvaluateUnitKeywords(CardInstance u, CardStats stats, int lane,
            PlayerState p, PlayerState o, GameState state)
        {
            float val = 0f;

            // Armored efficiency
            if (stats.Keywords.Contains(Keyword.Armored))
            {
                var opp = GetOpponentInLine(state, lane, p.PlayerId);
                int armorVal = u.Definition.BaseStats.KeywordParams.GetValueOrDefault(Keyword.Armored, 1);
                val += (opp != null && opp.CurrentStats.Attack <= armorVal) ?
                    _dna.Genes[NewDna.KEYWORD_ARMORED_EFFICIENCY] * 75f :
                    _dna.Genes[NewDna.KEYWORD_ARMORED_EFFICIENCY] * 25f;
            }

            // Splash damage optimization
            if (stats.Keywords.Contains(Keyword.SplashDamage))
            {
                int power = u.Definition.BaseStats.KeywordParams.GetValueOrDefault(Keyword.SplashDamage, 1);
                int targets = 0;
                foreach (int n in new[] { lane - 1, lane + 1 })
                    if (n >= 0 && n < 4 && !state.Board.Lines[n].IsSlotEmpty(o.PlayerId))
                        targets++;

                float splashMultiplier = 1f + (_dna.Genes[NewDna.BOARD_SPLASH_OPTIMIZATION_LOGIC] / 20f);
                val += targets * power * _dna.Genes[NewDna.KEYWORD_SPLASH_PRIORITY] * 30f * splashMultiplier;
            }

            // Flying value
            if (stats.Keywords.Contains(Keyword.Flying))
            {
                var opp = GetOpponentInLine(state, lane, p.PlayerId);
                bool canHitFace = (opp == null || !opp.CurrentStats.Keywords.Contains(Keyword.Flying));
                val += _dna.Genes[NewDna.KEYWORD_FLYING_VALUE] * (canHitFace ? 30f : 10f);
                if (canHitFace)
                    val += stats.Attack * _dna.Genes[NewDna.BOARD_FLYING_LANE_OPPORTUNISM] * 8f;
            }

            // Other keywords
            if (stats.Keywords.Contains(Keyword.Unkillable))
                val += _dna.Genes[NewDna.KEYWORD_UNKILLABLE_RECURSION] * 50f;

            if (stats.Keywords.Contains(Keyword.BurnSource))
                val += _dna.Genes[NewDna.KEYWORD_BURNSOURCE_AGGRESSION] * 35f;

            if (stats.Keywords.Contains(Keyword.SoulGuard))
                val += _dna.Genes[NewDna.KEYWORD_SOULGUARD_STICKINESS] * 40f;

            if (stats.Keywords.Contains(Keyword.Stunned))
                val -= (11f - _dna.Genes[NewDna.KEYWORD_STUN_DENIAL]) * 90f;

            if (stats.Keywords.Contains(Keyword.Burning))
                val -= 20f; // Penalty for burning units

            return val;
        }

        private float EvaluateUnitEffects(CardInstance u, PlayerState p, PlayerState o, GameState state)
        {
            float val = 0f;
            int lane = GetLaneOfUnit(state, u.InstanceId);

            // Bonus attack value
            if (u.Definition.Effects.Any(e => e.Actions.Any(a => a.Type == ActionType.BonusAttack)))
                val += _dna.Genes[NewDna.KEYWORD_BONUS_ATTACK_VALUE] * 40f;

            // Movement value
            if (u.Definition.Effects.Any(e => e.Actions.Any(a =>
                a.Type == ActionType.MoveRight || a.Type == ActionType.MoveLeft)))
            {
                bool hasGoodMove = false;
                if (lane < 3 && state.Board.Lines[lane + 1].IsSlotEmpty(p.PlayerId))
                    hasGoodMove = true;
                if (lane > 0 && state.Board.Lines[lane - 1].IsSlotEmpty(p.PlayerId))
                    hasGoodMove = true;

                if (hasGoodMove)
                    val += _dna.Genes[NewDna.BOARD_MOVEMENT_VALUE_SENSE] * 35f;
            }

            // Death effects
            if (u.Definition.Effects.Any(e => e.Trigger == TriggerType.OnDeath))
                val += _dna.Genes[NewDna.TRIGGER_DEATH_STRATEGY] * 25f;

            // Combat effects
            if (u.Definition.Effects.Any(e => e.Trigger == TriggerType.OnPreCombatLine ||
                                             e.Trigger == TriggerType.OnKill))
                val += _dna.Genes[NewDna.TRIGGER_COMBAT_STRATEGY] * 20f;

            // Buff protection for units with permanent buffs
            if (u.PermanentBuffs.Attack > 0 || u.PermanentBuffs.Health > 0)
                val += (u.PermanentBuffs.Attack + u.PermanentBuffs.Health) *
                      _dna.Genes[NewDna.BUFF_PROTECTION_OR_AMPLIFY] * 10f;

            // Evaluate all effects for their potential value
            foreach (var effect in u.Definition.Effects)
            {
                float effectValue = EvaluateEffectActions(effect, u, p, o, state);

                // Scale by trigger frequency
                switch (effect.Trigger)
                {
                    case TriggerType.Passive:
                        effectValue *= 2.0f; // Constant effect
                        break;
                    case TriggerType.OnPreCombatLine:
                        effectValue *= 1.5f; // Happens every turn
                        break;
                    case TriggerType.OnKill:
                    case TriggerType.OnDamagedEnemyHero:
                        effectValue *= 0.8f; // Conditional but powerful
                        break;
                    default:
                        effectValue *= 1.0f;
                        break;
                }

                val += effectValue;
            }

            return val;
        }

        private float EvaluateBoardGeometry(PlayerState p, PlayerState o, GameState state, float aggroM)
        {
            float score = 0f;

            for (int i = 0; i < 4; i++)
            {
                var line = state.Board.Lines[i];
                var myU = p.PlayerId == 1 ? line.Player1Unit : line.Player2Unit;
                var enU = p.PlayerId == 1 ? line.Player2Unit : line.Player1Unit;

                // Threat symmetry
                if (myU != null && enU != null && myU.CurrentStats.Health > enU.CurrentStats.Attack)
                    score += enU.CurrentStats.Attack * _dna.Genes[NewDna.BOARD_THREAT_SYMMETRY] * 12f;

                // Horizontal focus bias
                if (myU != null)
                {
                    // Left side vs right side preference
                    score += (i <= 1) ? (10f - _dna.Genes[NewDna.BOARD_HORIZONTAL_FOCUS_BIAS]) * 8f :
                                        _dna.Genes[NewDna.BOARD_HORIZONTAL_FOCUS_BIAS] * 8f;
                }

                // Lane holes (empty friendly slots with enemy presence)
                if (myU == null && enU != null)
                {
                    float holePenalty = enU.CurrentStats.Attack * 25f;
                    holePenalty *= (1f + (_dna.Genes[NewDna.BOARD_PLACEMENT_REACTION_TYPE] / 10f));
                    score -= holePenalty;
                }
            }

            return score;
        }

        private float EvaluateTacticalExecution(PlayerState p, PlayerState o, GameState state,
            float comboM, float futureM, float tempoM, float ctrlM, float aggroM)
        {
            float score = 0f;
            bool myTurn = state.ActivePlayerId == p.PlayerId;

            // 1. MANA RESERVE STRATEGY
            bool hasReactiveSpells = p.Hand.Any(c => c.Definition.Type == CardType.Spell &&
                c.Definition.Effects.Any(e =>
                    e.Targeting == TargetType.TargetEnemyUnit ||
                    e.Actions.Any(a => a.Target == TargetType.TargetEnemyUnit)));

            if (p.CurrentBlood > 0 && hasReactiveSpells)
                score += p.CurrentBlood * _dna.Genes[NewDna.PHASE_MANA_RESERVE_STRATEGY] * 18f;

            // 2. TURN-BASED TACTICS
            if (myTurn)
            {
                score += _dna.Genes[NewDna.PHASE_INITIATIVE_POSTURE] * 40f * tempoM;

                if (state.CurrentPhase == GamePhase.UnitOnly)
                    score -= _dna.Genes[NewDna.PHASE_REACTION_SENSITIVITY] * 30f;

                if (o.CurrentBlood >= 2)
                    score -= o.CurrentBlood * _dna.Genes[NewDna.PHASE_COUNTER_ANTICIPATION] * 25f;
            }

            // 3. ACTION ECONOMY
            int zeroCostCards = p.Hand.Count(c => c.CurrentStats.BloodCost == 0);
            if (zeroCostCards > 0)
            {
                float efficiencyValue = (float)Math.Pow(zeroCostCards, 1.6f) *
                                      _dna.Genes[NewDna.PHASE_EFFICIENCY_VS_VALUE] * 20f;
                score += efficiencyValue;
            }

            // 4. SACRIFICE TACTICS
            int sacrificeActivators = p.Hand.Count(IsSacrificeActivator);
            if (sacrificeActivators > 0)
            {
                float sacrificeScore = EvaluateSacrificeTactics(p, o, state);
                score += sacrificeScore * (_dna.Genes[NewDna.TARGETING_FRIENDLY_SACRIFICE_VALUE] / 10f);
            }

            // 5. DAMAGE OPTIMIZATION (avoid overkill)
            float wastePenalty = 0f;
            foreach (var card in p.Hand.Where(c => c.CurrentStats.BloodCost <= p.CurrentBlood))
            {
                foreach (var effect in card.Definition.Effects.Where(e => e.Trigger == TriggerType.OnPlayed))
                {
                    foreach (var action in effect.Actions.Where(a => a.Type == ActionType.DealDamage))
                    {
                        float worstOverkill = FindWorstOverkill(action, state, p);
                        wastePenalty -= worstOverkill * (11f - _dna.Genes[NewDna.TARGETING_DAMAGE_WASTE_TOLERANCE]) * 5f;
                    }
                }
            }
            score += wastePenalty;

            // 6. TRADE EVALUATION
            float exchangeEfficiency = EvaluateTradeEfficiency(p, o, state);
            score += exchangeEfficiency * _dna.Genes[NewDna.COMBAT_RESOURCE_EXCHANGE_EFFICIENCY] * 0.3f;

            // 7. STATUS CONTROL
            bool hasSilence = p.Hand.Any(c => c.Definition.Effects.Any(e =>
                e.Actions.Any(a => a.Type == ActionType.Silence)));
            if (hasSilence)
                score += 60f * _dna.Genes[NewDna.TARGETING_STATUS_PHILOSOPHY];

            // 8. SETUP RECOGNITION
            int setupCards = p.Hand.Count(c => c.Definition.Effects.Any(e =>
                e.Trigger == TriggerType.OnFriendlyActionPlayed ||
                e.Trigger == TriggerType.OnStatusApplied));
            score += setupCards * _dna.Genes[NewDna.TRIGGER_SETUP_RECOGNITION] * 15f * comboM;

            return score;
        }

        private float EvaluateSacrificeTactics(PlayerState p, PlayerState o, GameState state)
        {
            float sacrificeScore = 0f;

            var safeActivators = p.Hand.Where(IsSacrificeActivator)
                .Where(card => CanSafelyUseSacrificeActivator(p, state, card))
                .ToList();

            int safeActivatorsCount = safeActivators.Count;
            bool canSafelyUseAny = safeActivatorsCount > 0;

            var goodTargets = state.Board.GetAllUnits()
                .Where(u => u.OwnerPlayerId == p.PlayerId && IsGoodSacrificeTarget(u))
                .ToList();

            int goodTargetsCount = goodTargets.Count;
            int totalUnits = state.Board.GetAllUnits().Count(u => u.OwnerPlayerId == p.PlayerId);

            bool hasPayoffInHand = p.Hand.Any(c => c.Definition.Effects.Any(e =>
                (e.Trigger == TriggerType.OnOtherUnitSacrificed ||
                 e.Trigger == TriggerType.OnFriendlyUnitDied) &&
                e.Zone == EffectZone.Hand));

            if (canSafelyUseAny && (goodTargetsCount > 0 || hasPayoffInHand))
            {
                sacrificeScore = 70f * _dna.Genes[NewDna.TRIGGER_SACRIFICE_VISION];
                sacrificeScore += safeActivatorsCount * 20f;
                sacrificeScore += goodTargetsCount * 30f * _dna.Genes[NewDna.TRIGGER_SACRIFICE_VISION];

                if (hasPayoffInHand)
                    sacrificeScore += 50f * _dna.Genes[NewDna.TRIGGER_SACRIFICE_VISION];
            }
            else if (!canSafelyUseAny)
            {
                float dangerPenalty = p.Hand.Count(IsSacrificeActivator) * 80f;
                if (totalUnits <= 1) dangerPenalty *= 2f;
                sacrificeScore -= dangerPenalty;
            }

            return sacrificeScore;
        }

        private float FindWorstOverkill(ActionData action, GameState state, PlayerState p)
        {
            float worstOverkill = 0f;

            foreach (var enemy in state.Board.GetAllUnits().Where(u => u.OwnerPlayerId != p.PlayerId))
            {
                if (action.Amount > enemy.CurrentStats.Health)
                {
                    float overkill = action.Amount - enemy.CurrentStats.Health;
                    worstOverkill = Math.Max(worstOverkill, overkill);
                }
            }

            return worstOverkill;
        }

        private float EvaluateTradeEfficiency(PlayerState p, PlayerState o, GameState state)
        {
            float exchangeEfficiency = 0f;

            foreach (var line in state.Board.Lines)
            {
                var myU = p.PlayerId == 1 ? line.Player1Unit : line.Player2Unit;
                var enU = p.PlayerId == 1 ? line.Player2Unit : line.Player1Unit;

                if (myU != null && enU != null)
                {
                    bool mySurvives = myU.CurrentStats.Health > enU.CurrentStats.Attack;
                    bool enemyDies = enU.CurrentStats.Health <= myU.CurrentStats.Attack;

                    if (mySurvives && enemyDies)
                        exchangeEfficiency += 25f; // Good trade
                    else if (!mySurvives && enemyDies)
                        exchangeEfficiency += 0f; // Even trade
                    else if (mySurvives && !enemyDies)
                        exchangeEfficiency -= 15f; // Bad trade
                    else if (!mySurvives && !enemyDies)
                        exchangeEfficiency -= 5f; // Inefficient trade
                }
            }

            return exchangeEfficiency;
        }

        private float EvaluatePsychology(PlayerState p, PlayerState o, GameState state,
            float ctrlM, float aggroM)
        {
            float score = 0f;

            // RESOURCE ADVANTAGE
            int handDiff = p.Hand.Count - o.Hand.Count;
            score += handDiff * _dna.Genes[NewDna.PSYCH_RESOURCE_ADVANTAGE_GREED] * 40f;

            // PREDICTIVE CAUTION
            float predictiveMod = _dna.Genes[NewDna.PSYCH_PREDICTIVE_CAUTION] / 10f;
            float predictivePenalty = 0f;
            if (o.Hand.Count > 3)
                predictivePenalty += (o.Hand.Count - 3) * 20f;
            if (o.CurrentBlood >= 3)
                predictivePenalty += o.CurrentBlood * 15f;
            score -= predictivePenalty * predictiveMod;

            // OPPONENT MANA RESPECT
            if (o.CurrentBlood >= 2)
                score -= o.CurrentBlood * _dna.Genes[NewDna.PSYCH_OPPONENT_MANA_RESPECT] * 15f;

            // DECEPTION AND BLUFFING
            int baitCards = p.Hand.Count(c =>
                c.Definition.Effects.Any(e => e.Trigger == TriggerType.OnPlayed &&
                    e.Actions.Any(a => (a.Type == ActionType.BuffStats && a.BuffHp > 0) ||
                                      (a.Type == ActionType.Heal && a.Target == TargetType.FriendlyHero))));
            score += baitCards * _dna.Genes[NewDna.PSYCH_DECEPTION_BAITING] * 25f;

            // BLUFF RESISTANCE
            if (o.Hand.Count > 4)
                score -= (o.Hand.Count - 4) * (11f - _dna.Genes[NewDna.PSYCH_BLUFF_RESISTANCE]) * 30f;

            // ADAPTABILITY
            float styleConsistency = Math.Abs(_dna.Genes[NewDna.STYLE_AGGRO] - _dna.Genes[NewDna.STYLE_CONTROL]) / 10f;
            if (styleConsistency > 0.5f)
                score += _dna.Genes[NewDna.PSYCH_ADAPTABILITY] * 25f;

            return score * (ctrlM + 0.5f);
        }

        // UTILITY FUNCTIONS
        private float CalculatePotentialBurst(PlayerState p, GameState state)
        {
            float burst = 0f;

            // Board damage to face
            foreach (var line in state.Board.Lines)
            {
                var myU = p.PlayerId == 1 ? line.Player1Unit : line.Player2Unit;
                var enU = p.PlayerId == 1 ? line.Player2Unit : line.Player1Unit;

                if (myU != null && !myU.CurrentStats.Keywords.Contains(Keyword.Stunned) &&
                    CanUnitHitFace(myU, enU))
                {
                    burst += myU.CurrentStats.Attack;
                }
            }

            // Hand damage
            burst += p.Hand
                .Where(c => c.CurrentStats.BloodCost <= p.CurrentBlood)
                .SelectMany(c => c.Definition.Effects)
                .SelectMany(e => e.Actions)
                .Where(a => a.Type == ActionType.DealDamage &&
                       (a.Target == TargetType.EnemyHero || a.Target == TargetType.SelectedTarget))
                .Sum(a => a.Amount);

            return burst;
        }

        private bool IsSacrificeActivator(CardInstance c)
        {
            return c.Definition.Effects.Any(e => e.Actions.Any(a =>
                (a.Type == ActionType.SacrificeUnit &&
                 (a.Target == TargetType.TargetFriendlyUnit ||
                  a.Target == TargetType.OtherFriendlyUnits ||
                  a.Target == TargetType.SelectedTarget)) ||
                (a.Type == ActionType.DestroyUnit &&
                 (a.Target == TargetType.TargetFriendlyUnit ||
                  a.Target == TargetType.OtherFriendlyUnits ||
                  a.Target == TargetType.SelectedTarget))
            ));
        }

        private bool IsEngineUnit(CardInstance unit)
        {
            return unit.Definition.Effects.Any(e =>
                (e.Trigger == TriggerType.Passive && e.Zone == EffectZone.Board &&
                 e.Actions.Any(a => a.Target == TargetType.OtherFriendlyUnits ||
                                   a.Target == TargetType.FriendlySpellsInHand)) ||
                (e.Trigger == TriggerType.OnFriendlyActionPlayed && e.Zone == EffectZone.Board) ||
                (e.Trigger == TriggerType.OnFriendlyUnitDied && e.Zone == EffectZone.Board) ||
                (e.Actions.Any(a => a.Type == ActionType.AddResource ||
                                   a.Type == ActionType.DrawCard)) ||
                (e.Trigger == TriggerType.OnDamagedEnemyHero && e.Zone == EffectZone.Board) ||
                (e.Trigger == TriggerType.OnKill && e.Zone == EffectZone.Board)
            );
        }

        private bool IsGoodSacrificeTarget(CardInstance unit)
        {
            bool isToken = unit.Definition.Subtypes.Contains("Token");
            bool wantsToBeSacrificed = unit.Definition.Effects.Any(e => e.Trigger == TriggerType.OnSacrificed);
            bool hasGoodDeathEffect = unit.Definition.Effects.Any(e => e.Trigger == TriggerType.OnDeath &&
                e.Actions.Any(a => a.Type == ActionType.Heal ||
                                  a.Type == ActionType.DealDamage ||
                                  a.Type == ActionType.DrawCard ||
                                  a.Type == ActionType.SummonUnit));

            bool isWeakOrDamaged = unit.CurrentStats.Health <= 1 && unit.CurrentStats.Attack <= 1;
            bool isStunned = unit.CurrentStats.Keywords.Contains(Keyword.Stunned);
            bool isBurningAndLow = unit.CurrentStats.Keywords.Contains(Keyword.Burning) && unit.CurrentStats.Health <= 2;
            bool isEngine = IsEngineUnit(unit);

            return (isToken || wantsToBeSacrificed || hasGoodDeathEffect || isWeakOrDamaged || isStunned || isBurningAndLow)
                   && (!isEngine || wantsToBeSacrificed);
        }

        private bool CanSafelyUseSacrificeActivator(PlayerState p, GameState state, CardInstance activator)
        {
            var friendlyUnits = state.Board.GetAllUnits()
                .Where(u => u.OwnerPlayerId == p.PlayerId)
                .ToList();

            if (friendlyUnits.Count == 0)
                return false;

            foreach (var effect in activator.Definition.Effects)
            {
                foreach (var action in effect.Actions.Where(a =>
                    a.Type == ActionType.SacrificeUnit || a.Type == ActionType.DestroyUnit))
                {
                    if (action.Target == TargetType.TargetFriendlyUnit ||
                        action.Target == TargetType.SelectedTarget)
                    {
                        bool hasGoodTarget = friendlyUnits.Any(u => IsGoodSacrificeTarget(u));
                        bool hasBackup = friendlyUnits.Count >= 2;
                        return hasGoodTarget || hasBackup;
                    }

                    if (action.Target == TargetType.OtherFriendlyUnits)
                    {
                        if (friendlyUnits.Count <= 1)
                            return false;

                        bool hasGoodTarget = friendlyUnits.Any(u => IsGoodSacrificeTarget(u));
                        return hasGoodTarget || friendlyUnits.Count >= 3;
                    }
                }
            }

            return true;
        }

        private bool CanUnitHitFace(CardInstance myUnit, CardInstance? enemyUnit)
        {
            if (myUnit == null) return false;

         
            var stats = myUnit.CurrentStats;

            if (stats.Keywords.Contains(Keyword.Stunned)) return false;

         
            if (enemyUnit == null) return true;

          
            bool myFlying = stats.Keywords.Contains(Keyword.Flying);
            bool enemyFlying = enemyUnit.CurrentStats.Keywords.Contains(Keyword.Flying);

            return myFlying && !enemyFlying;
        }

        private bool CanDealDirectHeroDamage(CardInstance unitOrCard)
        {
            return unitOrCard.Definition.Effects.Any(e => e.Actions.Any(a =>
                a.Type == ActionType.DealDamage &&
                (a.Target == TargetType.EnemyHero ||
                 a.Target == TargetType.SelectedTarget))) ||
                   unitOrCard.CurrentStats.Keywords.Contains(Keyword.BurnSource);
        }

        private int GetLaneOfUnit(GameState state, int unitId)
        {
            for (int i = 0; i < 4; i++)
            {
                if (state.Board.Lines[i].Player1Unit?.InstanceId == unitId ||
                    state.Board.Lines[i].Player2Unit?.InstanceId == unitId)
                    return i;
            }
            return 0;
        }

        private CardInstance GetOpponentInLine(GameState state, int lane, int myId)
        {
            return myId == 1 ? state.Board.Lines[lane].Player2Unit : state.Board.Lines[lane].Player1Unit;
        }
    }
}