using Xunit;
using CardGame.Core.Application;
using CardGame.Core.State.Models;
using CardGame.Core.State.Enums;
using CardGame.Core.Commands.Implementations;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Models;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CardGame.Tests
{
    public class UnkillableSpellFix
    {
        private void SetupMockLibrary(CardData unit, CardData spell)
        {
            var library = CardLibrary.Instance;
            library.Clear();

            // Używamy refleksji, aby dostać się do prywatnego słownika _cards w CardLibrary
            var field = typeof(CardLibrary).GetField("_cards", BindingFlags.NonPublic | BindingFlags.Instance);
            var cardsDict = (Dictionary<int, CardData>)field.GetValue(library);

            cardsDict[unit.Id] = unit;
            cardsDict[spell.Id] = spell;
        }

        [Fact]
        public void UnkillableUnit_ShouldReturnToHand_WhenDestroyedBySpell()
        {
            // 1. PRZYGOTOWANIE DANYCH KART
            var skeletonData = new CardData
            {
                Id = 100,
                Name = "Skeleton",
                Type = CardType.Unit,
                Attack = 1,
                Health = 1,
                Cost = 1,
                Keywords = new List<Keyword> { Keyword.Unkillable }
            };

            var spellData = new CardData
            {
                Id = 200,
                Name = "Direct Damage",
                Type = CardType.Spell,
                Cost = 1,
                Effects = new List<EffectData>
                {
                    new EffectData
                    {
                        Trigger = TriggerType.OnPlayed,
                        Targeting = TargetType.TargetEnemyUnit, // Ważne dla PlaySpellCommand
                        Actions = new List<ActionData>
                        {
                            new ActionData { Type = ActionType.DealDamage, Amount = 10 }
                        }
                    }
                }
            };

            SetupMockLibrary(skeletonData, spellData);

            // 2. SETUP SILNIKA
            var rng = new DeterministicRng(1);
            var library = CardLibrary.Instance;

            // Tworzymy talie - każda ma po kilka kopii, by GameState.Initial mógł dobrać karty
            var deckA = Enumerable.Range(0, 10).Select(_ => new CardInstance(rng.NextId(), 1, library.CreateDefinition(100))).ToList();
            var deckB = Enumerable.Range(0, 10).Select(_ => new CardInstance(rng.NextId(), 2, library.CreateDefinition(200))).ToList();

            // Startujemy w fazie Mulligan
            var state = GameState.Initial(1, deckA, deckB, rng);
            var engine = new GameEngine(state, rng.Seed);

            // 3. PRZEJŚCIE PRZEZ FAZY
            // Mulligan -> UnitOnly
            engine.ExecuteCommand(new ConfirmMulliganCommand(1, new List<int>()));
            engine.ExecuteCommand(new ConfirmMulliganCommand(2, new List<int>()));

            Assert.Equal(GamePhase.UnitOnly, engine.CurrentState.CurrentPhase);

            // Tura Gracza 1: Zagranie Skeletona
            var skeleton = engine.CurrentState.PlayerA.Hand.First(c => c.Definition.Id == "100");
            engine.ExecuteCommand(new PlayUnitCommand(1, skeleton.InstanceId, 0));

            // Koniec fazy UnitOnly Gracza 1 -> Przejście do UnitAndAction Gracza 2
            engine.ExecuteCommand(new EndPhaseCommand(1));
            Assert.Equal(2, engine.CurrentState.ActivePlayerId);
            Assert.Equal(GamePhase.UnitAndAction, engine.CurrentState.CurrentPhase);

            // 4. WYKONANIE BŁĘDNEJ AKCJI (Czar niszczący)
            var targetSkeleton = engine.CurrentState.Board.Lines[0].Player1Unit;
            var damageSpell = engine.CurrentState.PlayerB.Hand.First(c => c.Definition.Id == "200");

            Assert.NotNull(targetSkeleton);

            // Gracz 2 rzuca czar w Skeletona Gracza 1
            engine.ExecuteCommand(new PlaySpellCommand(2, damageSpell.InstanceId, targetSkeleton.InstanceId));

            // 5. ASERCJA - SPRAWDZENIE CZY UNIT WRÓCIŁ DO RĘKI
            var finalBoard = engine.CurrentState.Board;
            var player1Hand = engine.CurrentState.PlayerA.Hand;
            var player1Discard = engine.CurrentState.PlayerA.DiscardPile;

            // Czy zniknął z planszy?
            Assert.Null(finalBoard.Lines[0].Player1Unit);

            // Czy NIE ma go w discardzie? (Tu był błąd)
            Assert.DoesNotContain(player1Discard, c => c.Definition.Id == "100");

            // Czy wrócił do ręki?
            Assert.Contains(player1Hand, c => c.Definition.Id == "100");

            // Czy został zresetowany? (HP powinno być pełne)
            var returnedUnit = player1Hand.First(c => c.Definition.Id == "100");
            Assert.Equal(0, returnedUnit.DamageTaken);
        }
    }
}