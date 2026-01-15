using Godot;
using CardGame.Core.Decks.Data;

// Ten skrypt musi być ustawiony jako Autoload w ustawieniach projektu!
public partial class GameSession : Node
{
    public static GameSession Instance { get; private set; }

    // Wybrane talie do przekazania do sceny gry
    public DeckData SelectedPlayerDeck { get; set; }
    public DeckData SelectedBotDeck { get; set; }

    public override void _Ready()
    {
        Instance = this;
    }

    public void Reset()
    {
        SelectedPlayerDeck = null;
        SelectedBotDeck = null;
    }
}