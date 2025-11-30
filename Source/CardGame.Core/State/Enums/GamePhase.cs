using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CardGame.Core.State.Enums
{
    public enum GamePhase
    {
        None,       // Faza nieokreślona lub początkowa
        UnitOnly, // Faza grania Unitów
        UnitAndAction, // Faza grania Unitów i Akcji
        ActionOnly, // Faza grania tylko Akcji
        Combat, // Faza walki
        EndTurn    // Faza kończenia tury
    }
}
