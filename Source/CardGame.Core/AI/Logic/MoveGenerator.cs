using System.Collections.Generic;
using System.Linq;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Logic;
using CardGame.Core.Commands.Implementations;
using CardGame.Core.Commands.Interfaces;
using CardGame.Core.State.Enums;
using CardGame.Core.State.Models;

namespace CardGame.Core.AI.Logic
{
    public class MoveGenerator
    {
        public List<IGameCommand> GenerateLegalMoves(GameState state, int playerId)
        {
            var moves = new List<IGameCommand>();
            var player = state.GetPlayer(playerId);

            if (state.CurrentPhase == GamePhase.Mulligan)
            {
                if (state.PlayersReady.Contains(playerId)) return moves;

                var hand = player.Hand;
                int count = hand.Count;
                for (int i = 0; i < (1 << count); i++)
                {
                    var rejectedIds = new List<int>();
                    for (int j = 0; j < count; j++)
                    {
                        if ((i & (1 << j)) != 0) rejectedIds.Add(hand[j].InstanceId);
                    }
                    moves.Add(new ConfirmMulliganCommand(playerId, rejectedIds));
                }
                return moves;
            }

            if (state.PendingInteraction != null)
            {
                var pending = state.PendingInteraction;
                if (pending.RequiredTargetType == TargetType.EmptyLane)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        if (state.Board.Lines[i].IsSlotEmpty(playerId))
                            moves.Add(new SelectTargetCommand(playerId, i));
                    }
                    return moves;
                }

                if (pending.RequiredTargetType == TargetType.Choice)
                {
                    for (int i = 0; i < pending.Options.Count; i++)
                        moves.Add(new SelectTargetCommand(playerId, i));
                }
                else
                {
                    var targets = EffectTargetResolver.GetPotentialTargets(pending.RequiredTargetType, state, pending.SourceCardInstanceId);
                    foreach (var t in targets)
                        moves.Add(new SelectTargetCommand(playerId, t.InstanceId));
                }
                return moves;
            }

            if (state.CurrentPhase == GamePhase.UnitOnly || state.CurrentPhase == GamePhase.UnitAndAction)
            {
                foreach (var card in player.Hand.Where(c => c.Definition.Type == CardType.Unit))
                {
                    if (PlayValidator.CanPlay(card, state, playerId))
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            if (state.Board.Lines[i].IsSlotEmpty(playerId))
                                moves.Add(new PlayUnitCommand(playerId, card.InstanceId, i));
                        }
                    }
                }
            }

            if (state.CurrentPhase == GamePhase.ActionOnly || state.CurrentPhase == GamePhase.UnitAndAction)
            {
                foreach (var card in player.Hand.Where(c => c.Definition.Type == CardType.Spell))
                {
                    if (PlayValidator.CanPlay(card, state, playerId))
                        moves.Add(new PlaySpellCommand(playerId, card.InstanceId));
                }
            }

            moves.Add(new EndPhaseCommand(playerId));
            return moves;
        }
    }
}