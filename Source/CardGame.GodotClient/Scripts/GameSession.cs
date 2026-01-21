using Godot;
using CardGame.Core.Decks.Data;

/// <summary>
/// Singleton zarządzający globalnym stanem sesji gry między scenami.
/// Przechowuje informacje o wybranych taliach przed uruchomieniem właściwej rozgrywki.
/// </summary>
public partial class GameSession : Node
{
    /// <summary>
    /// Statyczna instancja singletona (Autoload).
    /// </summary>
    public static GameSession Instance { get; private set; }
    /// <summary>
    /// Talia wybrana przez gracza lokalnego.
    /// </summary>
    public DeckData SelectedPlayerDeck { get; set; }
    /// <summary>
    /// Talia wybrana dla przeciwnika (Bota).
    /// </summary>
    public DeckData SelectedBotDeck { get; set; }
    /// <summary>
    /// Inicjalizuje singleton po załadowaniu do drzewa sceny.
    /// </summary>
    public override void _Ready()
    {
        Instance = this;
    }
    /// <summary>
    /// Czyści zapisane dane sesji (np. po zakończeniu gry).
    /// </summary>
    public void Reset()
    {
        SelectedPlayerDeck = null;
        SelectedBotDeck = null;
    }
}