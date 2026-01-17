using CardGame.Core.Events.Interfaces;
using CardGame.Core.State.Models;

public class InteractionRequiredEvent : IGameEvent
{
    public PendingInteraction Interaction { get; }
    public int SourcePlayerId => 0;

    public InteractionRequiredEvent(PendingInteraction interaction)
    {
        Interaction = interaction;
    }
}