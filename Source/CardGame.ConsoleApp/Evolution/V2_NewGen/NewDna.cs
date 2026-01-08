using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.AccessControl;

namespace CardGame.ConsoleApp.Evolution.V2_NewGen
{
    public class NewDna
    {
        public const int TOTAL_GENES = 82;
        public const string TOKEN_SUBTYPE = "Token";

        public float[] Genes { get; private set; }

        // ==================== 1. STYLE & STRATEGY (Archetypy) ====================
        public const int STYLE_FUTURE_PLANNING_BIAS = 0;
        public const int STYLE_TEMPO_PREFERENCE = 1;
        public const int STYLE_MIDRANGE = 2;
        public const int STYLE_CONTROL = 3;
        public const int STYLE_COMBO = 4;
        public const int STYLE_AGGRO = 5;
        public const int STYLE_BURN = 6;

        // ==================== 2. LIFE (Bohater - 20 HP) ====================
        public const int LIFE_RISK_APPETITE_AT_LETHAL = 7;
        public const int LIFE_INDIRECT_DAMAGE_FOCUS = 8;
        public const int LIFE_COMBAT_ORDER_URGENCY = 9;
        public const int LIFE_OVERHEAL_APPRECIATION = 10;
        public const int LIFE_VULNERABILITY_SENSE = 11;
        public const int LIFE_OPPONENT_HP_WEIGHT = 12;
        public const int LIFE_HEAL_PRIORITY_HERO = 13;
        public const int LIFE_PANIC_THRESHOLD = 14;
        public const int LIFE_LETHAL_BUFFER = 15;
        public const int LIFE_LETHAL_VISION = 16;
        public const int LIFE_SELF_HP_VALUE = 17;

        // ==================== 3. LOGIC & RESOURCES (Zarządzanie kartami) ====================
        public const int LOGIC_HAND_SIZE_OPTIMIZATION = 18;
        public const int LOGIC_OVERDRAW_RISK_TOLERANCE = 19;
        public const int LOGIC_GIVING_RESOURCES_PENALTY = 20;
        public const int LOGIC_REACTION_FROM_HAND_SENSE = 21;
        public const int LOGIC_STRATEGY_COST_REDUCTION = 22;
        public const int LOGIC_PROBABILITY_COGNITION = 23;
        public const int LOGIC_DECK_THINNING_FOCUS = 24;
        public const int LOGIC_DISCARD_PILE_RECYCLE = 25;
        public const int LOGIC_TUTOR_PRECISION = 26;
        public const int LOGIC_DRAW_URGENCY = 27;
        public const int LOGIC_PATIENCE_FACTOR = 28;
        public const int LOGIC_MANA_CURVE_BIAS = 29;

        // ==================== 4. PHASE_LOGIC (Inicjatywa tury) ====================
        public const int PHASE_MANA_RESERVE_STRATEGY = 30;
        public const int PHASE_INITIATIVE_POSTURE = 31;
        public const int PHASE_EFFICIENCY_VS_VALUE = 32;
        public const int PHASE_REACTION_SENSITIVITY = 33;
        public const int PHASE_COUNTER_ANTICIPATION = 34;

        // ==================== 5. BOARD_GEOMETRY (Pozycjonowanie) ====================
        public const int BOARD_SPLASH_OPTIMIZATION_LOGIC = 35;
        public const int BOARD_FLYING_LANE_OPPORTUNISM = 36;
        public const int BOARD_PLACEMENT_REACTION_TYPE = 37;
        public const int BOARD_HORIZONTAL_FOCUS_BIAS = 38;
        public const int BOARD_LANE_DENSITY_STRATEGY = 39;
        public const int BOARD_SPACE_MANAGEMENT_LOGIC = 40;
        public const int BOARD_MOVEMENT_VALUE_SENSE = 41;
        public const int BOARD_THREAT_SYMMETRY = 42;

        // ==================== 6. TARGETING & COMBAT (Celowanie i walka) ====================
        public const int TARGETING_AOE_SYMMETRY_TOLERANCE = 43;
        public const int TARGETING_FRIENDLY_SACRIFICE_VALUE = 44;
        public const int TARGETING_DAMAGE_WASTE_TOLERANCE = 45;
        public const int COMBAT_RESOURCE_EXCHANGE_EFFICIENCY = 46;
        public const int TARGETING_UNIT_PRIORITY_TYPE = 47;
        public const int TARGETING_FACE_VS_BOARD_BIAS = 48;
        public const int TARGETING_STATUS_PHILOSOPHY = 49;
        public const int TARGETING_REACTIVE_VS_PROACTIVE = 50;
        public const int COMBAT_TRADING_EFFICIENCY = 51;
        public const int COMBAT_PRECOMBAT_PRIORITY = 52;
        public const int COMBAT_LETHAL_CONFIDENCE = 53;

