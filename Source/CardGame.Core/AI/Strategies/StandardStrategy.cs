using CardGame.Core.AI.Interfaces;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Logic;
using CardGame.Core.Cards.Models;
using CardGame.Core.State.Enums;
using CardGame.Core.State.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CardGame.Core.AI.Strategies
{
    public class StandardStrategy : IAIStrategy
    {
        // Wagi bazowe
        private const float HealthWeight = 22.0f;
        private const float BoardWeight = 35.0f;
        private const float HandValueWeight = 18.0f;
        private const float EfficiencyWeight = 8.0f; // Obni¿ona, by pozwoliæ na oszczêdzanie
        private const float ThreatPenaltyWeight = 45.0f; // Kara za nara¿enie kluczowej jednostki

        public float Evaluate(GameState state, int botPlayerId)
        {
            var bot = state.GetPlayer(botPlayerId);
            var enemy = state.GetOpponent(botPlayerId);

            // 1. ABSOLUTNE PRIORYTETY (LETHAL / DEFEAT)
            if (enemy.Health <= 0) return 3000000f;
            if (bot.Health <= 0) return -3000000f;

            float score = 10000.0f;

            // 2. IDENTYFIKACJA STRATEGII
            bool isSacStrat = bot.Hand.Any(IsSacrificeCard) ||
                              state.Board.GetAllUnits().Any(u => u.OwnerPlayerId == botPlayerId && IsSacrificeCard(u));
            bool isMarkStrat = bot.Hand.Any(IsMarkCard) ||
                               state.Board.GetAllUnits().Any(u => u.OwnerPlayerId == botPlayerId && IsMarkCard(u));

            // 3. ZASOBY - Oszczêdzanie Blood ma sens, jeœli symulacja widzi przysz³e korzyœci
            float bloodPenalty = bot.CurrentBlood * EfficiencyWeight;
            float futurePotential = EvaluateFuturePotential(state, botPlayerId, isSacStrat);

            // Jeœli futurePotential jest wysoki (mamy combo), bloodPenalty zostanie zniwelowane
            score -= (bloodPenalty - futurePotential);

            // 4. ¯YCIE BOHATERA (Panic Mode poni¿ej 12 HP)
            score += (bot.Health - enemy.Health) * HealthWeight;
            if (bot.Health < 12) score -= (15 - bot.Health) * 35.0f;
            if (enemy.Health < 5) score += 200.0f; // Focus na wykoñczenie

            // 5. PLANSZA I ZAGRO¯ENIA (Threat Assessment)
            var myUnits = state.Board.GetAllUnits().Where(u => u.OwnerPlayerId == botPlayerId).ToList();
            var enemyUnits = state.Board.GetAllUnits().Where(u => u.OwnerPlayerId != botPlayerId).ToList();
            bool hasGerard = myUnits.Any(u => u.Definition.Name.Contains("Gerard"));

            foreach (var unit in myUnits)
            {
                // Ocena si³y jednostki
                score += EvaluateUnit(unit, state, botPlayerId, hasGerard, isSacStrat) * BoardWeight;
                // SNIPER LOGIC: Czy kluczowa jednostka zginie od razu?
                score += EvaluateThreats(unit, state, botPlayerId);
            }

            foreach (var unit in enemyUnits)
            {
                score -= EvaluateUnit(unit, state, botPlayerId, false, false) * (BoardWeight * 1.25f);
            }

            // 6. RÊKA - Karty to opcje, które kosztuj¹
            foreach (var card in bot.Hand)
            {
                score += EvaluateCardInHand(card, enemyUnits, bot.CurrentBlood, isMarkStrat, isSacStrat) * HandValueWeight;
            }

            // 7. BLOKADA PASYWNOŒCI (Wymuszanie gry, gdy nie ma planu oszczêdzania)
            if (state.ActivePlayerId == botPlayerId && bot.CurrentBlood > 0)
            {
                bool hasPlayable = bot.Hand.Any(c => c.CurrentStats.BloodCost <= bot.CurrentBlood);
                // Jeœli staæ nas na ruch, a nie mamy na rêce combo (futurePotential), pasowanie jest karane
                if (hasPlayable && futurePotential < 30.0f)
                    score -= 500.0f;
            }

            return score;
        }

        private float EvaluateUnit(CardInstance u, GameState state, int botId, bool hasGerard, bool isSac)
        {
            var s = u.CurrentStats;
            float val = (s.Attack * 2.8f) + (s.Health * 1.5f);

            // Synergie statusów
            if (s.Keywords.Contains(Keyword.Marked))
            {
                if (u.OwnerPlayerId == botId) val -= 55.0f; // Bycie oznaczonym to wyrok
                else val += hasGerard ? 75.0f : 25.0f; // Wróg z mark¹ to zasób
            }

            // Splash Damage i pozycjonowanie
            if (s.Keywords.Contains(Keyword.SplashDamage))
            {
                val += 15.0f; // Bazowy bonus za keyword
                // Solver w BotSolverze sprawdzi realne trafienia, tutaj dajemy wagê ogóln¹
            }

            if (s.Keywords.Contains(Keyword.Unkillable)) val += 35.0f;
            if (s.Keywords.Contains(Keyword.SoulGuard)) val += 20.0f;
            if (s.Keywords.Contains(Keyword.Stunned)) val *= 0.15f;

            // Specyficzne dla Sacrifice (Sommelier, Cat)
            if (isSac && (u.Definition.Id == "1" || u.Definition.Id == "12")) val += 10.0f;

            return val;
        }

        private float EvaluateThreats(CardInstance u, GameState state, int botId)
        {
            float penalty = 0;
            var opp = state.GetOpponent(botId);

            // Jeœli jednostka jest kluczowa dla silnika gry i ma ma³o HP (krucha)
            // Id 24: Gerard, Id 5: Queen of Cards, Id 36: Death
            bool isKeyUnit = (u.Definition.Id == "24" || u.Definition.Id == "5" || u.Definition.Id == "36");

            if (isKeyUnit && u.CurrentStats.Health <= 3)
            {
                // Sprawdzamy potencja³ przeciwnika (Deck Tracking)
                // Czy przeciwnik ma jeszcze Glocki (Id 7) w talii lub rêce?
                bool oppHasRemoval = opp.Hand.Count > 0 || opp.DrawPile.Any(c => c.Definition.Id == "7");

                if (oppHasRemoval)
                {
                    penalty -= ThreatPenaltyWeight; // Bot bêdzie ba³ siê wystawiæ Gerarda bez ochrony
                }
            }

            return penalty;
        }

        private float EvaluateFuturePotential(GameState state, int botId, bool isSac)
        {
            var p = state.GetPlayer(botId);
            float potential = 0;

            // Premiujemy trzymanie potê¿nego combo na rêce zamiast wyrzucania kart pojedynczo
            if (isSac)
            {
                bool hasBear = p.Hand.Any(c => c.Definition.Id == "10");
                bool hasCat = p.Hand.Any(c => c.Definition.Id == "12");
                if (hasBear && hasCat) potential += 60.0f; // Pozwala botowi oszczêdziæ krew na turê z combo
            }

            return potential;
        }

        private float EvaluateCardInHand(CardInstance card, List<CardInstance> enemyUnits, int currentBlood, bool isMark, bool isSac)
        {
            float val = 1.0f;

            // Skalowanie wartoœci wzglêdem aktualnej many
            if (card.CurrentStats.BloodCost <= currentBlood) val += 1.5f;

            // Synergie archetypów
            if (isMark && IsMarkCard(card)) val += 2.0f;
            if (isSac && IsSacrificeCard(card)) val += 2.0f;

            // Wartoœæ czarów reaktywnych
            if (card.Definition.Type == CardType.Spell)
            {
                if (card.Definition.Id == "7" && enemyUnits.Any(u => u.CurrentStats.Health <= 3)) val += 3.0f;
                if (card.Definition.Id == "31" && enemyUnits.Any(u => u.CurrentStats.Attack > 4)) val += 4.0f; // Silence
            }

            return val;
        }

        private bool IsMarkCard(CardInstance c) => new[] { "4", "32", "24", "25", "26" }.Contains(c.Definition.Id);
        private bool IsSacrificeCard(CardInstance c) => new[] { "10", "12", "3", "9", "900", "1" }.Contains(c.Definition.Id);
    }
}