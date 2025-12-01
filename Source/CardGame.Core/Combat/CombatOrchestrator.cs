using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Collections.Generic;
using CardGame.Core.GameRules.Damage;
using CardGame.Core.GameRules.Death;
using CardGame.Core.State.Models;

namespace CardGame.Core.Combat
{
    public class CombatOrchestrator
    {
        private readonly DamageCalculator _damageCalculator;
        private readonly DeathResolver _deathResolver;

        public CombatOrchestrator()
        {
            _damageCalculator = new DamageCalculator();
            _deathResolver = new DeathResolver();
        }

        public GameState ResolveCombatPhase(GameState currentState)
        {
            /* Kopiujemy planszę, gdyż będziemy ją zmieniać. **Powiniśmy** działać na kopiach obiektu
             * co może spróbuje zaimplementować, ale Amerykanin stwierdził, że możemy także działać na
             * Instancjach kart, aby nie będzie działać immutability. BoardState podmieniamy na nowy po
             *  tym całym cyrku. - B. 13:57 */

            /* Haha żartowałem. Teraz działamy na liniach i instancjach kart, aby zrobić z tego Immutability 
             * Stan - B. 14:15. */

            var newLines = new List<Line>();

            foreach (var line in currentState.Board.Lines)
            {
                var u1 = line.Player1Unit;
                var u2 = line.Player2Unit;

                // Domyślne jednostki chyba, że się biją.
                var nextU1 = u1;
                var nextU2 = u2;

                if (u1 != null && u2 != null)
                {
                    var ctx1 = new DamageContext(u1, u2, u1.CurrentStats.Attack, DamageType.Combat);
                    int dmgTo2 = _damageCalculator.CalculateFinalDamage(ctx1);

                    var ctx2 = new DamageContext(u2, u1, u2.CurrentStats.Attack, DamageType.Combat);
                    int dmgTo1 = _damageCalculator.CalculateFinalDamage(ctx2);

                    /* ----------------------- Immutability Coded ----------------------- 
                     * zamiast odejmować HP. od unitów, odejmujemy od nowych obiektów .*/
                    nextU2 = u2.TakeDamage(dmgTo2);
                    nextU1 = u1.TakeDamage(dmgTo1);

                }
                newLines.Add(line.UpdateUnits(nextU1, nextU2));
            }
            var damagedBoard = new BoardState(newLines);
            var cleanBoard = _deathResolver.ResolveDeaths(damagedBoard, currentState.PlayerA, currentState.PlayerB);
            return currentState.With(board: cleanBoard);
        }
    }
}