        // ==================== 7. UNIT_CONTEXT (Roles jednostek) ====================
        public const int UNIT_ENGINE_DURABILITY_PRIORITY = 54;
        public const int UNIT_STAT_VS_EFFECT_WEIGHT = 55;
        public const int UNIT_SUBTYPE_COHESION_WEIGHT = 56;
        public const int UNIT_PROTECTION_SYNERGY_BIAS = 57;
        public const int UNIT_REPLACEMENT_URGENCY = 58;
        public const int UNIT_TOKEN_VALUATION_LOGIC = 59;

        // ==================== 8. KEYWORDS & TRIGGERS (Mechaniki) ====================
        public const int KEYWORD_UNKILLABLE_RECURSION = 60;
        public const int KEYWORD_BURNSOURCE_AGGRESSION = 61;
        public const int KEYWORD_ARMORED_EFFICIENCY = 62;
        public const int KEYWORD_BONUS_ATTACK_VALUE = 63;
        public const int KEYWORD_MARKED_FOCUS = 64;
        public const int KEYWORD_SPLASH_PRIORITY = 65;
        public const int KEYWORD_SOULGUARD_STICKINESS = 66;
        public const int KEYWORD_STUN_DENIAL = 67;
        public const int KEYWORD_FLYING_VALUE = 68;

        public const int TRIGGER_DESTRUCTION_PREFERENCE = 69;
        public const int TRIGGER_SACRIFICE_VISION = 70;
        public const int TRIGGER_DEATH_STRATEGY = 71;
        public const int TRIGGER_PLAY_STRATEGY = 72;
        public const int TRIGGER_COMBAT_STRATEGY = 73;
        public const int TRIGGER_SETUP_RECOGNITION = 74;

        // ==================== 9. PSYCHOLOGY (Osobowość) ====================
        public const int PSYCH_RESOURCE_ADVANTAGE_GREED = 75;
        public const int PSYCH_PREDICTIVE_CAUTION = 76;
        public const int PSYCH_OPPONENT_MANA_RESPECT = 77;
        public const int PSYCH_DECEPTION_BAITING = 78;
        public const int PSYCH_BLUFF_RESISTANCE = 79;
        public const int PSYCH_ADAPTABILITY = 80;

        // ==================== 10. BUFFS (Wzmacnianie) ====================
        public const int BUFF_PROTECTION_OR_AMPLIFY = 81;

        // ========== KONSTRUKTORY ==========
        public NewDna()
        {
            Genes = new float[TOTAL_GENES];
        }

        public NewDna(float[] genes)
        {
            if (genes.Length != TOTAL_GENES)
                throw new ArgumentException($"DNA must have {TOTAL_GENES} genes, got {genes.Length}");
            Genes = genes;
        }

        // ========== METODY FABRYKUJĄCE ==========
        public static NewDna CreateRandom(Random rng)
        {
            var dna = new NewDna();
            for (int i = 0; i < TOTAL_GENES; i++)
                dna.Genes[i] = (float)(rng.NextDouble() * 10.0);
            return dna;
        }

        public static NewDna CreateArchetypeDNA(BotArchetype archetype, Random rng)
        {
            var dna = CreateRandom(rng);
            dna.ApplyArchetypeBias(archetype, rng, strength: 0.8f);
            return dna;
        }

        // ========== OPERACJE GENETYCZNE ==========
        public NewDna Clone()
        {
            return new NewDna((float[])Genes.Clone());
        }

        public static NewDna Crossover(NewDna parent1, NewDna parent2, Random rng)
        {
            var childGenes = new float[TOTAL_GENES];
         
            int crossoverPoint = rng.Next(TOTAL_GENES);

            for (int i = 0; i < TOTAL_GENES; i++)
            {
         
                if (rng.NextDouble() < 0.5)
                    childGenes[i] = parent1.Genes[i];
                else
                    childGenes[i] = parent2.Genes[i];

             
            }

            return new NewDna(childGenes);
        }

        public void Mutate(Random rng, int stagnationCounter)
        {
            // ZWIĘKSZONA MUTACJA: Im większy stagnationCounter, tym większa mutacja
            float mutationRate = Math.Min(0.5f, 0.1f + stagnationCounter * 0.02f);

            for (int i = 0; i < TOTAL_GENES; i++)
            {
                if (rng.NextDouble() < mutationRate)
                {
                    float roll = (float)rng.NextDouble();

                    if (roll < 0.4f)
                    {
                        // Mała perturbacja
                        Genes[i] = Math.Clamp(Genes[i] + (float)(rng.NextDouble() * 2 - 1), 0, 10);
                    }
                    else if (roll < 0.7f)
                    {
                        // Losowa wartość
                        Genes[i] = (float)(rng.NextDouble() * 10);
                    }
                    else
                    {
                        // Duża zmiana (przesunięcie o 3 w losowym kierunku)
                        float shift = (float)(rng.NextDouble() * 6 - 3);
                        Genes[i] = Math.Clamp(Genes[i] + shift, 0, 10);
                    }
                }
            }
        }

        // ========== METRYKI ==========
        public static float CalculateDistance(NewDna a, NewDna b)
        {
            float sum = 0;
            for (int i = 0; i < TOTAL_GENES; i++)
            {
                float diff = a.Genes[i] - b.Genes[i];
                sum += diff * diff;
            }
            return (float)Math.Sqrt(sum) / TOTAL_GENES;
        }

