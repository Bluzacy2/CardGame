using CardGame.ConsoleApp.Evolution.V2_NewGen;
using CardGame.Core.AI.Interfaces;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Models;
using CardGame.Core.State.Enums;
using CardGame.Core.State.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace CardGame.Core.AI.Strategies
{
    public class OptimizedNewEvolvableStrategy : IAIStrategy
    {
        private readonly NewDna _dna;
        private readonly Dictionary<int, CachedCardEvaluation> _cardEvaluationCache = new();
        private EvaluationCache _currentCache = null;

        // Cache dla synergii - obliczane raz na turę
        private Dictionary<string, float> _synergyCache = new();
        private int _lastSynergyTurn = -1;

        // Statystyki kart - dynamicznie ładowane
        private Dictionary<int, CardStatsEnhanced> _cardStats = new();

        public OptimizedNewEvolvableStrategy(NewDna dna, Dictionary<int, CardStatsEnhanced> cardStats = null)
        {
            _dna = dna ?? throw new ArgumentNullException(nameof(dna));

            // Ładowanie statystyk kart jeśli dostępne
            if (cardStats != null && cardStats.Any())
            {
                _cardStats = cardStats;
            }
            else
            {
                LoadCardStatsFromFile();
            }
        }

        public float Evaluate(GameState state, int botId)
        {
            // OPTYMALIZACJA: Cache dla powtarzających się stanów
            int cacheKey = ComputeStateCacheKey(state, botId);
            if (_currentCache != null && _currentCache.CacheKey == cacheKey)
            {
                return _currentCache.CachedScore;
            }

            var p = state.GetPlayer(botId);
            var o = state.GetOpponent(botId);

            if (o.Health <= 0) return 8000f + (_dna.Genes[NewDna.LIFE_LETHAL_VISION] * 100f);
            if (p.Health <= 0) return -8000f;

            // OPTYMALIZACJA: Jednokrotne pobranie danych
            var allUnits = state.Board.GetAllUnits().ToList();
            var myUnits = allUnits.Where(u => u.OwnerPlayerId == p.PlayerId).ToList();
            var enemyUnits = allUnits.Where(u => u.OwnerPlayerId != p.PlayerId).ToList();

            var unitLanes = new Dictionary<int, int>();
            var lineOpponents = new CardInstance[4];
            var lineAllies = new CardInstance[4];

            for (int i = 0; i < 4; i++)
            {
                var line = state.Board.Lines[i];
                var myU = p.PlayerId == 1 ? line.Player1Unit : line.Player2Unit;
                var enU = p.PlayerId == 1 ? line.Player2Unit : line.Player1Unit;

                lineAllies[i] = myU;
                lineOpponents[i] = enU;

                if (myU != null) unitLanes[myU.InstanceId] = i;
                if (enU != null) unitLanes[enU.InstanceId] = i;
            }

            // STYLE LENSES - oblicz raz
            float futureLens = (_dna.Genes[NewDna.STYLE_FUTURE_PLANNING_BIAS] / 5f) + 0.1f;
            float tempoLens = (_dna.Genes[NewDna.STYLE_TEMPO_PREFERENCE] / 5f) + 0.1f;
            float midLens = (_dna.Genes[NewDna.STYLE_MIDRANGE] / 5f) + 0.1f;
            float ctrlLens = (_dna.Genes[NewDna.STYLE_CONTROL] / 5f) + 0.1f;
            float comboLens = (_dna.Genes[NewDna.STYLE_COMBO] / 5f) + 0.1f;
            float aggroLens = (_dna.Genes[NewDna.STYLE_AGGRO] / 5f) + 0.1f;
            float burnLens = (_dna.Genes[NewDna.STYLE_BURN] / 5f) + 0.1f;

            float totalScore = 0f;

            // OPTYMALIZACJA: Oblicz raz potential burst
            float potentialBurst = CalculatePotentialBurst(p, lineAllies, lineOpponents, unitLanes);

            totalScore += EvaluateSurvival(p, o, state, ctrlLens, aggroLens, burnLens,
                potentialBurst, lineOpponents, enemyUnits, lineAllies);
            totalScore += EvaluateResources(p, o, state, comboLens, ctrlLens, futureLens,
                aggroLens, midLens, burnLens);
            totalScore += EvaluateBoardState(p, o, state, midLens, tempoLens, aggroLens,
                allUnits, myUnits, enemyUnits, lineAllies, lineOpponents, unitLanes);
            totalScore += EvaluateTacticalExecution(p, o, state, comboLens, futureLens,
                tempoLens, ctrlLens, aggroLens, lineOpponents, lineAllies);
            totalScore += EvaluatePsychology(p, o, state, ctrlLens, aggroLens);

            // Zapisz do cache
            _currentCache = new EvaluationCache
            {
                CacheKey = cacheKey,
                CachedScore = totalScore,
                Turn = state.TurnNumber
            };

            return totalScore;
        }

        #region Cache i pomocnicze

        private int ComputeStateCacheKey(GameState state, int botId)
        {
            unchecked
            {
                int hash = 17;
                var p = state.GetPlayer(botId);
                var o = state.GetOpponent(botId);

                hash = hash * 23 + p.Health.GetHashCode();
                hash = hash * 23 + o.Health.GetHashCode();
                hash = hash * 23 + p.CurrentBlood.GetHashCode();
                hash = hash * 23 + p.Hand.Count.GetHashCode();
                hash = hash * 23 + state.TurnNumber.GetHashCode();

                foreach (var unit in state.Board.GetAllUnits())
                {
                    hash = hash * 23 + unit.InstanceId.GetHashCode();
                    hash = hash * 23 + unit.CurrentStats.Attack.GetHashCode();
                    hash = hash * 23 + unit.CurrentStats.Health.GetHashCode();
                }

                return hash;
            }
        }

        private void RebuildSynergyCache(PlayerState p, GameState state)
        {
            if (_lastSynergyTurn == state.TurnNumber) return;

            _synergyCache.Clear();

            // Oblicz synergię dla różnych archetypów
            CalculateMarkingSynergy(p, state);
            CalculateSacrificeSynergy(p, state);
            CalculateMachineSynergy(p, state);
            CalculateSpellSynergy(p, state);
            CalculateDeathSynergy(p, state);

            _lastSynergyTurn = state.TurnNumber;
        }

        private void CalculateMarkingSynergy(PlayerState p, GameState state)
        {
            int markingCardsInHand = 0;
            int markingCardsOnBoard = 0;
            int payoffCardsInHand = 0;
            int payoffCardsOnBoard = 0;

            foreach (var card in p.Hand)
            {
                if (HasEffectType(card, ActionType.ApplyStatus, Keyword.Marked))
                    markingCardsInHand++;
                else if (BenefitsFromMarked(card))
                    payoffCardsInHand++;
            }

            foreach (var unit in state.Board.GetAllUnits().Where(u => u.OwnerPlayerId == p.PlayerId))
            {
                if (HasEffectType(unit, ActionType.ApplyStatus, Keyword.Marked))
                    markingCardsOnBoard++;
                else if (BenefitsFromMarked(unit))
                    payoffCardsOnBoard++;
            }

            float synergy = (markingCardsInHand + markingCardsOnBoard) *
                          (payoffCardsInHand + payoffCardsOnBoard) * 0.5f;
            _synergyCache["Marking"] = synergy;
        }

        private void CalculateSacrificeSynergy(PlayerState p, GameState state)
        {
            int sacrificeActivators = 0;
            int sacrificeTargets = 0;
            int sacrificePayoffs = 0;

            foreach (var card in p.Hand)
            {
                if (IsSacrificeActivator(card)) sacrificeActivators++;
                if (BenefitsFromSacrifice(card)) sacrificePayoffs++;
            }

            foreach (var unit in state.Board.GetAllUnits().Where(u => u.OwnerPlayerId == p.PlayerId))
            {
                if (IsGoodSacrificeTarget(unit)) sacrificeTargets++;
                if (BenefitsFromSacrifice(unit)) sacrificePayoffs++;
            }

            float synergy = Math.Min(sacrificeActivators, sacrificeTargets) * 20f +
                          sacrificePayoffs * 15f;
            _synergyCache["Sacrifice"] = synergy;
        }

        private void CalculateMachineSynergy(PlayerState p, GameState state)
        {
            int machinesOnBoard = 0;
            int machinePayoffs = 0;

            foreach (var unit in state.Board.GetAllUnits().Where(u => u.OwnerPlayerId == p.PlayerId))
            {
                if (unit.Definition.Subtypes.Contains("Machine"))
                    machinesOnBoard++;
                if (BenefitsFromMachines(unit))
                    machinePayoffs++;
            }

            foreach (var card in p.Hand)
            {
                if (BenefitsFromMachines(card))
                    machinePayoffs++;
            }

            float synergy = machinesOnBoard * machinePayoffs * 0.3f;
            _synergyCache["Machine"] = synergy;
        }

        private void CalculateSpellSynergy(PlayerState p, GameState state)
        {
            int spellsInHand = p.Hand.Count(c => c.Definition.Type == CardType.Spell);
            int spellPayoffs = 0;

            foreach (var card in p.Hand)
            {
                if (BenefitsFromSpells(card))
                    spellPayoffs++;
            }

            foreach (var unit in state.Board.GetAllUnits().Where(u => u.OwnerPlayerId == p.PlayerId))
            {
                if (BenefitsFromSpells(unit))
                    spellPayoffs++;
            }

            float synergy = spellsInHand * spellPayoffs * 0.2f;
            _synergyCache["Spell"] = synergy;
        }

        private void CalculateDeathSynergy(PlayerState p, GameState state)
        {
            int deathTriggers = 0;
            int deathPayoffs = 0;

            foreach (var card in p.Hand)
            {
                if (HasTriggerType(card, TriggerType.OnDeath))
                    deathTriggers++;
                if (BenefitsFromDeaths(card))
                    deathPayoffs++;
            }

            foreach (var unit in state.Board.GetAllUnits().Where(u => u.OwnerPlayerId == p.PlayerId))
            {
                if (HasTriggerType(unit, TriggerType.OnDeath))
                    deathTriggers++;
                if (BenefitsFromDeaths(unit))
                    deathPayoffs++;
            }

            float synergy = deathTriggers * deathPayoffs * 0.4f;
            _synergyCache["Death"] = synergy;
        }

        private bool HasEffectType(CardInstance card, ActionType actionType, Keyword? keyword = null)
        {
            foreach (var effect in card.Definition.Effects)
            {
                foreach (var action in effect.Actions)
                {
                    if (action.Type == actionType)
                    {
                        if (keyword == null || action.StatusKeyword == keyword)
                            return true;
                    }
                }
            }
            return false;
        }

        private bool HasTriggerType(CardInstance card, TriggerType triggerType)
        {
            return card.Definition.Effects.Any(e => e.Trigger == triggerType);
        }

        private bool BenefitsFromMarked(CardInstance card)
        {
            foreach (var effect in card.Definition.Effects)
            {
                // Sprawdź warunki
                if (effect.Condition != null && ConditionContainsMarked(effect.Condition))
                    return true;

                // Sprawdź akcje które korzystają z marked
                if (effect.Actions.Any(a => a.Type == ActionType.BonusAttack &&
                    effect.Trigger == TriggerType.OnStatusApplied))
                    return true;
            }
            return false;
        }

        private bool ConditionContainsMarked(ConditionData condition)
        {
            if (condition.Condition == ConditionType.IsStatus && condition.TargetParam == "Marked")
                return true;

            if (condition.SubConditions != null)
            {
                foreach (var sub in condition.SubConditions)
                {
                    if (ConditionContainsMarked(sub))
                        return true;
                }
            }

            return false;
        }

        private bool BenefitsFromSacrifice(CardInstance card)
        {
            return card.Definition.Effects.Any(e =>
                e.Trigger == TriggerType.OnOtherUnitSacrificed ||
                e.Trigger == TriggerType.OnFriendlyUnitDied);
        }

        private bool BenefitsFromMachines(CardInstance card)
        {
            foreach (var effect in card.Definition.Effects)
            {
                if (effect.Condition != null &&
                    effect.Condition.Condition == ConditionType.HasSubtypeOnBoard &&
                    effect.Condition.TargetParam == "Machine")
                    return true;
            }
            return false;
        }

        private bool BenefitsFromSpells(CardInstance card)
        {
            return card.Definition.Effects.Any(e =>
                e.Trigger == TriggerType.OnFriendlyActionPlayed &&
                e.Actions.Any(a => a.Type == ActionType.BuffStats && a.Amount < 0));
        }

        private bool BenefitsFromDeaths(CardInstance card)
        {
            return card.Definition.Effects.Any(e =>
                e.Trigger == TriggerType.OnFriendlyUnitDied);
        }

        private class EvaluationCache
        {
            public int CacheKey { get; set; }
            public float CachedScore { get; set; }
            public int Turn { get; set; }
        }

        private class CachedCardEvaluation
        {
            public int CardId { get; set; }
            public int StatsHash { get; set; }
            public float Value { get; set; }
            public int TurnCached { get; set; }
        }

        #endregion

        #region Survival Evaluation

        private float EvaluateSurvival(PlayerState p, PlayerState o, GameState state,
            float ctrlM, float aggroM, float burnM, float potentialBurst,
            CardInstance[] lineOpponents, List<CardInstance> enemyUnits, CardInstance[] lineAllies)
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
            if (potentialBurst >= oppHp)
            {
                score += 3000f * (_dna.Genes[NewDna.LIFE_LETHAL_VISION] / 5f);
            }
            else if (potentialBurst >= oppHp * 0.6f)
            {
                score += (potentialBurst / oppHp) * _dna.Genes[NewDna.LIFE_LETHAL_VISION] * 80f * aggroM;
            }

            // 5. OPPONENT HP WEIGHT
            float totalAggression = Math.Max(aggroM, burnM);
            float aggressionMultiplier = 1.0f + (totalAggression * 4.0f);
            score -= oppHp * _dna.Genes[NewDna.LIFE_OPPONENT_HP_WEIGHT] * aggressionMultiplier * 15f;

            // 6. VULNERABILITY ANALYSIS - ZOPTYMALIZOWANE
            float vulnerability = _dna.Genes[NewDna.LIFE_VULNERABILITY_SENSE] / 10f;
            float aggression = _dna.Genes[NewDna.BOARD_PLACEMENT_REACTION_TYPE] / 10f;
            float netHoleAttitude = aggression - vulnerability;

            for (int i = 0; i < 4; i++)
            {
                var enU = lineOpponents[i];
                var myU = lineAllies[i];

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
            foreach (var u in enemyUnits)
            {
                if (CanDealDirectHeroDamage(u))
                {
                    score -= 50f * _dna.Genes[NewDna.LIFE_INDIRECT_DAMAGE_FOCUS] * (1f + reactiveBias);
                }
            }

            // 8. HEAL PRIORITY
            if (selfHp < 15f)
            {
                float healInHand = CalculateHealInHand(p);
                score += healInHand * _dna.Genes[NewDna.LIFE_HEAL_PRIORITY_HERO] * 25f;
            }

            return score;
        }

        private float CalculatePotentialBurst(PlayerState p, CardInstance[] lineAllies,
            CardInstance[] lineOpponents, Dictionary<int, int> unitLanes)
        {
            float burst = 0f;

            // Board damage - użyjemy lineAllies zamiast przeszukiwania
            for (int i = 0; i < 4; i++)
            {
                var myU = lineAllies[i];
                var enU = lineOpponents[i];

                if (myU != null && CanUnitHitFace(myU, enU))
                {
                    burst += myU.CurrentStats.Attack;
                }
            }

            // Hand damage
            foreach (var card in p.Hand)
            {
                if (card.CurrentStats.BloodCost <= p.CurrentBlood)
                {
                    burst += CalculateDirectDamageFromCard(card);
                }
            }

            return burst;
        }

        private float CalculateHealInHand(PlayerState p)
        {
            float heal = 0f;
            foreach (var card in p.Hand)
            {
                foreach (var effect in card.Definition.Effects)
                {
                    foreach (var action in effect.Actions)
                    {
                        if (action.Type == ActionType.Heal)
                            heal += action.Amount;
                    }
                }
            }
            return heal;
        }

        private float CalculateDirectDamageFromCard(CardInstance card)
        {
            float damage = 0f;
            foreach (var effect in card.Definition.Effects)
            {
                foreach (var action in effect.Actions)
                {
                    if (action.Type == ActionType.DealDamage)
                        damage += action.Amount;
                }
            }
            return damage;
        }

        #endregion

        #region Resources Evaluation

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

            // 3. DECK THINNING FOCUS
            if (p.DrawPile.Count < 15)
                score += (15 - p.DrawPile.Count) * _dna.Genes[NewDna.LOGIC_DECK_THINNING_FOCUS] * 8f;

            // 4. DISCARD PILE RECYCLE
            int usefulInDiscard = CalculateUsefulCardsInDiscard(p);
            score += usefulInDiscard * _dna.Genes[NewDna.LOGIC_DISCARD_PILE_RECYCLE] * 10f;

            // 5. EVALUATE EACH CARD IN HAND z cache
            float spellPref = (ctrlM + burnM) / 2f;
            float unitPref = (aggroM + midM) / 2f;
            float faceBias = _dna.Genes[NewDna.TARGETING_FACE_VS_BOARD_BIAS] / 10f;

            // Rebuild synergy cache jeśli potrzebne
            RebuildSynergyCache(p, state);

            foreach (var card in hand)
            {
                float cardVal = EvaluateCardValue(card, p, o, state, spellPref, unitPref, faceBias);
                score += cardVal;
            }

            // 6. MANA CURVE OPTIMIZATION
            if (handCount > 0)
            {
                float totalCost = 0f;
                foreach (var card in hand) totalCost += card.CurrentStats.BloodCost;
                float avgCost = totalCost / handCount;

                float curveGoal = 11f - _dna.Genes[NewDna.LOGIC_MANA_CURVE_BIAS];
                float efficiencyMod = 1f + (_dna.Genes[NewDna.COMBAT_TRADING_EFFICIENCY] / 20f);
                score -= Math.Abs(avgCost - curveGoal) * 30f * futureM * efficiencyMod;
            }

            // 7. DRAW URGENCY
            if (handCount <= 2)
                score -= (3 - handCount) * _dna.Genes[NewDna.LOGIC_DRAW_URGENCY] * 60f;

            return score;
        }

        private int CalculateUsefulCardsInDiscard(PlayerState p)
        {
            int count = 0;
            foreach (var card in p.DiscardPile)
            {
                // Karty z efektami śmierci lub powrotu do ręki
                foreach (var effect in card.Definition.Effects)
                {
                    if (effect.Trigger == TriggerType.OnDeath ||
                        effect.Actions.Any(a => a.Type == ActionType.ReturnToHand ||
                                              a.Type == ActionType.SummonUnit))
                    {
                        count++;
                        break;
                    }
                }
            }
            return count;
        }

        private float EvaluateCardValue(CardInstance card, PlayerState p, PlayerState o, GameState state,
    float spellPref, float unitPref, float faceBias)
        {
            
            // OPTYMALIZACJA: Cache wartości kart
            int cardHash = ComputeCardHash(card, state.TurnNumber, p.Health, o.Health);
            if (_cardEvaluationCache.TryGetValue(card.InstanceId, out var cached) &&
                cached.StatsHash == cardHash && cached.TurnCached == state.TurnNumber)
            {
                return cached.Value;
            }

            float cardVal = (card.Definition.Type == CardType.Unit) ? unitPref * 20f : spellPref * 20f;
            if (card.CurrentStats.BloodCost > 4)
            {
                cardVal -= (card.CurrentStats.BloodCost - 4) * 15f;
            }

            // BASE STATS
            var stats = card.CurrentStats;
            cardVal += stats.Attack * 8f + stats.Health * 5f;

            // FIX 1: SILNA KARA ZA WYSOKI KOSZT (kwadratowa)
            float costPenalty = 0f;
            if (stats.BloodCost >= 5)
            {
                float aggroLens = (_dna.Genes[NewDna.STYLE_AGGRO] / 5f) + 0.1f;
                costPenalty = (stats.BloodCost - 4) * 8f * aggroLens;
            }
            cardVal -= costPenalty * (11f - _dna.Genes[NewDna.LOGIC_PATIENCE_FACTOR]) / 10f;

            // FIX 2: BONUSY I KARY ZA SYNERGIE
            float synergyBonus = CalculateCardSynergyBonus(card, p, state);
            cardVal += synergyBonus;

            // FIX 3: POPRAWA WYCENY SKOMPLIKOWANYCH KART
            float complexityBonus = EvaluateCardComplexity(card, p, o, state);
            cardVal += complexityBonus;

           

            // COST EFFICIENCY (po karze)
            float costEfficiency = (stats.Attack + stats.Health) / Math.Max(1f, stats.BloodCost);
            cardVal += costEfficiency * 12f;

            // CHOICE CARDS
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

            // ON-PLAYED EFFECTS z lepszym szacowaniem warunków
            float onPlayedValue = 0f;
            foreach (var effect in card.Definition.Effects)
            {
                if (effect.Trigger == TriggerType.OnPlayed)
                {
                    float conditionProb = EstimateConditionLikelihood(effect.Condition, card, p, o, state);
                    float actionsSum = 0f;
                    foreach (var action in effect.Actions)
                    {
                        actionsSum += EvaluateActionValue(action, card, p, o, state);
                    }
                    onPlayedValue += conditionProb * actionsSum;
                }
            }
            cardVal += onPlayedValue * _dna.Genes[NewDna.TRIGGER_PLAY_STRATEGY] * 0.2f;

            // GIVING RESOURCES PENALTY
            if (card.Definition.Effects.Any(e => e.Actions.Any(a =>
                a.Type == ActionType.GiveToOpponent ||
                (a.Type == ActionType.DrawCard && a.Target == TargetType.EnemyHero))))
            {
                cardVal -= _dna.Genes[NewDna.LOGIC_GIVING_RESOURCES_PENALTY] * 100f;
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
            foreach (var effect in card.Definition.Effects)
            {
                if ((effect.Zone == EffectZone.Hand || effect.Zone == EffectZone.Any) &&
                    effect.Actions.Any(a => a.Type == ActionType.BuffStats && a.Amount < 0))
                {
                    float fuel = 0f;
                    if (effect.Trigger == TriggerType.OnFriendlyActionPlayed)
                    {
                        int spellCount = p.Hand.Count(c => c.Definition.Type == CardType.Spell);
                        fuel = spellCount * _dna.Genes[NewDna.TRIGGER_SETUP_RECOGNITION];
                    }
                    else if (effect.Trigger == TriggerType.OnOtherUnitSacrificed)
                    {
                        int unitsOnBoard = state.Board.GetAllUnits().Count(u => u.OwnerPlayerId == p.PlayerId);
                        int sacrificeActivators = p.Hand.Count(c => IsSacrificeActivator(c));

                        fuel = Math.Min(unitsOnBoard, sacrificeActivators) * _dna.Genes[NewDna.TRIGGER_SACRIFICE_VISION] * 1.5f;
                        if (sacrificeActivators == 0 && unitsOnBoard > 0) fuel *= 0.3f;
                    }

                    int discount = card.Definition.BaseStats.BloodCost - stats.BloodCost;
                    float reductionValue = (fuel * 18f) + (discount * _dna.Genes[NewDna.LOGIC_STRATEGY_COST_REDUCTION] * 35f);

                    if (stats.BloodCost == 0) reductionValue += 250f;
                    else if (stats.BloodCost == 1) reductionValue += 100f;
                    else if (stats.BloodCost == 2) reductionValue += 40f;

                    cardVal += reductionValue;
                }
            }

            // CONDITIONAL EFFECTS - lepsze szacowanie
            foreach (var effect in card.Definition.Effects)
            {
                if (effect.Condition != null)
                {
                    float conditionLikelihood = EstimateConditionLikelihood(effect.Condition, card, p, o, state);
                    // Mniej agresywna redukcja dla warunkowych kart
                    cardVal *= (0.7f + 0.3f * conditionLikelihood);
                }
            }

            // FIX 4: KARA ZA ZBYT DROGIE BOARD CLEARS
            if (stats.BloodCost > 5)
            {
                bool isBoardClear = card.Definition.Effects.Any(e => e.Actions.Any(a =>
                    a.Type == ActionType.DestroyUnit &&
                    (a.Target == TargetType.AllEnemyUnits || a.Target == TargetType.AllUnitsOnBoard)));

                if (isBoardClear)
                {
                    // Mniejsza kara niż wcześniej, ale wciąż znacząca
                    cardVal *= 0.7f;
                }
            }

            // FIX 5: DYNAMICZNY BONUS ZA NIEDOCENIANE KARTY Z WYSOKIM WINRATE
            float underusedBonus = CalculateUnderusedCardBonus(card, p);
            cardVal += underusedBonus;


            // NOWA FIX: KARA ZA NADUŻYWANE KARTY Z NISKIM WINRATE
            float overusedPenalty = CalculateOverusedCardPenalty(card, p);
            cardVal -= overusedPenalty;

            

            // Zapisz do cache
            _cardEvaluationCache[card.InstanceId] = new CachedCardEvaluation
            {
                CardId = card.InstanceId,
                StatsHash = cardHash,
                Value = cardVal,
                TurnCached = state.TurnNumber
            };
            float efficiencyMultiplier = 10f / (card.CurrentStats.BloodCost + 2f);
            cardVal *= efficiencyMultiplier;
            return cardVal;
        }

        private int ComputeCardHash(CardInstance card, int turn, float myHp, float enemyHp)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + card.Definition.Id.GetHashCode();
                hash = hash * 23 + card.CurrentStats.BloodCost.GetHashCode();
                hash = hash * 23 + card.CurrentStats.Attack.GetHashCode();
                hash = hash * 23 + card.CurrentStats.Health.GetHashCode();
                hash = hash * 23 + turn.GetHashCode();
                hash = hash * 23 + ((int)myHp).GetHashCode();
                hash = hash * 23 + ((int)enemyHp).GetHashCode();
                return hash;
            }
        }

        private float CalculateCardSynergyBonus(CardInstance card, PlayerState p, GameState state)
        {
            float bonus = 0f;

            // Synergia z markingiem
            if (BenefitsFromMarked(card) && _synergyCache.TryGetValue("Marking", out float markingSynergy))
            {
                bonus += markingSynergy * 0.1f;
            }

            // Synergia z sacrifice
            if (BenefitsFromSacrifice(card) && _synergyCache.TryGetValue("Sacrifice", out float sacrificeSynergy))
            {
                bonus += sacrificeSynergy * 0.15f;
            }

            // Synergia z machine
            if (BenefitsFromMachines(card) && _synergyCache.TryGetValue("Machine", out float machineSynergy))
            {
                bonus += machineSynergy * 0.2f;
            }

            // Synergia ze spellami
            if (BenefitsFromSpells(card) && _synergyCache.TryGetValue("Spell", out float spellSynergy))
            {
                bonus += spellSynergy * 0.1f;
            }

            // Synergia ze śmiercią jednostek
            if (BenefitsFromDeaths(card) && _synergyCache.TryGetValue("Death", out float deathSynergy))
            {
                bonus += deathSynergy * 0.12f;
            }

            return bonus;
        }

        private float EvaluateCardComplexity(CardInstance card, PlayerState p, PlayerState o, GameState state)
        {
            float bonus = 0f;

            // Bonus za karty z wieloma efektami
            int effectCount = card.Definition.Effects.Count;
            if (effectCount > 1)
            {
                bonus += (effectCount - 1) * 5f * _dna.Genes[NewDna.TRIGGER_SETUP_RECOGNITION];
            }

            // Bonus za karty z warunkami
            bool hasConditions = card.Definition.Effects.Any(e => e.Condition != null);
            if (hasConditions)
            {
                bonus += 10f * _dna.Genes[NewDna.LOGIC_PROBABILITY_COGNITION];
            }

            // Bonus za karty z efektami w różnych strefach
            bool hasHandEffects = card.Definition.Effects.Any(e => e.Zone == EffectZone.Hand);
            bool hasBoardEffects = card.Definition.Effects.Any(e => e.Zone == EffectZone.Board);
            if (hasHandEffects && hasBoardEffects)
            {
                bonus += 15f * _dna.Genes[NewDna.TRIGGER_SACRIFICE_VISION];
            }

            // Bonus za karty z unikalnymi mechanikami
            if (HasUniqueMechanics(card))
            {
                bonus += 20f * _dna.Genes[NewDna.LOGIC_STRATEGY_COST_REDUCTION];
            }

            return bonus;
        }

        private bool HasUniqueMechanics(CardInstance card)
        {
            // Sprawdź czy karta ma unikalne mechaniki
            foreach (var effect in card.Definition.Effects)
            {
                foreach (var action in effect.Actions)
                {
                    if (action.Type == ActionType.AbsorbStats ||
                        action.Type == ActionType.MoveRight ||
                        action.Type == ActionType.MoveLeft ||
                        action.Type == ActionType.HealToFull ||
                        action.Type == ActionType.ModifyGlobalBuff)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private void LoadCardStatsFromFile()
        {
            try
            {
                // Sprawdź czy już załadowaliśmy
                if (_cardStats.Any())
                {
                    Console.WriteLine($"[CardStats] Already loaded {_cardStats.Count} cards");
                    return;
                }

                string statsDir = "EvolutionData/CardStats";
                if (!Directory.Exists(statsDir))
                {
                    Console.WriteLine($"[CardStats] CRITICAL: Directory not found: {statsDir}");
                    // Spróbuj stworzyć
                    Directory.CreateDirectory(statsDir);
                    Console.WriteLine($"[CardStats] Created directory: {statsDir}");
                    return;
                }

                var files = Directory.GetFiles(statsDir, "card_stats_gen*.csv");
                if (files.Length == 0)
                {
                    Console.WriteLine($"[CardStats] WARNING: No stats files found in {statsDir}");
                    Console.WriteLine($"[CardStats] Looking for ANY CSV files...");
                    files = Directory.GetFiles(statsDir, "*.csv");
                }

                if (files.Length == 0)
                {
                    Console.WriteLine($"[CardStats] ERROR: No CSV files found at all!");
                    return;
                }

                var latestStatsFile = files.OrderByDescending(f => f).First();
                Console.WriteLine($"[CardStats] Loading card stats from: {latestStatsFile}");
                Console.WriteLine($"[CardStats] File size: {new FileInfo(latestStatsFile).Length} bytes");

                var lines = File.ReadAllLines(latestStatsFile);
                Console.WriteLine($"[CardStats] Read {lines.Length} lines");

                if (lines.Length < 2)
                {
                    Console.WriteLine($"[CardStats] ERROR: File has less than 2 lines (no data)");
                    return;
                }

                int loadedCount = 0;
                int errorCount = 0;

                // Pomijamy nagłówek (pierwsza linia)
                for (int i = 1; i < lines.Length; i++)
                {
                    try
                    {
                        var line = lines[i];
                        if (string.IsNullOrWhiteSpace(line))
                            continue;

                        var parts = line.Split(';');
                        if (parts.Length < 9)
                        {
                            Console.WriteLine($"[CardStats] WARNING: Line {i} has only {parts.Length} parts");
                            continue;
                        }

                        int cardId = int.Parse(parts[0]);
                        string cardName = parts[1].Trim('"');
                        float useRate = float.Parse(parts[2], CultureInfo.InvariantCulture) / 100f;
                        int deckCount = int.Parse(parts[3]);
                        int totalDecks = int.Parse(parts[4]);
                        float avgCopies = float.Parse(parts[5], CultureInfo.InvariantCulture);
                        float winRate = float.Parse(parts[6], CultureInfo.InvariantCulture) / 100f;
                        int games = int.Parse(parts[7]);
                        int wins = int.Parse(parts[8]);
                        int totalCopies = parts.Length > 9 ? int.Parse(parts[9]) : 0;

                        // Create the stats object using constructor and object initializer
                        var stat = new CardStatsEnhanced(cardId, cardName)
                        {
                            UseRate = useRate,
                            DeckCount = deckCount,
                            TotalDecks = totalDecks,
                            AverageDensity = avgCopies,
                            TotalCopiesInPopulation = totalCopies
                            // Note: We CANNOT set GamesWithCard or WinsWithCard directly (read-only)
                            // Note: We CANNOT set WinRate directly (private setter)
                        };

                        // Now set the games/wins using the special method
                        stat.SetInitialGamesAndWins(games, wins);

                        _cardStats[cardId] = stat;
                        loadedCount++;

                        // Logowanie wszystkich kart z wysokim winrate
                        if (winRate > 0.60f)
                        {
                            Console.WriteLine($"[CardStats] HIGH WINRATE: {cardName} (ID:{cardId}) - {winRate:P1} win, {useRate:P1} use, {games} games");
                        }
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        if (errorCount < 5) // Loguj tylko pierwsze 5 błędów
                        {
                            Console.WriteLine($"[CardStats] ERROR parsing line {i}: {ex.Message}");
                        }
                    }
                }

                Console.WriteLine($"[CardStats] SUCCESS: Loaded {loadedCount} cards, {errorCount} errors");

                if (loadedCount == 0)
                {
                    Console.WriteLine($"[CardStats] CRITICAL: No cards loaded! Check CSV format.");
                    // Wypisz pierwszą linię dla debugu
                    if (lines.Length > 1)
                    {
                        Console.WriteLine($"[CardStats] Sample line: {lines[1]}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CardStats] FATAL ERROR: {ex.Message}");
                Console.WriteLine($"[CardStats] Stack trace: {ex.StackTrace}");
            }
        }

        private float CalculateUnderusedCardBonus(CardInstance card, PlayerState p)
        {
            // Jeśli nie ma statystyk dla tej karty, zwróć 0
            if (!int.TryParse(card.Definition.Id, out int cardId))
                return 0f;

            if (!_cardStats.ContainsKey(cardId))
                return 0f;

            var stat = _cardStats[cardId];

            // REMOVED: Minimum games threshold
            // if (stat.GamesWithCard < 5) return 0f; // REMOVED THIS LINE

            float winRate = stat.WinRate;
            float useRate = stat.UseRate;

            // 1. EKSTREMALNY BONUS za karty z winrate >80% i użyciem <5%
            if (winRate > 0.80f && useRate < 0.05f)
            {
                float winRateBonus = (winRate - 0.5f) * 320f;  // REDUCED 60%: from 800f to 320f
                float underusedBonus = (0.05f - useRate) * 400f;  // REDUCED 60%: from 1000f to 400f

                // REDUCED BONUS MULTIPLIER: from 2.0f to 0.8f
                return (winRateBonus + underusedBonus) * 0.8f;
            }

            // 2. Bardzo duży bonus za winrate >70% i użycie <10%
            if (winRate > 0.70f && useRate < 0.10f)
            {
                float winRateBonus = (winRate - 0.5f) * 240f;  // REDUCED 60%: from 600f to 240f
                float underusedBonus = (0.10f - useRate) * 320f;  // REDUCED 60%: from 800f to 320f

                // REDUCED BONUS MULTIPLIER: from 1.0f to 0.6f
                return (winRateBonus + underusedBonus) * 0.6f;
            }

            // 3. Duży bonus za winrate >60% i użycie <20%
            if (winRate > 0.60f && useRate < 0.20f)
            {
                float winRateBonus = (winRate - 0.5f) * 160f;  // REDUCED 60%: from 400f to 160f
                float underusedBonus = (0.20f - useRate) * 240f;  // REDUCED 60%: from 600f to 240f

                // REDUCED BONUS MULTIPLIER: from 1.0f to 0.5f
                return (winRateBonus + underusedBonus) * 0.5f;
            }

            // 4. Moderate bonus for decent winrate >55% and usage <30%
            if (winRate > 0.55f && useRate < 0.30f)
            {
                float winRateBonus = (winRate - 0.5f) * 80f;  // Smaller bonus
                float underusedBonus = (0.30f - useRate) * 120f;

                return (winRateBonus + underusedBonus) * 0.3f;
            }

            // 5. SMALL bonus for any card with winrate >52% and usage <15%
            if (winRate > 0.52f && useRate < 0.15f)
            {
                return 50f * (winRate - 0.5f) * (0.15f - useRate);
            }

            return 0f;
        }
        private float CalculateOverusedCardPenalty(CardInstance card, PlayerState p)
        {
            if (!int.TryParse(card.Definition.Id, out int cardId))
                return 0f;

            if (!_cardStats.ContainsKey(cardId))
                return 0f;

            var stat = _cardStats[cardId];

            float winRate = stat.WinRate;
            float useRate = stat.UseRate;

            // 1. MEGA KARA za karty z użyciem >90% i winrate <50%
            if (useRate > 0.90f && winRate < 0.50f)
            {
                float overusePenalty = (useRate - 0.9f) * 200f;  // REDUCED 60%: from 500f to 200f
                float badWinratePenalty = (0.50f - winRate) * 320f;  // REDUCED 60%: from 800f to 320f

                // REDUCED PENALTY MULTIPLIER: from 2.0f to 0.6f
                return (overusePenalty + badWinratePenalty) * 0.6f;
            }

            // 2. Duża kara za użycie >80% i winrate <52%
            if (useRate > 0.80f && winRate < 0.52f)
            {
                float overusePenalty = (useRate - 0.8f) * 160f;  // REDUCED 60%: from 400f to 160f
                float badWinratePenalty = (0.52f - winRate) * 240f;  // REDUCED 60%: from 600f to 240f

                // REDUCED PENALTY: from 1.0f multiplier to 0.4f
                return (overusePenalty + badWinratePenalty) * 0.4f;
            }

            // 3. Kara za użycie >70% i winrate <48%
            if (useRate > 0.70f && winRate < 0.48f)
            {
                // REDUCED PENALTY: from 500f to 150f
                return (0.48f - winRate) * 150f;
            }

            // 4. Mild penalty for overused cards with mediocre winrate
            if (useRate > 0.60f && winRate < 0.50f)
            {
                return (0.50f - winRate) * 50f;
            }

            return 0f;
        }

        private float EvaluateChoiceCard(CardInstance card, PlayerState p, PlayerState o, GameState state)
        {
            float bestOptionValue = 0f;

            foreach (var effect in card.Definition.Effects)
            {
                if (effect.Targeting == TargetType.Choice)
                {
                    foreach (var action in effect.Actions)
                    {
                        float optionValue = EvaluateActionValue(action, card, p, o, state);
                        if (optionValue > bestOptionValue) bestOptionValue = optionValue;
                    }
                }
            }

            return bestOptionValue;
        }

        #endregion

        #region Action Evaluation

        private float EvaluateActionValue(ActionData action, CardInstance sourceCard,
            PlayerState p, PlayerState o, GameState state)
        {
            float value = 0f;

            float comboLens = (_dna.Genes[NewDna.STYLE_COMBO] / 5f) + 0.1f;
            float ctrlLens = (_dna.Genes[NewDna.STYLE_CONTROL] / 5f) + 0.1f;

            switch (action.Type)
            {
                case ActionType.DealDamage:
                    value = EvaluateDamageAction(action, sourceCard, p, o, state);
                    break;

                case ActionType.DrawCard:
                    value = action.Amount * 25f * _dna.Genes[NewDna.LOGIC_DRAW_URGENCY];
                    break;

                case ActionType.DrawFromDiscard:
                    int usefulInDiscard = CalculateUsefulCardsInDiscard(p);
                    value = Math.Min(action.Amount, usefulInDiscard) * 30f *
                            _dna.Genes[NewDna.LOGIC_DISCARD_PILE_RECYCLE];
                    break;

                case ActionType.Heal:
                    value = action.Amount * 20f * _dna.Genes[NewDna.LIFE_HEAL_PRIORITY_HERO];
                    if (p.Health < 15) value *= 1.5f;
                    break;

                case ActionType.BuffStats:
                    if (action.Target == TargetType.FriendlySpellsInHand || action.Target == TargetType.EnemySpellsInHand)
                    {
                        value = EvaluateCostModificationEffect(action, p, o, comboLens, ctrlLens);
                    }
                    else
                    {
                        value = EvaluateBuffAction(action, p, o, state);
                    }
                    break;

                case ActionType.SummonUnit:
                case ActionType.MakeAUnit:
                    value = 25f + EvaluateSummonedUnitValue(action, p, state);
                    break;

                case ActionType.AddCardToHand:
                    value = 20f + EvaluateAddedCardValue(action, p);
                    break;

                case ActionType.ApplyStatus:
                    value = EvaluateStatusValue(action.StatusKeyword ?? Keyword.None, sourceCard, p, o, state);
                    break;

                case ActionType.Silence:
                    value = EvaluateSilenceValue(action, p, o, state);
                    break;

                case ActionType.TutorCard:
                    value = 30f * _dna.Genes[NewDna.LOGIC_TUTOR_PRECISION];
                    break;

                case ActionType.ReturnToHand:
                    value = EvaluateBounceValue(action, p, o, state);
                    break;

                case ActionType.BonusAttack:
                    value = EvaluateBonusAttackValue(action, sourceCard, p, o, state);
                    break;

                case ActionType.SacrificeUnit:
                case ActionType.DestroyUnit:
                    value = EvaluateSacrificeAction(action, sourceCard, p, o, state);
                    break;

                case ActionType.AbsorbStats:
                    value = 40f; // Silny efekt
                    break;

                case ActionType.MoveRight:
                case ActionType.MoveLeft:
                    value = EvaluateMovementValue(action, sourceCard, p, state);
                    break;

                case ActionType.HealToFull:
                    value = 35f; // Dobry efekt
                    break;

                case ActionType.ModifyGlobalBuff:
                    value = 50f; // Bardzo silny efekt
                    break;
            }

            return value;
        }

        private float EvaluateCostModificationEffect(ActionData action, PlayerState p, PlayerState o, float comboLens, float ctrlLens)
        {
            float value = 0f;
            // Pamiętaj: Amount -1 to zniżka, Amount 6 to podatek (według Twojego AuraSystem)

            if (action.Target == TargetType.FriendlySpellsInHand)
            {
                // ZNIŻKA DLA MNIE (np. Tea Maid)
                // Cenimy to bardziej, jeśli mamy dużo czarów w ręce

                int mySpells = p.Hand.Count(c => c.Definition.Type == CardType.Spell);
                float reductionValue = -action.Amount; // zamieniamy -1 na +1 zniżki

             
                // Bonus za "umożliwienie" combosów
                if (reductionValue > 0)
                {
                    float archetypeInterest = Math.Max(comboLens, ctrlLens);

                    // 60 pkt za każdy czar (wcześniej 50) + mnożnik archetypu
                    value = mySpells * 60f * (1f + archetypeInterest);

                    // Stały bonus za "potencjał" (nawet jeśli teraz nie mamy czarów, chcemy Maid na stole)
                    value += 100f * archetypeInterest;
                }
            }
            else if (action.Target == TargetType.EnemySpellsInHand)
            {
                // PODATEK DLA PRZECIWNIKA (np. Anti Matter Watermonster)
                // Cenimy to, bo blokuje tempo wroga (Control Style)
                int estimatedEnemySpells = o.Hand.Count / 2; // Szacujemy, że połowa to czary
                float taxValue = action.Amount; // Amount 6 to podatek

                // Im więcej kart ma wróg, tym groźniejszy jest podatek
                value = taxValue * estimatedEnemySpells * 50f * (1f + ctrlLens);

                // Jeśli podatek jest ogromny (jak u Watermonstera), dajemy extra bonus za "Lockdown"
                if (taxValue >= 3) value += 200f * ctrlLens;
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
                    if (o.Health <= action.Amount)
                        value += 1000f;
                    break;

                case TargetType.TargetEnemyUnit:
                    float bestTargetValue = FindBestDamageTargetValue(action.Amount, p, state);
                    value = bestTargetValue;
                    break;

                case TargetType.AllEnemyUnits:
                case TargetType.AllUnitsOnBoard:
                    value = EvaluateAoEDamageValue(action, sourceCard, p, state);
                    break;

                case TargetType.SelectedTarget:
                    // Średnia wartość między jednostką a bohaterem
                    float unitValue = FindBestDamageTargetValue(action.Amount, p, state);
                    float heroValue = action.Amount * 15f * _dna.Genes[NewDna.TARGETING_FACE_VS_BOARD_BIAS];
                    value = Math.Max(unitValue, heroValue);
                    break;
            }

            return value;
        }

        private float FindBestDamageTargetValue(float damageAmount, PlayerState p, GameState state)
        {
            float bestValue = 0f;

            foreach (var enemyUnit in state.Board.GetAllUnits().Where(u => u.OwnerPlayerId != p.PlayerId))
            {
                float killBonus = enemyUnit.CurrentStats.Health <= damageAmount ? 50f : 10f;
                float threatValue = enemyUnit.CurrentStats.Attack * 8f +
                                  enemyUnit.CurrentStats.Health * 3f;

                // Bonus za zabicie jednostek z efektami
                if (enemyUnit.CurrentStats.Health <= damageAmount && HasValuableEffects(enemyUnit))
                    killBonus += 30f;

                float unitValue = killBonus + threatValue;
                if (unitValue > bestValue) bestValue = unitValue;
            }

            return bestValue;
        }

        private bool HasValuableEffects(CardInstance unit)
        {
            // Sprawdź czy jednostka ma cenne efekty
            return unit.Definition.Effects.Any(e =>
                e.Trigger == TriggerType.Passive ||
                e.Trigger == TriggerType.OnPreCombatLine ||
                e.Trigger == TriggerType.OnDeath && e.Actions.Any(a =>
                    a.Type == ActionType.DealDamage ||
                    a.Type == ActionType.DrawCard ||
                    a.Type == ActionType.SummonUnit));
        }

        private float EvaluateAoEDamageValue(ActionData action, CardInstance sourceCard, PlayerState p, GameState state)
        {
            float potentialKills = 0F;
            float totalThreat = 0f;

            foreach (var u in state.Board.GetAllUnits().Where(u => u.OwnerPlayerId != p.PlayerId))
            {
                if (u.CurrentStats.Health <= action.Amount)
                {
                    potentialKills++;
                    if (HasValuableEffects(u)) potentialKills += 0.5f; // Bonus za cenne jednostki
                }
                totalThreat += u.CurrentStats.Attack * 3f + u.CurrentStats.Health * 1f;
            }

            // Uwzględnij koszt karty
            float costFactor = Math.Max(1f, 8f - sourceCard.CurrentStats.BloodCost) / 8f;
            float value = potentialKills * 40f * costFactor * _dna.Genes[NewDna.TARGETING_AOE_SYMMETRY_TOLERANCE];

            // Dodaj wartość redukcji zagrożenia
            value += totalThreat * 0.3f;

            return value;
        }

        private float EvaluateBuffAction(ActionData action, PlayerState p, PlayerState o, GameState state)
        {
            float value = 0f;

            if (action.BuffAtk < 0 || action.BuffHp < 0)
            {
                // Debuff - bardziej wartościowy
                float debuffValue = (Math.Abs(action.BuffAtk) * 12f + Math.Abs(action.BuffHp) * 8f) * 1.5f;

                // Bonus za potencjalne zabicie jednostki
                if (action.Target == TargetType.TargetEnemyUnit ||
                    action.Target == TargetType.SelectedTarget)
                {
                    debuffValue *= 1.3f;
                }
                value = debuffValue;
            }
            else
            {
                // Buff
                value = (action.BuffAtk * 8f + action.BuffHp * 5f);

                // Bonus za buffowanie jednostek z cennymi efektami
                if (action.Target == TargetType.TargetFriendlyUnit ||
                    action.Target == TargetType.SelectedTarget)
                {
                    value *= 1.2f;
                }
            }

            return value;
        }

        private float EvaluateSummonedUnitValue(ActionData action, PlayerState p, GameState state)
        {
            // Sprawdź jaka jednostka jest przywoływana
            if (action.ValueParam != 0)
            {
                // ZAMIAST: action.ValueParam.Value
                if (action.ValueParam >= 900) return 15f;
            }

            return 25f; // Domyślna wartość przywołania
        }

        private float EvaluateAddedCardValue(ActionData action, PlayerState p)
        {
            // Sprawdź jaką kartę dodajemy
            if (action.ValueParam != 0)
            {
                // ZAMIAST: action.ValueParam.Value
                if (action.ValueParam >= 900) return 10f;
            }

            return 20f; // Domyślna wartość dodania karty
        }

        private float EvaluateStatusValue(Keyword status, CardInstance sourceCard,
            PlayerState p, PlayerState o, GameState state)
        {
            switch (status)
            {
                case Keyword.Marked:
                    // Marking jest teraz bardziej wartościowy dzięki synergiom
                    return 30f * _dna.Genes[NewDna.KEYWORD_MARKED_FOCUS];

                case Keyword.Stunned:
                    // Stun jest bardzo silny - zwiększona wartość
                    return 60f * (11f - _dna.Genes[NewDna.KEYWORD_STUN_DENIAL]);

                case Keyword.Burning:
                    return 25f; // Zwiększona z 20f

                case Keyword.SoulGuard:
                    return 35f * _dna.Genes[NewDna.KEYWORD_SOULGUARD_STICKINESS];

                case Keyword.Unkillable:
                    return 60f * _dna.Genes[NewDna.KEYWORD_UNKILLABLE_RECURSION];

                case Keyword.Flying:
                    return 25f * _dna.Genes[NewDna.KEYWORD_FLYING_VALUE];

                case Keyword.Armored:
                    return 20f; // Nowy keyword

                case Keyword.SplashDamage:
                    return 15f; // Nowy keyword

                default:
                    return 20f; // Zwiększona z 15f
            }
        }

        private float EvaluateSilenceValue(ActionData action, PlayerState p, PlayerState o, GameState state)
        {
            float value = 40f * _dna.Genes[NewDna.TARGETING_STATUS_PHILOSOPHY];

            // Bonus jeśli są jednostki do wyciszenia
            int valuableEnemies = 0;
            foreach (var unit in state.Board.GetAllUnits().Where(u => u.OwnerPlayerId != p.PlayerId))
            {
                if (HasValuableEffects(unit) || unit.CurrentStats.Keywords.Any())
                    valuableEnemies++;
            }

            value += valuableEnemies * 15f;
            return value;
        }

        private float EvaluateBounceValue(ActionData action, PlayerState p, PlayerState o, GameState state)
        {
            float value = 15f; // Bazowa wartość

            // Bonus za zwrócenie silnej jednostki przeciwnika
            if (action.Target == TargetType.SelectedTarget ||
                action.Target == TargetType.TargetEnemyUnit)
            {
                float bestTargetValue = 0f;
                foreach (var unit in state.Board.GetAllUnits().Where(u => u.OwnerPlayerId != p.PlayerId))
                {
                    float unitValue = unit.CurrentStats.Attack * 10f + unit.CurrentStats.Health * 6f;
                    if (HasValuableEffects(unit)) unitValue += 30f;
                    if (unitValue > bestTargetValue) bestTargetValue = unitValue;
                }

                value += bestTargetValue * 0.3f;
            }

            return value;
        }

        private float EvaluateBonusAttackValue(ActionData action, CardInstance sourceCard, PlayerState p, PlayerState o, GameState state)
        {
            float value = 35f * _dna.Genes[NewDna.KEYWORD_BONUS_ATTACK_VALUE];

            // Bonus jeśli jednostka może zaatakować bezpośrednio bohatera
            if (action.Target == TargetType.Self && sourceCard != null)
            {
                int lane = GetLaneOfUnit(state, sourceCard.InstanceId);
                var enemyInLane = GetOpponentInLane(state, lane, p.PlayerId);

                if (CanUnitHitFace(sourceCard, enemyInLane))
                    value *= 1.5f;
            }

            return value;
        }

        private float EvaluateSacrificeAction(ActionData action, CardInstance sourceCard,
            PlayerState p, PlayerState o, GameState state)
        {
            float value = 0f;

            // Sprawdź czy mamy dobre cele
            int goodTargets = 0;
            float totalSacrificeValue = 0f;

            foreach (var u in state.Board.GetAllUnits().Where(u => u.OwnerPlayerId == p.PlayerId))
            {
                if (IsGoodSacrificeTarget(u))
                {
                    goodTargets++;
                    totalSacrificeValue += CalculateSacrificeTargetValue(u);
                }
            }

            // Sprawdź czy mamy payoff
            bool hasPayoff = p.Hand.Any(c => BenefitsFromSacrifice(c)) ||
                            state.Board.GetAllUnits().Any(u =>
                                u.OwnerPlayerId == p.PlayerId && BenefitsFromSacrifice(u));

            if (goodTargets > 0 || hasPayoff)
            {
                value = 70f * _dna.Genes[NewDna.TRIGGER_SACRIFICE_VISION];
                value += goodTargets * 30f * _dna.Genes[NewDna.TRIGGER_SACRIFICE_VISION];
                value += totalSacrificeValue * 0.5f;

                if (hasPayoff) value += 50f * _dna.Genes[NewDna.TRIGGER_SACRIFICE_VISION];
            }
            else
            {
                value = -40f * _dna.Genes[NewDna.TRIGGER_SACRIFICE_VISION];
            }

            return value * (_dna.Genes[NewDna.TARGETING_FRIENDLY_SACRIFICE_VALUE] / 10f);
        }

        private float CalculateSacrificeTargetValue(CardInstance unit)
        {
            float value = 0f;

            // Tokens mają niską wartość
            if (unit.Definition.Subtypes.Contains("Token"))
                value -= 20f;

            // Słabe jednostki mają niską wartość
            if (unit.CurrentStats.Attack <= 1 && unit.CurrentStats.Health <= 1)
                value -= 15f;

            // Jednostki ze stunem lub burningiem mają niską wartość
            if (unit.CurrentStats.Keywords.Contains(Keyword.Stunned) ||
                unit.CurrentStats.Keywords.Contains(Keyword.Burning))
                value -= 25f;

            // Jednostki które chcą być poświęcone (death effects) dodają wartość
            if (unit.Definition.Effects.Any(e => e.Trigger == TriggerType.OnDeath ||
                                                e.Trigger == TriggerType.OnSacrificed))
                value += 30f;

            return value;
        }

        private float EvaluateMovementValue(ActionData action, CardInstance sourceCard, PlayerState p, GameState state)
        {
            float value = 20f; // Bazowa wartość ruchu

            if (sourceCard != null)
            {
                int currentLane = GetLaneOfUnit(state, sourceCard.InstanceId);

                // Sprawdź czy ruch jest korzystny
                if (action.Type == ActionType.MoveRight && currentLane < 3)
                {
                    var targetLane = state.Board.Lines[currentLane + 1];
                    var targetSlot = p.PlayerId == 1 ? targetLane.Player1Unit : targetLane.Player2Unit;

                    // Bonus za przeniesienie do pustego slota
                    if (targetSlot == null)
                        value += 15f;
                }
                else if (action.Type == ActionType.MoveLeft && currentLane > 0)
                {
                    var targetLane = state.Board.Lines[currentLane - 1];
                    var targetSlot = p.PlayerId == 1 ? targetLane.Player1Unit : targetLane.Player2Unit;

                    if (targetSlot == null)
                        value += 15f;
                }
            }

            return value;
        }

        #endregion

        #region Condition Evaluation

        private float EstimateConditionLikelihood(ConditionData? condition, CardInstance sourceCard,
    PlayerState p, PlayerState o, GameState state)
        {
            if (condition == null) return 1.0f;

            switch (condition.Condition)
            {
                case ConditionType.HasSubtypeOnBoard:
                    return EstimateHasSubtypeOnBoard(condition.TargetParam, p, state);

                case ConditionType.IsSubtype:
                    return EstimateIsSubtype(condition.TargetParam, sourceCard, p, o, state);

                case ConditionType.IsStatus:
                    return EstimateIsStatus(condition.TargetParam, sourceCard, p, o, state);

                case ConditionType.IsEnemy:
                    // POPRAWA: Większe prawdopodobieństwo dla targetów wrogich
                    return 0.8f; // Zwiększone z 0.9f

                case ConditionType.IsSelf:
                    // POPRAWA: Mniejsze prawdopodobieństwo że target to my
                    return 0.05f; // Zmniejszone z 0.1f

                case ConditionType.Not:
                    if (condition.SubConditions != null && condition.SubConditions.Count > 0)
                    {
                        float subProb = EstimateConditionLikelihood(condition.SubConditions[0], sourceCard, p, o, state);
                        return 1.0f - subProb;
                    }
                    break;

                case ConditionType.And:
                    return EstimateAndCondition(condition.SubConditions, sourceCard, p, o, state);

                case ConditionType.Or:
                    return EstimateOrCondition(condition.SubConditions, sourceCard, p, o, state);
            }

            return 0.6f; // Domyślne prawdopodobieństwo
        }

        private float EstimateHasSubtypeOnBoard(string? subtype, PlayerState p, GameState state)
        {
            if (string.IsNullOrEmpty(subtype)) return 0.0f;

            int count = 0;
            foreach (var unit in state.Board.GetAllUnits())
            {
                if (unit.OwnerPlayerId == p.PlayerId && unit.Definition.Subtypes.Contains(subtype))
                    count++;
            }

            // Większe prawdopodobieństwo jeśli mamy więcej jednostek tego typu
            if (count >= 2) return 0.9f;
            if (count == 1) return 0.6f;

            // Ale dajmy szansę że może się pojawić
            return 0.3f; // Zwiększone z 0.0f
        }

        private float EstimateIsSubtype(string? subtype, CardInstance sourceCard, PlayerState p, PlayerState o, GameState state)
        {
            if (string.IsNullOrEmpty(subtype)) return 0.0f;

            // Dla sourceCard - sprawdź czy ma subtype
            if (sourceCard.Definition.Subtypes.Contains(subtype))
                return 1.0f;

            // Dla innych przypadków - średnie prawdopodobieństwo
            return 0.4f;
        }

        private float EstimateIsStatus(string? status, CardInstance sourceCard, PlayerState p, PlayerState o, GameState state)
        {
            if (string.IsNullOrEmpty(status)) return 0.0f;

            // Konwersja string na Keyword
            if (Enum.TryParse<Keyword>(status, out var keyword))
            {
                // Sprawdź czy sourceCard ma status
                if (sourceCard.CurrentStats.Keywords.Contains(keyword))
                    return 1.0f;

                // Sprawdź czy jakakolwiek jednostka ma status
                foreach (var unit in state.Board.GetAllUnits())
                {
                    if (unit.CurrentStats.Keywords.Contains(keyword))
                        return 0.7f;
                }
            }

            return 0.3f;
        }

        private float EstimateAndCondition(List<ConditionData>? subConditions, CardInstance sourceCard,
            PlayerState p, PlayerState o, GameState state)
        {
            if (subConditions == null || subConditions.Count == 0) return 0.0f;

            float minProbability = 1.0f;
            foreach (var sub in subConditions)
            {
                float subProb = EstimateConditionLikelihood(sub, sourceCard, p, o, state);
                minProbability = Math.Min(minProbability, subProb);
            }
            return minProbability;
        }

        private float EstimateOrCondition(List<ConditionData>? subConditions, CardInstance sourceCard,
            PlayerState p, PlayerState o, GameState state)
        {
            if (subConditions == null || subConditions.Count == 0) return 0.0f;

            float maxProbability = 0.0f;
            foreach (var sub in subConditions)
            {
                float subProb = EstimateConditionLikelihood(sub, sourceCard, p, o, state);
                maxProbability = Math.Max(maxProbability, subProb);
            }
            return maxProbability;
        }

        #endregion

        #region Board State Evaluation

        private float EvaluateBoardState(PlayerState p, PlayerState o, GameState state,
            float midM, float tempoM, float aggroM, List<CardInstance> allUnits,
            List<CardInstance> myUnits, List<CardInstance> enemyUnits,
            CardInstance[] lineAllies, CardInstance[] lineOpponents, Dictionary<int, int> unitLanes)
        {
            float score = 0f;

            // 1. FORMATION STRATEGY
            float densityDNA = _dna.Genes[NewDna.BOARD_LANE_DENSITY_STRATEGY];
            float precombatFocus = _dna.Genes[NewDna.COMBAT_PRECOMBAT_PRIORITY] / 10f;

            // Wide formation bonus
            score += myUnits.Count * (10.1f - densityDNA) * 40f * midM * (1f + precombatFocus * 0.3f);

            // Tall formation bonus
            if (myUnits.Count > 0)
            {
                float maxStats = 0f;
                foreach (var u in myUnits)
                {
                    float stats = u.CurrentStats.Attack + u.CurrentStats.Health;
                    if (stats > maxStats) maxStats = stats;
                }
                score += maxStats * densityDNA * 25f * midM;
            }

            // 2. UNIT EVALUATION
            foreach (var u in allUnits)
            {
                bool isFriendly = u.OwnerPlayerId == p.PlayerId;
                var stats = u.CurrentStats;
                int lane = unitLanes.TryGetValue(u.InstanceId, out int l) ? l : 0;

                float unitVal = EvaluateUnitValue(u, stats, lane, p, o, state, isFriendly, lineOpponents);

                // Add to score with appropriate weights
                if (isFriendly)
                    score += unitVal * tempoM;
                else
                    score -= unitVal * (1f + (_dna.Genes[NewDna.TARGETING_UNIT_PRIORITY_TYPE] / 10f)) * aggroM;
            }

            // 3. GEOMETRY EVALUATION
            score += EvaluateBoardGeometry(p, lineAllies, lineOpponents, aggroM);

            // 4. SPACE MANAGEMENT
            if (myUnits.Count >= 3)
            {
                float clogPenalty = _dna.Genes[NewDna.BOARD_SPACE_MANAGEMENT_LOGIC] * 40f;
                clogPenalty *= (1.1f - (_dna.Genes[NewDna.TRIGGER_DESTRUCTION_PREFERENCE] / 10f));
                score -= clogPenalty;
            }

            return score;
        }

        private float EvaluateUnitValue(CardInstance u, CardStats stats, int lane,
            PlayerState p, PlayerState o, GameState state, bool isFriendly, CardInstance[] lineOpponents)
        {
            float unitVal = (stats.Attack * 18f + stats.Health * 12f) *
                           (_dna.Genes[NewDna.UNIT_STAT_VS_EFFECT_WEIGHT] / 10f);

            // Engine detection
            if (IsEngineUnit(u))
            {
                if (isFriendly)
                    unitVal += 150f * _dna.Genes[NewDna.UNIT_ENGINE_DURABILITY_PRIORITY];
                else
                    unitVal += 200f;
            }

            // Subtype synergy
            foreach (var subtype in u.Definition.Subtypes)
            {
                int matches = CountSubtypeMatches(u, subtype, p.PlayerId, state);
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
            unitVal += EvaluateUnitKeywords(u, stats, lane, p, lineOpponents);

            // Effect evaluation
            unitVal += EvaluateUnitEffects(u, p, o, state);

            // Trading efficiency modifier
            float tradingEfficiency = _dna.Genes[NewDna.COMBAT_TRADING_EFFICIENCY] / 10f;
            unitVal *= (1f + tradingEfficiency * 0.2f);

            return unitVal;
        }

        private int CountSubtypeMatches(CardInstance sourceUnit, string subtype, int playerId, GameState state)
        {
            int matches = 0;
            foreach (var unit in state.Board.GetAllUnits())
            {
                if (unit.OwnerPlayerId == playerId &&
                    unit.InstanceId != sourceUnit.InstanceId &&
                    unit.Definition.Subtypes.Contains(subtype))
                {
                    matches++;
                }
            }
            return matches;
        }

        private float EvaluateUnitKeywords(CardInstance u, CardStats stats, int lane,
            PlayerState p, CardInstance[] lineOpponents)
        {
            float val = 0f;

            // Armored efficiency
            if (stats.Keywords.Contains(Keyword.Armored))
            {
                var opp = lineOpponents[lane];
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
                if (lane - 1 >= 0 && lineOpponents[lane - 1] != null) targets++;
                if (lane + 1 < 4 && lineOpponents[lane + 1] != null) targets++;

                float splashMultiplier = 1f + (_dna.Genes[NewDna.BOARD_SPLASH_OPTIMIZATION_LOGIC] / 20f);
                val += targets * power * _dna.Genes[NewDna.KEYWORD_SPLASH_PRIORITY] * 30f * splashMultiplier;
            }

            // Flying value
            if (stats.Keywords.Contains(Keyword.Flying))
            {
                var opp = lineOpponents[lane];
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
                val -= 25f;

            return val;
        }

        private float EvaluateUnitEffects(CardInstance u, PlayerState p, PlayerState o, GameState state)
        {
            float val = 0f;

            // Bonus attack value
            if (u.Definition.Effects.Any(e => e.Actions.Any(a => a.Type == ActionType.BonusAttack)))
                val += _dna.Genes[NewDna.KEYWORD_BONUS_ATTACK_VALUE] * 40f;

            // Movement value
            if (u.Definition.Effects.Any(e => e.Actions.Any(a =>
                a.Type == ActionType.MoveRight || a.Type == ActionType.MoveLeft)))
            {
                bool hasGoodMove = false;
                int lane = GetLaneOfUnit(state, u.InstanceId);

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
                float effectValue = 0f;
                foreach (var action in effect.Actions)
                {
                    effectValue += EvaluateActionValue(action, u, p, o, state);
                }

                // Scale by trigger frequency
                switch (effect.Trigger)
                {
                    case TriggerType.Passive:
                        effectValue *= 2.0f;
                        break;
                    case TriggerType.OnPreCombatLine:
                        effectValue *= 1.5f;
                        break;
                    case TriggerType.OnKill:
                    case TriggerType.OnDamagedEnemyHero:
                        effectValue *= 0.8f;
                        break;
                    default:
                        effectValue *= 1.0f;
                        break;
                }

                val += effectValue;
            }

            return val;
        }

        private float EvaluateBoardGeometry(PlayerState p, CardInstance[] lineAllies,
            CardInstance[] lineOpponents, float aggroM)
        {
            float score = 0f;

            for (int i = 0; i < 4; i++)
            {
                var myU = lineAllies[i];
                var enU = lineOpponents[i];

                // Threat symmetry
                if (myU != null && enU != null && myU.CurrentStats.Health > enU.CurrentStats.Attack)
                    score += enU.CurrentStats.Attack * _dna.Genes[NewDna.BOARD_THREAT_SYMMETRY] * 12f;

                // Horizontal focus bias
                if (myU != null)
                {
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

        #endregion

        #region Tactical Execution

        private float EvaluateTacticalExecution(PlayerState p, PlayerState o, GameState state,
            float comboM, float futureM, float tempoM, float ctrlM, float aggroM,
            CardInstance[] lineOpponents, CardInstance[] lineAllies)
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
                float sacrificeScore = EvaluateSacrificeTactics(p, state);
                score += sacrificeScore * (_dna.Genes[NewDna.TARGETING_FRIENDLY_SACRIFICE_VALUE] / 10f);
            }

            // 5. DAMAGE OPTIMIZATION (avoid overkill)
            float wastePenalty = CalculateDamageWastePenalty(p, state, lineOpponents);
            score += wastePenalty;

            // 6. TRADE EVALUATION
            float exchangeEfficiency = EvaluateTradeEfficiency(p, lineAllies, lineOpponents);
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

        private float CalculateDamageWastePenalty(PlayerState p, GameState state, CardInstance[] lineOpponents)
        {
            float wastePenalty = 0f;

            foreach (var card in p.Hand.Where(c => c.CurrentStats.BloodCost <= p.CurrentBlood))
            {
                foreach (var effect in card.Definition.Effects.Where(e => e.Trigger == TriggerType.OnPlayed))
                {
                    foreach (var action in effect.Actions.Where(a => a.Type == ActionType.DealDamage))
                    {
                        float worstOverkill = FindWorstOverkill(action, lineOpponents);
                        wastePenalty -= worstOverkill * (11f - _dna.Genes[NewDna.TARGETING_DAMAGE_WASTE_TOLERANCE]) * 5f;
                    }
                }
            }

            return wastePenalty;
        }

        private float FindWorstOverkill(ActionData action, CardInstance[] lineOpponents)
        {
            float worstOverkill = 0f;

            for (int i = 0; i < 4; i++)
            {
                var enemy = lineOpponents[i];
                if (enemy != null && action.Amount > enemy.CurrentStats.Health)
                {
                    float overkill = action.Amount - enemy.CurrentStats.Health;
                    if (overkill > worstOverkill) worstOverkill = overkill;
                }
            }

            return worstOverkill;
        }

        private float EvaluateTradeEfficiency(PlayerState p, CardInstance[] lineAllies, CardInstance[] lineOpponents)
        {
            float exchangeEfficiency = 0f;

            for (int i = 0; i < 4; i++)
            {
                var myU = lineAllies[i];
                var enU = lineOpponents[i];

                if (myU != null && enU != null)
                {
                    bool mySurvives = myU.CurrentStats.Health > enU.CurrentStats.Attack;
                    bool enemyDies = enU.CurrentStats.Health <= myU.CurrentStats.Attack;

                    if (mySurvives && enemyDies)
                        exchangeEfficiency += 25f;
                    else if (!mySurvives && enemyDies)
                        exchangeEfficiency += 0f;
                    else if (mySurvives && !enemyDies)
                        exchangeEfficiency -= 15f;
                    else if (!mySurvives && !enemyDies)
                        exchangeEfficiency -= 5f;
                }
            }

            return exchangeEfficiency;
        }

        private float EvaluateSacrificeTactics(PlayerState p, GameState state)
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

            bool hasPayoffInHand = p.Hand.Any(c => BenefitsFromSacrifice(c));

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

        #endregion

        #region Psychology

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

        #endregion

        #region Utility Functions

        private bool IsSacrificeActivator(CardInstance c)
        {
            if (c == null) return false;
            return c.Definition.Effects.Any(e =>
                                            e.Actions.Any(a => (a.Type == ActionType.SacrificeUnit)
                                                && (a.Target == TargetType.TargetFriendlyUnit 
                                                 || a.Target == TargetType.OtherFriendlyUnits 
                                                 || a.Target == TargetType.SelectedTarget))
            );
        }

        private bool IsEngineUnit(CardInstance unit)
        {
            if (unit == null) return false;

            return unit.Definition.Effects.Any(e =>
                // 1. Aury kosztów (NOWOŚĆ: Anti Matter Watermonster, Tea Maid)
                (e.Trigger == TriggerType.Passive && e.Zone == EffectZone.Board &&
                 e.Actions.Any(a => a.Target == TargetType.FriendlySpellsInHand ||
                                   a.Target == TargetType.EnemySpellsInHand)) ||

                // 2. Aury Keywordów (np. Survivalist dający SoulGuard innym)
                (e.Trigger == TriggerType.Passive && e.Zone == EffectZone.Board &&
                 e.Actions.Any(a => a.Type == ActionType.ApplyStatus && a.StatusKeyword.HasValue)) ||

                // 3. Generatory zasobów i dociągu (Queen of Cards, Raptor)
                (e.Actions.Any(a => a.Type == ActionType.AddResource ||
                                   a.Type == ActionType.DrawCard ||
                                   a.Type == ActionType.AddCardToHand)) ||

                // 4. Silniki reaktywne (Little Bob, Gerard, Vane)
                (e.Trigger == TriggerType.OnFriendlyActionPlayed && e.Zone == EffectZone.Board) ||
                (e.Trigger == TriggerType.OnFriendlyUnitDied && e.Zone == EffectZone.Board) ||
                (e.Trigger == TriggerType.OnStatusApplied && e.Zone == EffectZone.Board) ||
                (e.Trigger == TriggerType.OnOtherUnitSacrificed && e.Zone == EffectZone.Board) ||

                // 5. Silniki agresywne (Juggernaut, Raptor)
                (e.Trigger == TriggerType.OnDamagedEnemyHero && e.Zone == EffectZone.Board) ||
                (e.Trigger == TriggerType.OnDamagedEnemyUnit && e.Zone == EffectZone.Board) ||
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

        private bool CanUnitHitFace(CardInstance myUnit, CardInstance enemyUnit)
        {
            if (myUnit == null) return false;
            if (myUnit.CurrentStats.Keywords.Contains(Keyword.Stunned)) return false;
            if (enemyUnit == null) return true;

            bool myFlying = myUnit.CurrentStats.Keywords.Contains(Keyword.Flying);
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

        private CardInstance GetOpponentInLane(GameState state, int lane, int myId)
        {
            return myId == 1 ? state.Board.Lines[lane].Player2Unit : state.Board.Lines[lane].Player1Unit;
        }

        #endregion
    }
}