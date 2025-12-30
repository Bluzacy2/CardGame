using CardGame.Core.AI.Interfaces;
using CardGame.Core.Application;
using CardGame.Core.Cards.Data;
using CardGame.Core.Commands.Implementations;
using CardGame.Core.Commands.Interfaces;
using CardGame.Core.State.Enums;
using CardGame.Core.State.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CardGame.Core.AI.Logic
{
    // --- KLASY POMOCNICZE (DTO) ---
    // Definiujemy je tutaj, aby by³y widoczne dla BotSolvera i reszty projektu w tym namespace.

    public class EvaluatedMove
    {
        public IGameCommand Command { get; }
        public float Score { get; }
        public string Description { get; }
        public string DeepReasoning { get; }

        public EvaluatedMove(IGameCommand command, float score, string description = "", string deepReasoning = "")
        {
            Command = command;
            Score = score;
            Description = description;
            DeepReasoning = deepReasoning;
        }
    }

    public class SimulationNode
    {
        public GameState State { get; }
        public IGameCommand? InitialMove { get; }
        public float Score { get; }

        public SimulationNode(GameState state, IGameCommand? initialMove, float score)
        {
            State = state;
            InitialMove = initialMove;
            Score = score;
        }
    }

    // --- G£ÓWNA KLASA SOLVERA (Mózg 3.1) ---

    public class BotSolver
    {
        private readonly GameEngine _engineTemplate;
        private readonly MoveGenerator _moveGenerator;
        private readonly IAIStrategy _strategy;
        private readonly VirtualOpponent _virtualOpponent;
        private readonly int _botId;

        // Parametry Beam Search
        private const int BeamWidth = 4; // Szerokoœæ wi¹zki (ile najlepszych scenariuszy œledzimy)
        private const int MaxDepth = 5;  // G³êbokoœæ (ile ruchów w przód, wliczaj¹c reakcje wroga)

        public BotSolver(GameEngine engine, int botId, IAIStrategy strategy)
        {
            _engineTemplate = engine;
            _botId = botId;
            _strategy = strategy;
            _moveGenerator = new MoveGenerator();
            _virtualOpponent = new VirtualOpponent(engine.Factory);
        }

        public List<EvaluatedMove> FindBestMoves(GameState rootState)
        {
            var legalMoves = _moveGenerator.GenerateLegalMoves(rootState, _botId);
            var results = new List<EvaluatedMove>();

            foreach (var move in legalMoves)
            {
                // Uruchamiamy symulacjê dla ka¿dego legalnego ruchu
                var sim = SimulateRoundWithReasoning(rootState, move);
                string desc = GenerateDescription(move, rootState);

                results.Add(new EvaluatedMove(move, sim.Score, desc, sim.Reasoning));
            }

            // Sortujemy malej¹co po wyniku
            return results.OrderByDescending(r => r.Score).ToList();
        }

        private (float Score, string Reasoning) SimulateRoundWithReasoning(GameState rootState, IGameCommand firstMove)
        {
            var simulator = new GameEngine(rootState, _engineTemplate.Rng.Seed);

            // 1. ACTION CHAINING: Wykonaj ruch. Jeœli wymaga celu, dobierz go w tej samej klatce.
            //    To zapobiega marnowaniu "g³êbokoœci" na techniczne klikniêcia.
            GameState state = ExecuteFullAction(simulator, firstMove);

            // Jeœli ruch zosta³ zablokowany przez silnik (nielegalny), dajemy karê.
            if (state == rootState && !(firstMove is EndPhaseCommand))
                return (-3000000f, "Ruch nielegalny");

            // 2. BEAM SEARCH: Symulacja drzewiasta
            var beam = new List<SimulationNode> {
                new SimulationNode(state, firstMove, _strategy.Evaluate(state, _botId))
            };

            for (int d = 1; d < MaxDepth; d++)
            {
                var nextCandidates = new List<SimulationNode>();
                foreach (var node in beam)
                {
                    // Sprawdzamy czyja tura w symulacji
                    bool isBotTurn = node.State.ActivePlayerId == _botId;

                    // Minimax:
                    // - Jeœli nasza tura: szukamy ruchu, który maksymalizuje nasz wynik (BestOutcome).
                    // - Jeœli tura wroga: zak³adamy, ¿e wróg zagra tak, by nas zniszczyæ (WorstOutcome).
                    //   U¿ywamy tu VirtualOpponent z Deck Trackingiem.
                    var outcome = isBotTurn
                        ? SimulateAndFindBestOutcomeForBot(node.State)
                        : SimulateAndFindWorstOutcomeForBot(node.State);

                    nextCandidates.Add(new SimulationNode(outcome.State, node.InitialMove, outcome.Score));
                }

                if (!nextCandidates.Any()) break;

                // Zawê¿amy poszukiwania do najlepszych kandydatów (Beam Width)
                beam = nextCandidates.OrderByDescending(n => n.Score).Take(BeamWidth).ToList();
            }

            // Wybieramy najlepszy stan koñcowy z ca³ej symulacji
            var best = beam.OrderByDescending(n => n.Score).First();
            GameState finalState = best.State;

            // 3. PROJEKCJA WALKI (Force Combat)
            // Jeœli symulacja skoñczy³a siê w œrodku rundy (nie dotar³a do nowej tury),
            // musimy wymusiæ symulacjê walki, aby zobaczyæ, czy jednostki prze¿yj¹.
            if (finalState.CurrentPhase != GamePhase.Combat && finalState.TurnNumber == rootState.TurnNumber)
            {
                var endSim = new GameEngine(finalState, _engineTemplate.Rng.Seed);
                // Komenda EndPhase w CombatPhaseState automatycznie wyzwala logikê walki
                finalState = endSim.ExecuteCommand(new EndPhaseCommand(finalState.ActivePlayerId)).NewState;
            }

            // Generujemy opis "Wizji" na podstawie ró¿nicy stanu pocz¹tkowego i koñcowego
            return (best.Score, GenerateDeepReasoning(rootState, finalState));
        }

        private (GameState State, float Score) SimulateAndFindBestOutcomeForBot(GameState currentState)
        {
            var moves = _moveGenerator.GenerateLegalMoves(currentState, _botId);
            float bestScore = float.MinValue;
            GameState bestState = currentState;

            // Optymalizacja: Sprawdzamy tylko 6 pierwszych (logicznych) ruchów wewn¹trz symulacji
            foreach (var move in moves.Take(6))
            {
                var sim = new GameEngine(currentState, _engineTemplate.Rng.Seed);
                var resState = ExecuteFullAction(sim, move);
                float score = _strategy.Evaluate(resState, _botId);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestState = resState;
                }
            }
            return (bestState, bestScore);
        }

        private (GameState State, float Score) SimulateAndFindWorstOutcomeForBot(GameState currentState)
        {
            var oppId = currentState.ActivePlayerId;

            // Wstrzykujemy "Realistyczn¹ Rêkê" przeciwnikowi (na podstawie tego, co zosta³o mu w talii)
            var stateWithPhantom = _virtualOpponent.InjectRealisticPhantomHand(currentState, oppId);
            var enemyMoves = _moveGenerator.GenerateLegalMoves(stateWithPhantom, oppId);

            float worstForBot = float.MaxValue;
            GameState worstState = currentState;

            // Zak³adamy, ¿e przeciwnik jest m¹dry i wybierze ruch najgorszy dla nas
            foreach (var move in enemyMoves.Take(6))
            {
                var sim = new GameEngine(stateWithPhantom, _engineTemplate.Rng.Seed);
                var resState = ExecuteFullAction(sim, move);

                float score = _strategy.Evaluate(resState, _botId);
                if (score < worstForBot)
                {
                    worstForBot = score;
                    worstState = resState;
                }
            }
            return (worstState, worstForBot);
        }

        /// <summary>
        /// Wykonuje ruch i automatycznie rozwi¹zuje wszelkie interakcje (wybór celu).
        /// Pozwala botowi traktowaæ "Zagraj Czar + Wybierz Cel" jako jedn¹ decyzjê.
        /// </summary>
        private GameState ExecuteFullAction(GameEngine engine, IGameCommand cmd)
        {
            var state = engine.ExecuteCommand(cmd).NewState;
            int safety = 0;

            // Pêtla obs³uguj¹ca wieloetapowe zagrania (np. wybór celu)
            while (state.PendingInteraction != null && safety++ < 5)
            {
                var targets = _moveGenerator.GenerateLegalMoves(state, _botId);
                if (!targets.Any()) break;

                // Wybieramy cel, który daje najlepszy natychmiastowy wynik
                IGameCommand bestT = targets.First();
                float bestV = float.MinValue;

                foreach (var t in targets.Take(5))
                {
                    var test = new GameEngine(state, _engineTemplate.Rng.Seed).ExecuteCommand(t).NewState;
                    float v = _strategy.Evaluate(test, _botId);
                    if (v > bestV) { bestV = v; bestT = t; }
                }
                state = engine.ExecuteCommand(bestT).NewState;
            }
            return state;
        }

        private string GenerateDeepReasoning(GameState start, GameState end)
        {
            var pS = start.GetPlayer(_botId); var pE = end.GetPlayer(_botId);
            var eS = start.GetOpponent(_botId); var eE = end.GetOpponent(_botId);

            if (eE.Health <= 0) return "!!! LETHAL IN SIGHT !!!";

            List<string> r = new List<string>();

            // Bilans kart
            int cD = pE.Hand.Count - pS.Hand.Count;
            if (cD != 0) r.Add($"Cards:{(cD > 0 ? "+" : "")}{cD}");

            // Obra¿enia w bohatera
            int hD = eS.Health - eE.Health;
            if (hD > 0) r.Add($"EnemyDmg:{hD}");

            // Oszczêdzone zasoby (tylko jeœli jesteœmy w tej samej rundzie)
            if (pE.CurrentBlood > 0 && start.TurnNumber == end.TurnNumber)
                r.Add($"Save:{pE.CurrentBlood}B");

            // Bilans jednostek (zabite vs stracone)
            int kills = start.Board.GetAllUnits().Count(u => u.OwnerPlayerId != _botId) -
                        end.Board.GetAllUnits().Count(u => u.OwnerPlayerId != _botId);
            if (kills > 0) r.Add($"Kills:{kills}");

            return r.Count == 0 ? "Hold Position" : "WIZJA: " + string.Join(", ", r);
        }

        private string GenerateDescription(IGameCommand move, GameState state)
        {
            if (move is PlayUnitCommand pu)
            {
                var card = state.GetPlayer(_botId).Hand.FirstOrDefault(c => c.InstanceId == pu.CardInstanceId);
                if (card == null) return "Summon Unit";

                // Kontekstowe opisy dla UI
                if (card.Definition.Id == "24") return "STRAT: Gerard Precision";
                if (card.Definition.Id == "4") return "STRAT: Focus Fire (Mark)";
                if (card.Definition.Id == "12") return "SACR: Fuel Setup (Cat)";
                if (card.Definition.Id == "10") return "SACR: Absorb stats (Bear)";
                var targetName = "Cel nieznany";
                if (pu.SelectedTargetId.HasValue)
                {
                    targetName = state.Board.GetAllUnits().FirstOrDefault(u => u.InstanceId == pu.SelectedTargetId)?.Definition.Name ?? "Brak";
                }
                return $"Play {card.Definition.Name}";
            }
            if (move is PlaySpellCommand ps)
            {
                var card = state.GetPlayer(_botId).Hand.FirstOrDefault(c => c.InstanceId == ps.CardInstanceId);
                if (card?.Definition.Id == "900") return "COMBO: Recycle Unit";
                return $"Cast {card?.Definition.Name ?? "Spell"}";
            }
            return move is EndPhaseCommand ? "PAS: Manage Resources" : move.GetType().Name;
        }
    }
}