        public static NewDna Average(List<NewDna> dnaList)
        {
            if (dnaList.Count == 0) return new NewDna();

            var avgGenes = new float[TOTAL_GENES];
            for (int i = 0; i < TOTAL_GENES; i++)
                avgGenes[i] = dnaList.Average(d => d.Genes[i]);

            return new NewDna(avgGenes);
        }

        // ========== ARCHETYPY ==========
        public void ApplyArchetypeBias(BotArchetype archetype, Random rng, float strength = 0.5f)
        {
            Dictionary<string, Dictionary<int, float>> archetypeBiases = new()
            {
                ["Aggro"] = new()
                {
                    [STYLE_AGGRO] = 9.0f,
                    [STYLE_TEMPO_PREFERENCE] = 8.0f,
                    [TARGETING_FACE_VS_BOARD_BIAS] = 8.0f
                },
                ["Control"] = new()
                {
                    [STYLE_CONTROL] = 9.0f,
                    [STYLE_FUTURE_PLANNING_BIAS] = 8.0f,
                    [LOGIC_HAND_SIZE_OPTIMIZATION] = 8.0f
                },
                ["Combo"] = new()
                {
                    [STYLE_COMBO] = 9.0f,
                    [LOGIC_TUTOR_PRECISION] = 9.0f,
                    [TRIGGER_SETUP_RECOGNITION] = 8.0f
                },
                ["Midrange"] = new()
                {
                    [STYLE_MIDRANGE] = 9.0f,
                    [UNIT_STAT_VS_EFFECT_WEIGHT] = 8.0f,
                    [COMBAT_TRADING_EFFICIENCY] = 8.0f
                },
                ["Sacrifice"] = new()
                {
                    [TRIGGER_SACRIFICE_VISION] = 9.0f,
                    [TRIGGER_DEATH_STRATEGY] = 9.0f,
                    [TARGETING_FRIENDLY_SACRIFICE_VALUE] = 8.0f
                }
            };

            if (archetypeBiases.TryGetValue(archetype.Name, out var biases))
            {
                foreach (var kvp in biases)
                    Genes[kvp.Key] = Genes[kvp.Key] * (1 - strength) + kvp.Value * strength;
            }
        }

        public string ClassifyArchetype()
        {
            var styleScores = new Dictionary<string, float>
            {
                ["Aggro"] = Genes[STYLE_AGGRO] * 1.2f + Genes[TARGETING_FACE_VS_BOARD_BIAS] * 0.3f,
                ["Control"] = Genes[STYLE_CONTROL] * 1.2f + Genes[LOGIC_HAND_SIZE_OPTIMIZATION] * 0.3f,
                ["Combo"] = Genes[STYLE_COMBO] * 1.2f + Genes[LOGIC_TUTOR_PRECISION] * 0.3f,
                ["Midrange"] = Genes[STYLE_MIDRANGE] * 1.2f + Genes[COMBAT_TRADING_EFFICIENCY] * 0.3f,
                ["Sacrifice"] = Genes[TRIGGER_SACRIFICE_VISION] * 0.9f +
                               Genes[TRIGGER_DEATH_STRATEGY] * 0.4f +
                               Genes[TARGETING_FRIENDLY_SACRIFICE_VALUE] * 0.2f
            };

            return styleScores.OrderByDescending(kvp => kvp.Value).First().Key;
        }

        // ========== ULEPSZONE BUDOWANIE TALII ==========
        public int[] BuildDeck(int[] cardPool, Random rng, Dictionary<int, CardStatsEnhanced> stats = null)
        {
            const int DECK_SIZE = 30;
            const int MAX_COPIES_PER_CARD = 3;

            var deck = new List<int>();
            var cardLibrary = CardLibrary.Instance;

            var scoredCards = new Dictionary<int, (float baseScore, CardData data)>();
            foreach (int cardId in cardPool)
            {
                try
                {
                    var cardData = cardLibrary.GetCard(cardId);
                    // ScoreCard teraz zwraca Efficiency (Value / Cost)
                    float baseScore = ScoreCard(cardData);
                    scoredCards[cardId] = (baseScore, cardData);
                }
                catch { }
            }

            var counts = new Dictionary<int, int>();
            var deckCardData = new List<CardData>();

            for (int i = 0; i < DECK_SIZE; i++)
            {
                var deckAnalysis = AnalyzeCurrentDeck(deckCardData);
                var bestChoice = SelectBestCardForDeck(scoredCards, counts, deckCardData, deckAnalysis, rng, stats);

                if (bestChoice.cardId > 0)
                {
                    deck.Add(bestChoice.cardId);
                    counts[bestChoice.cardId] = counts.GetValueOrDefault(bestChoice.cardId, 0) + 1;
                    deckCardData.Add(scoredCards[bestChoice.cardId].data);
                }
                else
                {
                    int randomCardId = cardPool[rng.Next(cardPool.Length)];
                    if (!counts.ContainsKey(randomCardId) || counts[randomCardId] < MAX_COPIES_PER_CARD)
                    {
                        deck.Add(randomCardId);
                        counts[randomCardId] = counts.GetValueOrDefault(randomCardId, 0) + 1;
                        deckCardData.Add(scoredCards[randomCardId].data);
                    }
                }
            }
            return deck.OrderBy(x => rng.Next()).ToArray();
        }

