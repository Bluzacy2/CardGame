using CardGame.Core.Cards.Models;
using CardGame.Core.State.Models;

namespace CardGame.Core.GameRules.Death.Prevention
{
    public interface IDeathPrevention
    {
        bool CanPreventDeath(CardInstance unit, GameState state);

        GameState PreventDeath(CardInstance unit, GameState currentState);
    }
}