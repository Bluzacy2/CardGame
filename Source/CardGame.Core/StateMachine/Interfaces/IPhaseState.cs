using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using CardGame.Core.Events;
using CardGame.Core.Commands.Interfaces;
using CardGame.Core.State.Enums;
using CardGame.Core.State.Models;

namespace CardGame.Core.StateMachine.Interfaces
{
    public interface IPhaseState
    {


        GamePhase PhaseType { get; }
        bool IsCommandAllowed(IGameCommand command, GameState state);

        GameState ProcessEndPhase(GameState currentState, EventBus eventBus);

        bool ShouldEndPhaseAutomatically(GameState state);
    }
}