        // 2. WYBÓR KONKRETNEJ KARTY DO DECKU
        private (int cardId, float score) SelectBestCardForDeck(
            Dictionary<int, (float baseScore, CardData data)> scoredCards,
            Dictionary<int, int> counts,
            List<CardData> currentDeck,
            DeckAnalysis analysis,
            Random rng,
            Dictionary<int, CardStatsEnhanced> stats)
        {
            const int MAX_COPIES = 3;
            var candidates = new List<(int cardId, float score)>();

            float controlStyle = Genes[STYLE_CONTROL] / 10f;
            float intuition = Genes[LOGIC_PROBABILITY_COGNITION] / 10f;

            foreach (var kvp in scoredCards)
            {
                int cardId = kvp.Key;
                var cardData = kvp.Value.data;
                float baseScore = kvp.Value.baseScore;

                int currentCopies = counts.GetValueOrDefault(cardId, 0);
                if (currentCopies >= MAX_COPIES) continue;

                // Waga kopii zależna od DNA
                float copyWeight = CalculateCopyWeight(currentCopies + 1);
                float contextualScore = baseScore * copyWeight;

                // Dynamiczna kara za drogie karty (Control boi się ich mniej)
                if (cardData.Cost >= 5)
                {
                    float heavyPenaltyBase = 15f * (1.1f - controlStyle);
                    float currentHeavyCount = currentDeck.Count(c => c.Cost >= 5);
                    contextualScore -= (currentHeavyCount * heavyPenaltyBase);
                }

                // Użycie Snapshotu Statystyk (zamrożone dane z poprzedniej gen)
                if (stats != null && stats.TryGetValue(cardId, out var stat))
                {
                    if (stat.GamesWithCard > 20)
                    {
                        float wrOffset = stat.WinRate - 0.50f;
                        contextualScore += (wrOffset * 500f * intuition);

                        if (stat.UseRate > 0.80f)
                            contextualScore *= (0.7f + (0.3f * controlStyle));
                    }
                    else { contextualScore += 40f; } // Bonus za eksplorację nowości
                }

                contextualScore += CalculateCurveAdjustment(cardData, analysis);
                contextualScore += CalculateSynergyBonus(cardData, currentDeck);

                // Kontrolowany szum 10%
                float noise = 1.0f + (float)(rng.NextDouble() * 0.2 - 0.1);
                contextualScore *= noise;

                candidates.Add((cardId, contextualScore));
            }

            if (!candidates.Any()) return (0, 0);
            var sorted = candidates.OrderByDescending(c => c.score).Take(3).ToList();
            return sorted[rng.Next(sorted.Count)];
        }

        private float CalculateCopyWeight(int copyNumber)
        {
            if (copyNumber == 1) return 1.0f; // Pierwsza kopia zawsze super

            // Pobieramy geny
            float aggroStyle = Genes[STYLE_AGGRO] / 10f;
            float comboStyle = Genes[STYLE_COMBO] / 10f;
            float controlStyle = Genes[STYLE_CONTROL] / 10f;

            // Aggro i Combo potrzebują powtarzalności (3 kopie są kluczowe)
            // Control woli "toolbox" (więcej różnych kart na różne sytuacje, mniej kopii tych samych)
            float consistencyPreference = (aggroStyle * 0.4f) + (comboStyle * 0.6f) - (controlStyle * 0.3f);

            // Bazowy spadek wartości dla kolejnych kopii
            float drop = (copyNumber == 2) ? 0.2f : 0.4f;

            // Korygujemy spadek przez preferencje bota
            // Jeśli consistencyPreference jest wysokie, drop będzie mniejszy (czyli 2 i 3 kopia będą mocniejsze)
            float adjustedDrop = Math.Clamp(drop - (consistencyPreference * 0.25f), 0.05f, 0.8f);

            return 1.0f - adjustedDrop;
        }

        private float CalculateCurveAdjustment(CardData card, DeckAnalysis analysis)
        {
            float curvePreference = Genes[LOGIC_MANA_CURVE_BIAS] / 10f;
            float targetAvgCost = 4.5f - (curvePreference * 2.7f);

            float predictedAvgCost = (analysis.totalCost + card.Cost) / (analysis.cardCount + 1);
            float deviation = Math.Abs(predictedAvgCost - targetAvgCost);

        
            float buildingProgress = analysis.cardCount / 30f;
            float penaltyWeight = 5.0f * buildingProgress;

            return -deviation * penaltyWeight;

         
        }

