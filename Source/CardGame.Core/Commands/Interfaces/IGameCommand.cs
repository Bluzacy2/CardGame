using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using CardGame.Core.Events;
using CardGame.Core.State.Models;
namespace CardGame.Core.Commands.Interfaces
{
    public interface IGameCommand
    {
        // Właściwość PlayerId: Identyfikator gracza wykonującego komendę. Kluczowe dla walidacji i logiki gry.
        int PlayerId { get; }

        // Metoda Wykonaj: Przyjmuje stary stan gry i zwraca nowy stan gry po zastosowaniu komendy.
        GameState Execute(GameState currentState, EventBus eventBus);
    }
}
