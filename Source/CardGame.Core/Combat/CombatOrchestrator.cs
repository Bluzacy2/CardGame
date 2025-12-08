using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using CardGame.Core.Events;
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

        public GameState ResolveCombatPhase(GameState currentState, EventBus eventBus)
        {
            var newLines = new List<Line>();
            var tempPlayerA = currentState.PlayerA;
            var tempPlayerB = currentState.PlayerB;

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

                    
                    nextU2 = u2.TakeDamage(dmgTo2);
                    nextU1 = u1.TakeDamage(dmgTo1);

                }
                else if (u1 != null && u2 == null)
                {
                    int dmg = u1.CurrentStats.Attack;
                    if (dmg > 0)
                    {
                        Console.WriteLine($"[WALKA] {u1.Definition.Name} (L{line.Index}) uderza wroga bezpośrednio za {dmg}!");
                        tempPlayerB = tempPlayerB.WithDamageTaken(dmg);
                    
                    }
                }
                
                else if (u2 != null && u1 == null)
                {
                    int dmg = u2.CurrentStats.Attack;
                    if (dmg > 0)
                    {
                        Console.WriteLine($"[WALKA] {u2.Definition.Name} (L{line.Index}) uderza wroga bezpośrednio za {dmg}!");
                        tempPlayerA = tempPlayerA.WithDamageTaken(dmg);
                    }
                }
                newLines.Add(line.UpdateUnits(nextU1, nextU2));
            }
            var damagedBoard = new BoardState(newLines);
            var stateWithDamage = currentState.With(board: damagedBoard, playerA:tempPlayerA, playerB: tempPlayerB);
            var finalState = _deathResolver.ResolveDeaths(stateWithDamage, eventBus);

            return finalState;

        }
    }
}