        private float CalculateSynergyBonus(CardData card, List<CardData> currentDeck)
        {
            if (!currentDeck.Any()) return 0;

            float bonus = 0;

            // Synergie przez subtype
            var sharedSubtypes = currentDeck
                .SelectMany(d => d.Subtypes ?? new List<string>())
                .Intersect(card.Subtypes ?? new List<string>())
                .Count();

            bonus += sharedSubtypes * 5.0f * Genes[UNIT_SUBTYPE_COHESION_WEIGHT] * 0.1f;

            // Synergie przez efekty
            foreach (var deckCard in currentDeck)
            {
                // Mark synergy
                bool hasMarkEffect = card.Effects?.Any(e =>
                    e.Actions?.Any(a => a.StatusKeyword == Keyword.Marked) == true) == true;
                bool hasMarkPayoff = deckCard.Effects?.Any(e =>
                    e.Trigger == TriggerType.OnStatusApplied &&
                    e.Condition?.SubConditions?.Any(sc => sc.Condition == ConditionType.IsStatus && sc.TargetParam == "Marked") == true) == true;

                if ((hasMarkEffect && hasMarkPayoff) || (hasMarkPayoff && hasMarkEffect))
                    bonus += 80.0f * Genes[KEYWORD_MARKED_FOCUS] * 0.1f;

                // Sacrifice synergy
                bool isSacrificeActivator = card.Effects?.Any(e =>
                    e.Actions?.Any(a => a.Type == ActionType.SacrificeUnit || a.Type == ActionType.DestroyUnit) == true) == true;
                bool hasSacrificePayoff = deckCard.Effects?.Any(e =>
                    e.Trigger == TriggerType.OnSacrificed || e.Trigger == TriggerType.OnOtherUnitSacrificed) == true;

                if ((isSacrificeActivator && hasSacrificePayoff) || (hasSacrificePayoff && isSacrificeActivator))
                    bonus += 100.0f * Genes[TRIGGER_SACRIFICE_VISION] * 0.1f;

                // Draw synergy (karty które benefit z draw)
                bool givesDraw = card.Effects?.Any(e =>
                    e.Actions?.Any(a => a.Type == ActionType.DrawCard) == true) == true;
                bool benefitsFromDraw = deckCard.Effects?.Any(e =>
                    e.Trigger == TriggerType.OnFriendlyCardDrawn ||
                    (e.Zone == EffectZone.Hand && e.Trigger == TriggerType.OnFriendlyActionPlayed)) == true;

                if (givesDraw && benefitsFromDraw)
                    bonus += 25.0f * Genes[LOGIC_HAND_SIZE_OPTIMIZATION] * 0.1f;
            }

            return bonus;
        }

        private float CalculateArchetypeBonus(CardData card)
        {
            string archetype = ClassifyArchetype();

            return archetype switch
            {
                "Aggro" => CalculateAggroBonus(card),
                "Control" => CalculateControlBonus(card),
                "Combo" => CalculateComboBonus(card),
                "Midrange" => CalculateMidrangeBonus(card),
                "Sacrifice" => CalculateSacrificeBonus(card),
                _ => 0
            };
        }

        private float CalculateAggroBonus(CardData card)
        {
            float bonus = 0;

            // Aggro lubi tanie jednostki z wysokim atakiem
            if (card.Type == CardType.Unit)
            {
                // Bonus za niski koszt
                bonus += (5 - card.Cost) * 3.0f;

                // Bonus za wysoki atak w stosunku do kosztu
                float attackEfficiency = card.Attack / Math.Max(1, card.Cost);
                bonus += attackEfficiency * 10.0f;

                // Bonus za Charge/Flying (bezpośredni atak)
                if (card.Keywords?.Contains(Keyword.Flying) == true)
                    bonus += 15.0f;

                // Bonus za Bonus Attack
                if (card.Effects?.Any(e => e.Actions?.Any(a => a.Type == ActionType.BonusAttack) == true) == true)
                    bonus += 20.0f;
            }

            return bonus * (Genes[STYLE_AGGRO] / 10.0f);
        }

        private float CalculateControlBonus(CardData card)
        {
            float bonus = 0;

            // Control lubi spelle i jednostki z dużą żywotnością
            if (card.Type == CardType.Spell)
            {
                bonus += 10.0f;

                // Bonus za board control
                if (card.Effects?.Any(e =>
                    e.Actions?.Any(a => a.Type == ActionType.DestroyUnit ||
                                       a.Type == ActionType.DealDamage) == true) == true)
                    bonus += 15.0f;
            }
            else if (card.Type == CardType.Unit)
            {
                // Bonus za wysokie HP
                float healthEfficiency = card.Health / Math.Max(1, card.Cost);
                bonus += healthEfficiency * 8.0f;

                // Bonus za efekty kontroli
                if (card.Effects?.Any(e =>
                    e.Actions?.Any(a => a.Type == ActionType.Silence ||
                                       a.Type == ActionType.ApplyStatus ||
                                       a.Type == ActionType.ReturnToHand) == true) == true)
                    bonus += 20.0f;
            }

            return bonus * (Genes[STYLE_CONTROL] / 10.0f);
        }

