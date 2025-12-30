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
        // --- KONFIGURACJA WAG ---
        private const float HealthWeight = 25.0f;
        private const float BoardWeight = 45.0f;
        private const float HandValueWeight = 20.0f;
        private const float EfficiencyWeight = 10.0f;
        private const float ThreatPenaltyWeight = 30.0f; // Zmniejszone z 50.0f

        public float Evaluate(GameState state, int botPlayerId)
        {
            var bot = state.GetPlayer(botPlayerId);
            var enemy = state.GetOpponent(botPlayerId);

            // 1. ABSOLUTNE PRIORYTETY
            if (enemy.Health <= 0 && bot.Health > 0) return 3000000f; // Lethal
            if (bot.Health <= 0) return -3000000f; // Pora¿ka
            if (bot.Health <= 0 && enemy.Health <= 0) return 0f; // Remis

            float score = 10000.0f;

            // 2. IDENTYFIKACJA STO£U I SYTUACJI
            var myUnits = state.Board.GetAllUnits().Where(u => u.OwnerPlayerId == botPlayerId).ToList();
            var enemyUnits = state.Board.GetAllUnits().Where(u => u.OwnerPlayerId != botPlayerId).ToList();

            bool hasGerard = myUnits.Any(u => u.Definition.Name.Contains("Gerard"));
            bool isSacStrat = bot.Hand.Any(IsSacrificeCard) || myUnits.Any(IsSacrificeCard);
            bool isMarkStrat = bot.Hand.Any(IsMarkCard) || myUnits.Any(IsMarkCard);

            // 3. EFEKTYWNOŒÆ ZASOBÓW
            // Karzemy za niewydan¹ krew, chyba ¿e mamy plan na combo
            score -= (bot.CurrentBlood * EfficiencyWeight);
            score += EvaluateRealizablePotential(state, botPlayerId, isSacStrat, isMarkStrat);

            // 4. ¯YCIE BOHATERA
            score += (bot.Health - enemy.Health) * HealthWeight;
            if (bot.Health < 12) score -= (15 - bot.Health) * 45.0f; // Panic Mode

            // 5. PLANSZA I KILL PRIORITY
            foreach (var unit in myUnits)
            {
                score += EvaluateFriendlyUnit(unit, state, hasGerard, isSacStrat, true) * BoardWeight;

                // Sprawdzamy, czy nie wystawiliœmy kluczowej jednostki na pewn¹ œmieræ
                if (IsHighValueTarget(unit)) score += EvaluateSafety(unit, state, botPlayerId);
            }

            foreach (var unit in enemyUnits)
            {
                // Kill Priority: Im groŸniejszy wróg, tym wiêksza "ulga" (bonus) gdy zginie w symulacji
                float threat = CalculateEnemyThreat(unit);

                // Jeœli jednostka ¿yje, odejmujemy punkty (zagro¿enie).
                // Jeœli zginê³a (HP<=0), threat wynosi 0, co oznacza zysk wzglêdem stanu, gdzie ona ¿yje.
                if (unit.CurrentStats.Health > 0)
                {
                    score -= threat * (BoardWeight * 1.3f);
                }
            }

            // 6. RÊKA (BEZ KARY ZA MA£¥ ILOŒÆ KART)
            // Bot ma graæ tym co ma. Oceniamy tylko jakoœæ kart w kontekœcie sto³u.
            foreach (var card in bot.Hand)
            {
                score += EvaluateCardInHand(card, enemyUnits, myUnits, bot.CurrentBlood) * HandValueWeight;
            }

            // 7. ANTI-LOAFING (Blokada pasywnoœci)
            // Jeœli bot ma manê i karty, a nic nie robi -> kara.
            if (state.ActivePlayerId == botPlayerId && bot.CurrentBlood > 0)
            {
                bool hasPlayable = bot.Hand.Any(c => c.CurrentStats.BloodCost <= bot.CurrentBlood);
                if (hasPlayable)
                {
                    score -= 400.0f;
                }
            }

            return Math.Clamp(score, -4000000f, 4000000f);
        }

        private float EvaluateFriendlyUnit(CardInstance u, GameState state, bool hasGerard, bool isSac, bool isFriendly)
        {
            var s = u.CurrentStats;
            float val = (s.Attack * 3.0f) + (s.Health * 2.0f);

            // --- FRIENDLY FIRE FIX ---
            if (s.Health <= 0)
            {
                if (IsFodder(u)) val += 40.0f; // Poœwiêcenie jest dobre
                else val -= 250.0f; // Strata wa¿nej jednostki to du¿y b³¹d
            }
            else if (u.DamageTaken > 0)
            {
                val -= (u.DamageTaken * 12.0f);
            }

            // --- STATUSY ---
            if (s.Keywords.Contains(Keyword.Marked)) val -= 40.0f;
            if (s.Keywords.Contains(Keyword.Unkillable)) val += 40.0f;
            if (s.Keywords.Contains(Keyword.SoulGuard)) val += 20.0f;

            // --- SPLASH POSITIONING (Z BalanceStrategy) ---
            if (s.Keywords.Contains(Keyword.SplashDamage))
            {
                // Bonus za œrodek planszy (linie 1 i 2), gdzie splash ma max zasiêg
                int lineIdx = GetLineIndex(state, u.InstanceId);
                if (lineIdx == 1 || lineIdx == 2) val += 15.0f;
            }

            // Bonus dla decku Sacrifice
            if (isSac && IsFodder(u)) val += 15.0f;

            return val;
        }

        private float CalculateEnemyThreat(CardInstance u)
        {
            // --- KILL PRIORITY SYSTEM ---
            var s = u.CurrentStats;
            float threat = (s.Attack * 3.5f) + (s.Health * 2.0f);

            // 1. ENGINE THREAT (Najwy¿szy priorytet - zabij to!)
            if (IsHighValueTarget(u)) threat += 200.0f;

            // 2. MARK UTILITY (Naprawa logiki Markowania)
            if (s.Keywords.Contains(Keyword.Marked))
            {
                // Marka na jednostce 1 HP jest bezu¿yteczna (chyba ¿e dla triggera)
                // Marka na jednostce > 3 HP jest bardzo cenna.
                if (s.Health <= 1) threat -= 10.0f; // Zmniejszamy priorytet, to "overkill"
                else threat += (s.Health * 10.0f);   // Zwiêkszamy priorytet, bo ³atwiej zabiæ
            }

            if (s.Keywords.Contains(Keyword.SplashDamage)) threat += 50.0f;

            return threat;
        }

        private float EvaluateSafety(CardInstance u, GameState state, int botId)
        {
            // Jeœli wystawiamy kluczow¹ jednostkê, a ona ma ma³o ¿ycia
            if (u.CurrentStats.Health <= 3)
            {
                var opp = state.GetOpponent(botId);
                // Kara, jeœli wróg ma zasoby na kontrê
                if (opp.Hand.Count > 0 && opp.MaxBlood >= 2)
                {
                    return -ThreatPenaltyWeight;
                }
            }
            return 0.0f;
        }

        private float EvaluateCardInHand(CardInstance card, List<CardInstance> enemyUnits, List<CardInstance> myUnits, int currentBlood)
        {
            float val = 1.0f;

            // Karta "grywalna" teraz jest cenniejsza (Tempo)
            if (card.CurrentStats.BloodCost <= currentBlood) val += 3.0f;

            // --- CONTEXTUAL SPELLS ---
            if (card.Definition.Type == CardType.Spell)
            {
                // Glock: Cenniejszy, gdy jest cel, który mo¿na zabiæ (Kill Priority)
                if (card.Definition.Id == "7")
                {
                    bool hasHighValueTarget = enemyUnits.Any(u => IsHighValueTarget(u) || (u.CurrentStats.Health <= 3 && u.CurrentStats.Attack >= 4));
                    val += hasHighValueTarget ? 8.0f : 0.0f;
                }

                // Buffs: Cenniejsze, gdy mamy stó³
                if (card.Definition.Effects.Any(e => e.Actions.Any(a => a.Type == ActionType.BuffStats)))
                {
                    val += (myUnits.Count > 0) ? 5.0f : -5.0f;
                }
            }

            // Jednostki s¹ cenniejsze przy pustym stole
            if (card.Definition.Type == CardType.Unit && myUnits.Count == 0) val += 10.0f;

            return val;
        }

        private float EvaluateRealizablePotential(GameState state, int botId, bool isSac, bool isMark)
        {
            var bot = state.GetPlayer(botId);
            float potential = 0;
            int nextTurnMana = bot.MaxBlood + 1;

            // Combo: Sacrifice
            bool hasBear = bot.Hand.Any(c => c.Definition.Id == "10");
            bool hasFuel = bot.Hand.Any(c => c.Definition.Id == "12");
            if (isSac && hasBear && hasFuel && nextTurnMana >= 3)
            {
                potential += 80.0f;
            }

            // Combo: Mark Execution (Gerard + Marki)
            // Jeœli mamy Gerarda, marki na stole wroga s¹ aktywem
            if (isMark && bot.Hand.Any(c => c.Definition.Id == "24"))
            {
                int markedEnemies = state.Board.GetAllUnits().Count(u => u.OwnerPlayerId != botId && u.CurrentStats.Keywords.Contains(Keyword.Marked));
                potential += (markedEnemies * 40.0f);
            }

            return potential;
        }

        // --- HELPERY ---
        private int GetLineIndex(GameState s, int unitId)
        {
            for (int i = 0; i < 4; i++)
                if (!s.Board.Lines[i].IsSlotEmpty(1) && s.Board.Lines[i].Player1Unit.InstanceId == unitId) return i;
                else if (!s.Board.Lines[i].IsSlotEmpty(2) && s.Board.Lines[i].Player2Unit.InstanceId == unitId) return i;
            return -1;
        }

        private bool IsHighValueTarget(CardInstance u) => new[] { "24", "5", "36", "14", "6", "23" }.Contains(u.Definition.Id);
        private bool IsFodder(CardInstance c) => new[] { "12", "3", "1", "900" }.Contains(c.Definition.Id);
        private bool IsSacrificeCard(CardInstance c) => new[] { "10", "12", "3", "9", "900", "1" }.Contains(c.Definition.Id);
        private bool IsMarkCard(CardInstance c) => new[] { "4", "32", "24", "25", "26" }.Contains(c.Definition.Id);
    }
}