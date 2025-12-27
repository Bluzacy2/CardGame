using System.Collections.Generic;
using CardGame.Core.Cards.Data;
using CardGame.Core.Commands.Implementations;
using CardGame.Core.Commands.Interfaces;
using CardGame.Core.State.Enums;
using CardGame.Core.State.Models;

namespace CardGame.Core.AI.Logic
{
    internal class MoveGenerator
    {
        public List<IGameCommand> GenerateLegalMoves(GameState state, int playerId)
        {
            var moves = new List<IGameCommand>();
            var player = state.GetPlayer(playerId);

            if (state.CurrentPhase == GamePhase.Mulligan)
            {
                moves.Add(new ConfirmMulliganCommand(playerId, new List<int>()));
                return moves;
            }

            if (state.PendingInteraction != null)
            {
                var allUnits = state.Board.GetAllUnits();
                foreach (var unit in allUnits)
                {
                    moves.Add(new SelectTargetCommand(playerId, unit.InstanceId));
                }
                return moves;
            }

            // Zagrywanie Jednostek - używamy BloodCost
            if (state.CurrentPhase == GamePhase.UnitOnly || state.CurrentPhase == GamePhase.UnitAndAction)
            {
                foreach (var card in player.Hand)
                {
                    if (card.Definition.Type == CardType.Unit && player.CanPlayCard(card.CurrentStats.BloodCost))
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            if (state.Board.Lines[i].IsSlotEmpty(playerId))
                            {
                                moves.Add(new PlayUnitCommand(playerId, card.InstanceId, i));
                            }
                        }
                    }
                }
            }

            // Zagrywanie Czarów - używamy BloodCost
            if (state.CurrentPhase == GamePhase.ActionOnly || state.CurrentPhase == GamePhase.UnitAndAction)
            {
                foreach (var card in player.Hand)
                {
                    if (card.Definition.Type == CardType.Spell && player.CanPlayCard(card.CurrentStats.BloodCost))
                    {
                        moves.Add(new PlaySpellCommand(playerId, card.InstanceId));
                    }
                }
            }

            if (moves.Count == 0 || !moves.Exists(m => m is EndPhaseCommand))
            {
                moves.Add(new EndPhaseCommand(playerId));
            }

            return moves;
        }
    }
}