        private float CalculateComboBonus(CardData card)
        {
            float bonus = 0;

            // Combo lubi karty które pomagają znaleźć combo pieces
            if (card.Effects?.Any(e =>
                e.Actions?.Any(a => a.Type == ActionType.TutorCard ||
                                   a.Type == ActionType.DrawCard) == true) == true)
                bonus += 25.0f;

            // Bonus za karty które są częścią combo
            bool isComboPiece = card.Effects?.Any(e =>
                e.Trigger == TriggerType.OnStatusApplied ||
                e.Trigger == TriggerType.OnFriendlyActionPlayed ||
                (e.Trigger == TriggerType.OnPlayed && e.Actions?.Count > 1)) == true;

            if (isComboPiece)
                bonus += 15.0f;

            return bonus * (Genes[STYLE_COMBO] / 10.0f);
        }

        private float CalculateMidrangeBonus(CardData card)
        {
            float bonus = 0;

            // Midrange lubi zrównoważone karty
            if (card.Type == CardType.Unit)
            {
                // Bonus za dobre staty za koszt
                float statEfficiency = (card.Attack + card.Health) / Math.Max(1, card.Cost * 2);
                bonus += statEfficiency * 15.0f;

                // Bonus za efekty które działają same z siebie
                if (card.Effects?.Any(e => e.Trigger == TriggerType.OnPlayed) == true)
                    bonus += 10.0f;
            }

            return bonus * (Genes[STYLE_MIDRANGE] / 10.0f);
        }

        private float CalculateSacrificeBonus(CardData card)
        {
            float bonus = 0;

            // Sacrifice lubi karty związane z poświęcaniem
            bool isSacrificeRelated = card.Effects?.Any(e =>
                e.Trigger == TriggerType.OnSacrificed ||
                e.Trigger == TriggerType.OnOtherUnitSacrificed ||
                e.Trigger == TriggerType.OnFriendlyUnitDied ||
                e.Actions?.Any(a => a.Type == ActionType.SacrificeUnit) == true) == true;

            if (isSacrificeRelated)
                bonus += 30.0f * Genes[TRIGGER_SACRIFICE_VISION] * 0.1f;

            // Bonus za tokeny (łatwe do poświęcenia)
            if (card.Subtypes?.Contains("Token") == true)
                bonus += 15.0f;

            // Bonus za efekty śmierci
            if (card.Effects?.Any(e => e.Trigger == TriggerType.OnDeath) == true)
                bonus += 20.0f * Genes[TRIGGER_DEATH_STRATEGY] * 0.1f;

            return bonus;
        }

        private DeckAnalysis AnalyzeCurrentDeck(List<CardData> deck)
        {
            if (!deck.Any())
                return new DeckAnalysis { cardCount = 0, totalCost = 0, uniqueCards = 0 };

            return new DeckAnalysis
            {
                cardCount = deck.Count,
                totalCost = deck.Sum(c => c.Cost),
                uniqueCards = deck.Select(c => c.Id).Distinct().Count()
            };
        }

        private float GetIdealAverageCost(string archetype)
        {
            return archetype switch
            {
                "Aggro" => 2.5f,
                "Control" => 4.0f,
                "Combo" => 3.5f,
                "Midrange" => 3.5f,
                "Sacrifice" => 3.0f,
                _ => 3.0f
            };
        }

        // ========== ULEPSZONY SCORING KART ==========
        private float ScoreCard(CardData card)
        {
            float rawValue = 0;

            if (card.Type == CardType.Unit)
            {
                float aggroWeight = Genes[STYLE_AGGRO] / 10f;
                float controlWeight = Genes[STYLE_CONTROL] / 10f;
                float midrangeWeight = Genes[STYLE_MIDRANGE] / 10f;

                rawValue += (card.Attack * 10f * aggroWeight);
                rawValue += (card.Health * 8f * controlWeight);

                float statBalance = Math.Abs(card.Attack - card.Health);
                rawValue -= (statBalance * 5f * midrangeWeight);

                if (card.Keywords != null)
                {
                    foreach (var kw in card.Keywords)
                    {
                        int param = 1;
                        if (card.KeywordParams != null && card.KeywordParams.TryGetValue(kw, out int pValue))
                            param = pValue;
                        rawValue += ScoreKeyword(kw.ToString(), param);
                    }
                }
            }
            else if (card.Type == CardType.Spell)
            {
                float spellAffinity = (Genes[STYLE_CONTROL] + Genes[STYLE_COMBO] + Genes[STYLE_BURN]) / 30f;
                rawValue += (30f * spellAffinity);
            }

            if (card.Effects != null)
            {
                foreach (var effect in card.Effects)
                    rawValue += ScoreEffect(effect);
            }

            // VALUE / COST (Wydajność) - Klucz do optymalizacji decku
            return rawValue / (card.Cost + 1);
        }


