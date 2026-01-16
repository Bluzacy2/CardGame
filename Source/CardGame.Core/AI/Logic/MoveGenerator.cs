using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Logic;
using CardGame.Core.Commands.Implementations;
using CardGame.Core.Commands.Interfaces;
using CardGame.Core.State.Enums;
using CardGame.Core.State.Models;
using System.Collections.Generic;
using System.Linq;

namespace CardGame.Core.AI.Logic
{
    /// <summary>
    /// Generates all legal game commands for a player in a given game state.
    /// </summary>
    public class MoveGenerator
    {
        #region Public Methods

        /// <summary>
        /// Generates all legal moves available to a player in the current game state.
        /// </summary>
        /// <param name="state">The current game state.</param>
        /// <param name="playerId">The ID of the player to generate moves for.</param>
        /// <returns>A list of legal IGameCommand objects.</returns>
        public List<IGameCommand> GenerateLegalMoves(GameState state, int playerId)
        {
            var moves = new List<IGameCommand>();
            var player = state.GetPlayer(playerId);

            // --- MULLIGAN PHASE ---
            if (state.CurrentPhase == GamePhase.Mulligan)
            {
                if (state.PlayersReady.Contains(playerId))
                {
                    return moves;
                }

                var hand = player.Hand;
                int count = hand.Count;
                
                for (int i = 0; i < (1 << count); i++)
                {
                    var rejectedIds = new List<int>();
                    for (int j = 0; j < count; j++)
                    {
                        if ((i & (1 << j)) != 0)
                        {
                            rejectedIds.Add(hand[j].InstanceId);
                        }
                    }
                    moves.Add(new ConfirmMulliganCommand(playerId, rejectedIds));
                }
                
                return moves;
            }

            // --- PENDING INTERACTION ---
            if (state.PendingInteraction != null)
            {
                var pending = state.PendingInteraction;
                
                if (pending.RequiredTargetType == TargetType.EmptyLane)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        if (state.Board.Lines[i].IsSlotEmpty(playerId))
                        {
                            moves.Add(new SelectTargetCommand(playerId, i));
                        }
                    }
                    
                    return moves;
                }

                if (pending.RequiredTargetType == TargetType.Choice)
                {
                    for (int i = 0; i < pending.Options.Count; i++)
                    {
                        moves.Add(new SelectTargetCommand(playerId, i));
                    }
                }
                else
                {
                    var targets = EffectTargetResolver.GetPotentialTargets(
                        pending.RequiredTargetType, state, pending.SourceCardInstanceId);
                    
                    foreach (var target in targets)
                    {
                        moves.Add(new SelectTargetCommand(playerId, target.InstanceId));
                    }
                }
                
                return moves;
            }

            // --- UNIT PLAY PHASE ---
            if (state.CurrentPhase == GamePhase.UnitOnly || state.CurrentPhase == GamePhase.UnitAndAction)
            {
                foreach (var card in player.Hand.Where(c => c.Definition.Type == CardType.Unit))
                {
                    if (PlayValidator.CanPlay(card, state, playerId))
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

            // --- ACTION (SPELL) PLAY PHASE ---
            if (state.CurrentPhase == GamePhase.ActionOnly || state.CurrentPhase == GamePhase.UnitAndAction)
            {
                foreach (var card in player.Hand.Where(c => c.Definition.Type == CardType.Spell))
                {
                    if (PlayValidator.CanPlay(card, state, playerId))
                    {
                        moves.Add(new PlaySpellCommand(playerId, card.InstanceId));
                    }
                }
            }

            // --- END TURN OPTION ---
            moves.Add(new EndPhaseCommand(playerId));
            
            return moves;
        }

        #endregion
    }
}