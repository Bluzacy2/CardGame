using CardGame.Core.Application;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Models;
using CardGame.Core.GameRules.Damage;
using CardGame.Core.State.Models;

namespace CardGame.Core.Cards.Logic.Keywords
{
    public interface IKeywordHandler
    {
        Keyword Type { get; }
        int OnModifyDamageTaken(int amount, DamageContext context);
        GameState OnAfterAttack(GameState state, CardInstance attacker, CardInstance? victim, int lineIdx, GameContext context);
        GameState OnRoundEnd(GameState state, CardInstance unit, GameContext context);
        bool OnPreventDeath(ref GameState state, CardInstance unit, GameContext context);
    }
}