        private float ScoreKeyword(string keyword, int paramValue)
        {
            var keywordMapping = new Dictionary<string, int>
            {
                ["Armored"] = KEYWORD_ARMORED_EFFICIENCY,
                ["Flying"] = KEYWORD_FLYING_VALUE,
                ["SplashDamage"] = KEYWORD_SPLASH_PRIORITY,
                ["SoulGuard"] = KEYWORD_SOULGUARD_STICKINESS,
                ["Unkillable"] = KEYWORD_UNKILLABLE_RECURSION,
                ["BurnSource"] = KEYWORD_BURNSOURCE_AGGRESSION,
                ["Stunned"] = KEYWORD_STUN_DENIAL,
                ["Marked"] = KEYWORD_MARKED_FOCUS
            };

            if (keywordMapping.TryGetValue(keyword, out int geneIndex))
            {
                // DNA * Wartość Keywordu (np. Splash 6 daje 6x większy bonus niż Splash 1)
                return (Genes[geneIndex] * 4f) * paramValue;
            }

            return 5f * paramValue;
        }


        private float ScoreEffect(EffectData effect)
        {
            float score = 0;

            var triggerMapping = new Dictionary<TriggerType, int>
            {
                [TriggerType.OnDeath] = TRIGGER_DEATH_STRATEGY,
                [TriggerType.OnSacrificed] = TRIGGER_SACRIFICE_VISION,
                [TriggerType.OnPlayed] = TRIGGER_PLAY_STRATEGY,
                [TriggerType.OnKill] = TRIGGER_COMBAT_STRATEGY,
                [TriggerType.OnPreCombatLine] = TRIGGER_COMBAT_STRATEGY,
                [TriggerType.OnFriendlyUnitDied] = TRIGGER_DEATH_STRATEGY,
                [TriggerType.OnOtherUnitSacrificed] = TRIGGER_SACRIFICE_VISION,
                [TriggerType.OnStatusApplied] = TRIGGER_SETUP_RECOGNITION,
                [TriggerType.OnFriendlyActionPlayed] = TRIGGER_SETUP_RECOGNITION,
                [TriggerType.OnDamagedEnemyHero] = STYLE_AGGRO,
                [TriggerType.OnFriendlyCardDrawn] = LOGIC_HAND_SIZE_OPTIMIZATION
            };

            if (triggerMapping.TryGetValue(effect.Trigger, out int geneIndex))
                score += Genes[geneIndex] * 2.5f;
            if (effect.Actions != null)
            {
                foreach (var action in effect.Actions)
                {
                    score += EvaluateIndividualAction(action);
                }
            }

            return score;
        }
        private float ScoreStatusKeyword(Keyword? keyword)
        {
            if (keyword == null) return 0;

            return keyword switch
            {
                Keyword.Marked => Genes[KEYWORD_MARKED_FOCUS] * 4f,
                Keyword.Stunned => (11f - Genes[KEYWORD_STUN_DENIAL]) * 5f,
                Keyword.SoulGuard => Genes[KEYWORD_SOULGUARD_STICKINESS] * 4f,
                Keyword.Unkillable => Genes[KEYWORD_UNKILLABLE_RECURSION] * 5f,
                Keyword.Flying => Genes[KEYWORD_FLYING_VALUE] * 3f,
                Keyword.Burning => 20f,
                _ => 15f
            };
        }

