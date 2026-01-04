using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;
using CardGame.Core.Application;
using CardGame.Core.AI;
using CardGame.Core.AI.Strategies;
using CardGame.Core.Commands.Interfaces;
using CardGame.Core.Commands.Implementations;

public partial class BotCoordinator : Node
{
    private GameEngine _engine;
    private AIPlayerController _aiController;
    private int _botId;

    // Zdarzenie, gdy bot chce wykonać komendę (Bootstrap przekaże to do silnika)
    public event Action<IGameCommand> OnBotCommand;

    public bool IsThinking { get; private set; } = false;

    public void Initialize(GameEngine engine, int botId, AIPlayerController aiController)
    {
        _engine = engine;
        _botId = botId;
        _aiController = aiController;
    }

    public async void PlayTurn()
    {
        if (IsThinking) return;
        IsThinking = true;

        // Symulacja czasu reakcji
        await Task.Delay(800);

        // Obliczenia w tle (nie blokują UI)
        await Task.Run(() =>
        {
            var moves = _aiController.BeamSolver.FindBestMoves(_engine.CurrentState);
            IGameCommand cmd = moves.Any() ? moves.First().Command : new EndPhaseCommand(_botId);

            // Wywołanie na głównym wątku
            Callable.From(() => ExecuteBestMove(cmd)).CallDeferred();
        });
    }

    // Metoda wywoływana przez CallDeferred (musi przyjmować Variant lub być wrapperem)
    // Ale my użyjemy prostszego triku z przekazaniem obiektu C#
    private void EmitCommand(Variant commandObj)
    {
        // Uwaga: Godot Marshalling może być tricky dla interfejsów.
        // Bezpieczniej użyć Callable.From w Task.Run (jak mieliśmy wcześniej).
        // Ale dla czystości klasy, zróbmy publiczną metodę, którą wywoła Callable.
    }

    // Wersja bezpieczna dla Task.Run + Callable
    public void ExecuteBestMove(IGameCommand cmd)
    {
        OnBotCommand?.Invoke(cmd);

        // Jeśli bot zagrał EndPhase, kończy myślenie.
        // Jeśli zagrał kartę, IsThinking pozostaje true (lub false, jeśli chcesz pozwolić mu grać seriami).
        // W naszej logice bot gra jeden ruch na "tik" pętli gry, więc resetujemy flagę.
        IsThinking = false;
    }
}