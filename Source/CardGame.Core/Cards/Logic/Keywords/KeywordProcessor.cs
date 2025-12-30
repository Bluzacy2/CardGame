using System.Collections.Generic;
using CardGame.Core.Application;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Models;
using CardGame.Core.GameRules.Damage;
using CardGame.Core.State.Models;

namespace CardGame.Core.Cards.Logic.Keywords
{
    public class KeywordProcessor
    {
        private readonly Dictionary<Keyword, IKeywordHandler> _handlers = new();

        public void RegisterHandler(IKeywordHandler handler) => _handlers[handler.Type] = handler;

        public int ProcessDamageTaken(int amount, DamageContext context)
        {
            int finalAmount = amount;
            foreach (var keyword in context.Target.CurrentStats.Keywords)
            {
                if (_handlers.TryGetValue(keyword, out var handler))
                    finalAmount = handler.OnModifyDamageTaken(finalAmount, context);
            }
            return finalAmount;
        }

        public GameState ProcessPostAttack(GameState state, CardInstance attacker, CardInstance? victim, int lineIdx, GameContext context)
        {
            var workingState = state;
            foreach (var keyword in attacker.CurrentStats.Keywords)
            {
                if (_handlers.TryGetValue(keyword, out var handler))
                    workingState = handler.OnAfterAttack(workingState, attacker, victim, lineIdx, context);
            }
            return workingState;
        }

        public GameState ProcessRoundEnd(GameState state, CardInstance unit, GameContext context)
        {
            var workingState = state;
            foreach (var keyword in unit.CurrentStats.Keywords)
            {
                if (_handlers.TryGetValue(keyword, out var handler))
                    workingState = handler.OnRoundEnd(workingState, unit, context);
            }
            return workingState;
        }

        public bool TryPreventDeath(ref GameState state, CardInstance unit, GameContext context, bool isSacrifice)
        {
            foreach (var keyword in unit.CurrentStats.Keywords)
            {
                if (_handlers.TryGetValue(keyword, out var handler))
                {
                    if (handler.OnPreventDeath(ref state, unit, context, isSacrifice))
                        return true;
                }
            }
            return false;
        }
    }
}