        private float EvaluateIndividualAction(ActionData action)
        {
            float actionScore = 0;

            switch (action.Type)
            {
                case ActionType.DealDamage:
                    if (action.Target == TargetType.EnemyHero)
                        actionScore += Genes[TARGETING_FACE_VS_BOARD_BIAS] * 2.5f;
                    else if (action.Target == TargetType.AllEnemyUnits || action.Target == TargetType.AllUnitsOnBoard)
                        actionScore += action.Amount * Genes[TARGETING_AOE_SYMMETRY_TOLERANCE] * 4f;
                    else
                        actionScore += action.Amount * 3f; // Standardowy dmg w jednostkę
                    break;

                case ActionType.DrawCard:
                    actionScore += action.Amount * Genes[LOGIC_HAND_SIZE_OPTIMIZATION] * 3f;
                    break;

                case ActionType.DrawFromDiscard:
                    actionScore += action.Amount * Genes[LOGIC_DISCARD_PILE_RECYCLE] * 4f;
                    break;

                case ActionType.TutorCard:
                    actionScore += Genes[LOGIC_TUTOR_PRECISION] * 5f;
                    break;

                case ActionType.BuffStats:
                    // NIUANS: Rozróżnienie między buffem statystyk a modyfikacją kosztu
                    if (action.Amount < 0) // ZNIŻKI (np. Tea Maid, The Creature)
                    {
                        if (action.Target == TargetType.FriendlySpellsInHand || action.Target == TargetType.Self)
                            actionScore += Math.Abs(action.Amount) * Genes[LOGIC_STRATEGY_COST_REDUCTION] * 6f;
                    }
                    else if (action.Amount > 0) // PODATKI (np. Anti Matter DragonFriend)
                    {
                        if (action.Target == TargetType.EnemySpellsInHand)
                            actionScore += action.Amount * Genes[STYLE_CONTROL] * 4f; // Control kocha utrudniać życie
                    }
                    else // Standardowe +1/+1 (np. Radio Demon, Vane)
                    {
                        float statValue = (action.BuffAtk * 1.5f) + (action.BuffHp * 1.0f);
                        actionScore += statValue * 3f;
                    }
                    break;

                case ActionType.ApplyStatus:
                    actionScore += ScoreStatusKeyword(action.StatusKeyword);
                    break;

                case ActionType.Silence:
                    // Bardzo ważne dla kart typu "Silence" (Id: 31) i "White Mourning" (Id: 57)
                    actionScore += Genes[TARGETING_STATUS_PHILOSOPHY] * 5f;
                    if (action.Target == TargetType.AllUnitsOnBoard) actionScore *= 1.5f;
                    break;

                case ActionType.DestroyUnit:
                    // Obsługuje "Rocket Ignorance" (Id: 49) i "The Curtain Call" (Id: 53)
                    actionScore += Genes[TRIGGER_DESTRUCTION_PREFERENCE] * 6f;
                    if (action.Target == TargetType.AllEnemyUnits) actionScore *= 2f;
                    break;

                case ActionType.SacrificeUnit:
                    actionScore += Genes[TARGETING_FRIENDLY_SACRIFICE_VALUE] * 4f;
                    break;

                case ActionType.Heal:
                case ActionType.HealToFull:
                    // Obsługuje "Mokke" (Id: 1) i "FireAxe Man" (Id: 23)
                    actionScore += Genes[LIFE_HEAL_PRIORITY_HERO] * 3.5f;
                    break;

                case ActionType.SummonUnit:
                case ActionType.MakeAUnit:
                    // Obsługuje "Collector" (Id: 56) i "Evo Ghost" (Id: 43)
                    // Jeśli karta przywołuje konkretne ID (np. 904, 905), dajemy bonus za "Body"
                    actionScore += 25f;
                    if (action.StringParam == "AdjacentLanes") actionScore += 15f; // Bonus za szerokość boardu
                    break;

                case ActionType.AddCardToHand:
                    // Obsługuje "Bone Sommelier" (Id: 3) i "The Trapper" (Id: 51)
                    actionScore += 20f + Genes[LOGIC_REACTION_FROM_HAND_SENSE] * 2f;
                    break;

                case ActionType.BonusAttack:
                    // Kluczowe dla "Nepotism" (Id: 19) i "Gerard" (Id: 24)
                    actionScore += Genes[KEYWORD_BONUS_ATTACK_VALUE] * 5f;
                    break;

                case ActionType.AbsorbStats:
                    // Unikalne dla "Polar Bear" (Id: 10)
                    actionScore += 45f * (Genes[STYLE_MIDRANGE] / 5f);
                    break;

                case ActionType.MoveRight:
                case ActionType.MoveLeft:
                    actionScore += Genes[BOARD_MOVEMENT_VALUE_SENSE] * 4f;
                    break;

                case ActionType.ModifyGlobalBuff:
                    // Bardzo silne dla "Radio Demon" (Id: 6)
                    actionScore += (action.BuffAtk + action.BuffHp) * 40f;
                    break;

                case ActionType.GiveToOpponent:
                    // Kara/Bonus dla "Exploding Fruitcake" (Id: 28)
                    actionScore -= Genes[LOGIC_GIVING_RESOURCES_PENALTY] * 12f;
                    break;

                case ActionType.AddResource:
                    // Obsługuje "Widows Spider" (Id: 40) - daje krew/manę
                    actionScore += 35f * (Genes[LOGIC_MANA_CURVE_BIAS] / 5f);
                    break;

                case ActionType.ReturnToHand:
                    // Obsługuje "Genie" (Id: 20) i "Black Cat" (Id: 12)
                    actionScore += 25f * (Genes[LOGIC_REACTION_FROM_HAND_SENSE] / 5f);
                    break;

                case ActionType.ShuffleDeck:
                    // Obsługuje "Critical Thinking" (Id: 13) - zapobiega fatydze, odświeża deck
                    actionScore += 5f;
                    break;

                default:
                    actionScore += 10f;
                    break;
            }

            return actionScore;
        }

        // ========== POMOCNICZE ==========
        public float[] GetGenesArray() => Genes;

        public float[] GetBehaviorVector()
        {
            var importantGenes = new int[]
            {
                STYLE_AGGRO, STYLE_CONTROL, STYLE_COMBO, STYLE_MIDRANGE,
                STYLE_BURN, STYLE_TEMPO_PREFERENCE, LOGIC_MANA_CURVE_BIAS,
                TARGETING_FACE_VS_BOARD_BIAS, TRIGGER_SACRIFICE_VISION,
                TRIGGER_DEATH_STRATEGY, UNIT_TOKEN_VALUATION_LOGIC
            };

            return importantGenes.Select(i => Genes[i]).ToArray();
        }
    }

    internal class DeckAnalysis
    {
        public int cardCount { get; set; }
        public int totalCost { get; set; }
        public int uniqueCards { get; set; }
    }
}