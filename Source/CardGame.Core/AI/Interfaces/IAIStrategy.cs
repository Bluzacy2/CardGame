using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CardGame.Core.State.Models;

namespace CardGame.Core.AI.Interfaces
{
    public interface IAIStrategy
    {
        float Evaluate(GameState state, int botPlayerId);
    }
}
