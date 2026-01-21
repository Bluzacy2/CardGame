using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;
using CardGame.Core.Application;
using CardGame.Core.AI;
using CardGame.Core.AI.Strategies;
using CardGame.Core.Commands.Interfaces;
using CardGame.Core.Commands.Implementations;

/// <summary>
/// Manages the AI's turn execution flow within the Godot scene tree.
/// Handles visual delays (for better UX), runs AI calculations on background threads to avoid freezing the UI,
/// and dispatches the chosen commands back to the main thread.
/// </summary>
public partial class BotCoordinator : Node
{
    #region Private Fields

    private GameEngine _engine;
    private AIPlayerController _aiController;
    private int _botId;

    #endregion

    #region Public Events & Properties

    /// <summary>
    /// Event triggered when the bot has finished thinking and wants to execute a command.
    /// The GameBootstrap should subscribe to this to forward the command to the GameEngine.
    /// </summary>
    public event Action<IGameCommand> OnBotCommand;

    /// <summary>
    /// Indicates whether the bot is currently processing a turn (waiting or calculating).
    /// Prevents multiple turn logic executions from overlapping.
    /// </summary>
    public bool IsThinking { get; private set; } = false;

    #endregion

    #region Initialization

    /// <summary>
    /// Initializes the coordinator with the required game logic dependencies.
    /// </summary>
    /// <param name="engine">The core GameEngine instance.</param>
    /// <param name="botId">The player ID assigned to this bot (usually 2).</param>
    /// <param name="aiController">The logic controller responsible for finding the best moves.</param>
    public void Initialize(GameEngine engine, int botId, AIPlayerController aiController)
    {
        _engine = engine;
        _botId = botId;
        _aiController = aiController;
    }

    #endregion

    #region Turn Logic

    /// <summary>
    /// Starts the bot's turn sequence.
    /// 1. Sets IsThinking to true.
    /// 2. Waits for a visual delay (simulating reaction time).
    /// 3. Runs the AI solver algorithm on a background thread.
    /// 4. Schedules the execution of the move on the main thread via CallDeferred.
    /// </summary>
    public async void PlayTurn()
    {
        if (IsThinking) return;
        IsThinking = true;

        // Simulate reaction time/thinking delay for better user experience
        await Task.Delay(800);

        // Run heavy calculations in the background to prevent UI freeze
        await Task.Run(() =>
        {
            var moves = _aiController.BeamSolver.FindBestMoves(_engine.CurrentState);

            // If no valid moves are found, default to ending the phase
            IGameCommand cmd = moves.Any() ? moves.First().Command : new EndPhaseCommand(_botId);

            // Execute on the main thread because Godot nodes are not thread-safe
            Callable.From(() => ExecuteBestMove(cmd)).CallDeferred();
        });
    }

    // Helper method for marshaling data (kept for potential future use or legacy compatibility)
    private void EmitCommand(Variant commandObj)
    {
        // Note: Direct interface passing via Variant can be tricky in Godot C#.
        // We use Callable.From closure in PlayTurn instead.
    }

    /// <summary>
    /// Executes the selected command on the main thread.
    /// Invokes the OnBotCommand event and resets the thinking flag.
    /// </summary>
    /// <param name="cmd">The command selected by the AI.</param>
    public void ExecuteBestMove(IGameCommand cmd)
    {
        OnBotCommand?.Invoke(cmd);

        // If the bot played EndPhase, it stops thinking.
        // If it played a card, IsThinking is reset here to allow the next loop tick to trigger PlayTurn again if needed.
        // (Assuming the bot plays one move per tick logic in GameBootstrap).
        IsThinking = false;
    }

    #endregion
}