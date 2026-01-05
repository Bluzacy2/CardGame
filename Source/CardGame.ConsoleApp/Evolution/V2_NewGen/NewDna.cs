using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Models;
using System;
using System.Collections.Generic;
using System.Linq;

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
                childGenes[i] = i < crossoverPoint ? parent1.Genes[i] : parent2.Genes[i];

                if (rng.NextDouble() < 0.3)
                {
                    float perturbation = (float)(rng.NextDouble() * 0.5 - 0.25);
                    childGenes[i] = Math.Clamp(childGenes[i] + perturbation, 0, 10);
                }
            }

            return new NewDna(childGenes);
        }

        public void Mutate(Random rng, int stagnationCounter)
        {
            float mutationRate = Math.Min(0.3f, 0.05f + stagnationCounter * 0.01f);

            for (int i = 0; i < TOTAL_GENES; i++)
            {
                if (rng.NextDouble() < mutationRate)
                {
                    float roll = (float)rng.NextDouble();

                    if (roll < 0.3f)
                        Genes[i] = Math.Clamp(Genes[i] + (float)(rng.NextDouble() * 2 - 1), 0, 10);
                    else if (roll < 0.6f)
                        Genes[i] = (float)(rng.NextDouble() * 10);
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
        public int[] BuildDeck(int[] cardPool, Random rng)
        {
            const int DECK_SIZE = 30;
            const int MAX_COPIES_PER_CARD = 3;

            var deck = new List<int>();
            var cardLibrary = CardLibrary.Instance;

            // Krok 1: Oceń wszystkie karty (bez szumu na początku)
            var scoredCards = new Dictionary<int, (float baseScore, CardData data)>();
            foreach (int cardId in cardPool)
            {
                try
                {
                    var cardData = cardLibrary.GetCard(cardId);
                    float baseScore = ScoreCard(cardData);
                    scoredCards[cardId] = (baseScore, cardData);
                }
                catch { }
            }

            // Krok 2: ITERACYJNE DODAWANIE KART Z UWZGLĘDNIENIEM JUŻ WYBRANYCH
            var counts = new Dictionary<int, int>();
            var deckCardData = new List<CardData>();

            for (int i = 0; i < DECK_SIZE; i++)
            {
                // Oblicz aktualne potrzeby decku (curve, synergy)
                var deckAnalysis = AnalyzeCurrentDeck(deckCardData);

                // Wybierz najlepszą kartę uwzględniając już wybrane
                var bestChoice = SelectBestCardForDeck(scoredCards, counts, deckCardData, deckAnalysis, rng);

                if (bestChoice.cardId > 0)
                {
                    deck.Add(bestChoice.cardId);
                    counts[bestChoice.cardId] = counts.GetValueOrDefault(bestChoice.cardId, 0) + 1;
                    deckCardData.Add(scoredCards[bestChoice.cardId].data);
                }
                else
                {
                    // Dodaj losową kartę jeśli nie znaleziono dobrej
                    int randomCardId = cardPool[rng.Next(cardPool.Length)];
                    if (!counts.ContainsKey(randomCardId) || counts[randomCardId] < MAX_COPIES_PER_CARD)
                    {
                        deck.Add(randomCardId);
                        counts[randomCardId] = counts.GetValueOrDefault(randomCardId, 0) + 1;
                        deckCardData.Add(scoredCards[randomCardId].data);
                    }
                }
            }

            // Krok 3: Wymieszaj talie
            deck = deck.OrderBy(x => rng.Next()).ToList();

            return deck.ToArray();
        }

        private (int cardId, float score) SelectBestCardForDeck(
            Dictionary<int, (float baseScore, CardData data)> scoredCards,
            Dictionary<int, int> counts,
            List<CardData> currentDeck,
            DeckAnalysis analysis,
            Random rng)
        {
            const int MAX_COPIES = 3;
            var candidates = new List<(int cardId, float score)>();

            foreach (var kvp in scoredCards)
            {
                int cardId = kvp.Key;
                var cardData = kvp.Value.data;
                float baseScore = kvp.Value.baseScore;

                // Sprawdź limit kopii
                int currentCopies = counts.GetValueOrDefault(cardId, 0);
                if (currentCopies >= MAX_COPIES)
                    continue;

                // Oblicz wagę dla tej kopii (zależna od archetypu i już posiadanych kopii)
                float copyWeight = CalculateCopyWeight(currentCopies + 1);

                // Oceniaj kartę w kontekście całego decku
                float contextualScore = baseScore * copyWeight;

                // Bonus/kara za curve
                float curveAdjustment = CalculateCurveAdjustment(cardData, analysis);
                contextualScore += curveAdjustment;

                // Bonus za synergię z już wybranymi kartami
                float synergyBonus = CalculateSynergyBonus(cardData, currentDeck);
                contextualScore += synergyBonus;

                // Bonus za spójność archetypu
                float archetypeBonus = CalculateArchetypeBonus(cardData);
                contextualScore += archetypeBonus;

                // Dodaj losowość (±15%)
                float noise = 1.0f + (float)(rng.NextDouble() * 0.3 - 0.15);
                contextualScore *= noise;

                candidates.Add((cardId, contextualScore));
            }

            if (!candidates.Any())
                return (0, 0);

            // Wybierz najlepszą kartę (z losowością dla top 3)
            var topCandidates = candidates.OrderByDescending(c => c.score).Take(3).ToList();
            return topCandidates[rng.Next(topCandidates.Count)];
        }

        private float CalculateCopyWeight(int copyNumber)
        {
            string archetype = ClassifyArchetype();

            // Aggro chce więcej kopii, Control mniej
            return archetype switch
            {
                "Aggro" => copyNumber == 1 ? 1.0f : (copyNumber == 2 ? 0.85f : 0.7f),
                "Control" => copyNumber == 1 ? 1.0f : (copyNumber == 2 ? 0.7f : 0.4f),
                "Combo" => copyNumber == 1 ? 1.0f : (copyNumber == 2 ? 0.9f : 0.8f),
                "Midrange" => copyNumber == 1 ? 1.0f : (copyNumber == 2 ? 0.8f : 0.6f),
                "Sacrifice" => copyNumber == 1 ? 1.0f : (copyNumber == 2 ? 0.75f : 0.5f),
                _ => copyNumber == 1 ? 1.0f : (copyNumber == 2 ? 0.7f : 0.4f)
            };
        }

        private float CalculateCurveAdjustment(CardData card, DeckAnalysis analysis)
        {
            string archetype = ClassifyArchetype();
            float idealAvgCost = GetIdealAverageCost(archetype);

            // Przewidywany średni koszt po dodaniu tej karty
            float predictedAvgCost = (analysis.totalCost + card.Cost) / (analysis.cardCount + 1);
            float deviation = Math.Abs(predictedAvgCost - idealAvgCost);

            // Kara za zaburzanie curve
            float penalty = -deviation * 3.0f;

            // Dodatkowa kara jeśli deck ma już zły curve
            if (analysis.cardCount >= 15 && deviation > 1.0f)
                penalty *= 2.0f;

            return penalty;
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
                    bonus += 30.0f * Genes[KEYWORD_MARKED_FOCUS] * 0.1f;

                // Sacrifice synergy
                bool isSacrificeActivator = card.Effects?.Any(e =>
                    e.Actions?.Any(a => a.Type == ActionType.SacrificeUnit || a.Type == ActionType.DestroyUnit) == true) == true;
                bool hasSacrificePayoff = deckCard.Effects?.Any(e =>
                    e.Trigger == TriggerType.OnSacrificed || e.Trigger == TriggerType.OnOtherUnitSacrificed) == true;

                if ((isSacrificeActivator && hasSacrificePayoff) || (hasSacrificePayoff && isSacrificeActivator))
                    bonus += 40.0f * Genes[TRIGGER_SACRIFICE_VISION] * 0.1f;

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
            float score = 0;

            // KOSZT - użyj łagodniejszej funkcji
            float costPref = Genes[LOGIC_MANA_CURVE_BIAS] / 10.0f;
            float idealCost = 10.0f * (1.0f - costPref);
            float costDiff = Math.Abs(card.Cost - idealCost);
            // Używamy funkcji kwadratowej (x² * 0.5) zamiast liniowej (x * 5.0)
            score -= costDiff * costDiff * 0.5f;

            if (card.Type == CardType.Unit)
            {
                // STATYSTYKI zależne od archetypu
                // Aggro ceni atak, Control ceni health
                float aggroValue = card.Attack * (1.0f + (5 - Math.Min(card.Cost, 5)) * 0.2f);
                float controlValue = card.Health * (1.0f + Math.Min(card.Cost, 5) * 0.1f);

                score += aggroValue * Genes[STYLE_AGGRO] * 0.25f;
                score += controlValue * Genes[STYLE_CONTROL] * 0.25f;

                // Midrange ceni zrównoważone statystyki
                float statBalance = Math.Abs(card.Attack - card.Health);
                score -= statBalance * Genes[STYLE_MIDRANGE] * 0.15f;

                // KEYWORDS
                if (card.Keywords != null)
                {
                    foreach (var keyword in card.Keywords)
                        score += ScoreKeyword(keyword.ToString());
                }
            }
            else if (card.Type == CardType.Spell)
            {
                // Spelle - Control i Combo je cenią
                score += Genes[STYLE_CONTROL] * 2.5f;
                score += Genes[STYLE_COMBO] * 3.0f;
            }

            // EFFECTS - oceniamy wszystkie efekty
            if (card.Effects != null)
            {
                foreach (var effect in card.Effects)
                    score += ScoreEffect(effect);
            }

            return score;
        }

        private float ScoreKeyword(string keyword)
        {
            var keywordMapping = new Dictionary<string, int>
            {
                ["Armored"] = KEYWORD_ARMORED_EFFICIENCY,
                ["Flying"] = KEYWORD_FLYING_VALUE,
                ["SplashDamage"] = KEYWORD_SPLASH_PRIORITY,
                ["SoulGuard"] = KEYWORD_SOULGUARD_STICKINESS,
                ["Unkillable"] = KEYWORD_UNKILLABLE_RECURSION,
                ["BurnSource"] = KEYWORD_BURNSOURCE_AGGRESSION,
                ["Stunned"] = KEYWORD_STUN_DENIAL
            };

            if (keywordMapping.TryGetValue(keyword, out int geneIndex))
                return Genes[geneIndex] * 1.5f; // Zmniejszone z 2.0f

            return 0;
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
                [TriggerType.OnOtherUnitSacrificed] = TRIGGER_SACRIFICE_VISION
            };

            if (triggerMapping.TryGetValue(effect.Trigger, out int geneIndex))
                score += Genes[geneIndex] * 2.0f; // Zmniejszone z 3.0f

            if (effect.Actions != null)
            {
                foreach (var action in effect.Actions)
                {
                    if (action.Type == ActionType.DealDamage && action.Target == TargetType.EnemyHero)
                        score += Genes[TARGETING_FACE_VS_BOARD_BIAS] * 1.5f; // Zmniejszone
                    else if (action.Type == ActionType.DrawCard)
                        score += Genes[LOGIC_HAND_SIZE_OPTIMIZATION] * 1.5f;
                    else if (action.Type == ActionType.SacrificeUnit)
                        score += Genes[TARGETING_FRIENDLY_SACRIFICE_VALUE] * 2.0f;
                    else if (action.Type == ActionType.TutorCard)
                        score += Genes[LOGIC_TUTOR_PRECISION] * 2.5f;
                    else if (action.Type == ActionType.BuffStats)
                        score += (action.BuffAtk + action.BuffHp) * 3.0f;
                }
            }

            return score;
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