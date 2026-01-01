using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CardGame.Core.AI;
using CardGame.Core.AI.Logic;
using CardGame.Core.AI.Logic.Mcts;
using CardGame.Core.AI.Strategies;
using CardGame.Core.Application;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Factories;
using CardGame.Core.Cards.Models;
using CardGame.Core.State.Models;
using CardGame.Core.State.Enums;
using CardGame.Core.Events;
using CardGame.Core.Events.Interfaces;
using CardGame.Core.Commands.Interfaces;
using CardGame.Core.Commands.Implementations;
using CardGame.ConsoleApp.Evolution;

namespace CardGame.ConsoleApp
{
    public static class AIvsEvolutionRunner
    {
        private static readonly Queue<string> _displayLogs = new();
        private static List<EvaluatedMove> _lastThoughts = new List<EvaluatedMove>();
        private static int _eventsSeenSoFar = 0;
        private const int UI_WIDTH = 158;
        private static int _p1Wins = 0, _p2Wins = 0, _gamesPlayed = 0;
        private static bool _isAutoMode = false;

        public static async Task RunAsync()
        {
            Console.Clear();
            var hof = new HallOfFame();
            hof.Load();

            // 1. Load candidates
            string masterPath = "best_bot_dna.json";
            GeneticIndividual globalMaster = null;
            if (File.Exists(masterPath))
            {
                globalMaster = JsonSerializer.Deserialize<GeneticIndividual>(File.ReadAllText(masterPath));
            }

            var selectableBots = new List<GeneticIndividual>();
            if (globalMaster != null)
            {
                var masterEntry = globalMaster.Clone();
                masterEntry.Id = "MASTER";
                masterEntry.Wins = globalMaster.Wins;
                masterEntry.GamesPlayed = globalMaster.GamesPlayed;
                masterEntry.Fitness = globalMaster.Fitness;
                masterEntry.Generation = globalMaster.Generation;
                selectableBots.Add(masterEntry);
            }

            foreach (var champ in hof.Champions.Where(c => c != null))
            {
                bool isDuplicate = false;
                if (globalMaster != null)
                {
                    float diff = 0;
                    for (int i = 0; i < globalMaster.StrategyDNA.Length; i++)
                        diff += Math.Abs(globalMaster.StrategyDNA[i] - champ.StrategyDNA[i]);
                    if (diff < 0.01f) isDuplicate = true;
                }
                if (!isDuplicate) selectableBots.Add(champ);
            }

            if (!selectableBots.Any())
            {
                Console.WriteLine("ERROR: No evolved bots found.");
                await Task.Delay(3000);
                return;
            }

            // 2. Selection Menus
            Console.WriteLine("╔══════════════════════════════════════════════════════╗");
            Console.WriteLine("║            EVOLUTION ARENA: SELECT BRAIN             ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════╝");
            for (int i = 0; i < selectableBots.Count; i++)
            {
                var b = selectableBots[i];
                string label = b.Id == "MASTER" ? "GLOBAL MASTER" : $"CHAMPION [{b.Id}]";
                Console.WriteLine($"{i + 1}. {label.PadRight(18)} | Gen: {b.Generation,-4} | Fit: {b.Fitness:F0}");
            }
            Console.Write("\nSelect Evolved Brain (1-" + selectableBots.Count + "): ");
            int brainIdx = int.TryParse(Console.ReadLine(), out int bi) ? bi - 1 : 0;
            var selectedBot = selectableBots[Math.Clamp(brainIdx, 0, selectableBots.Count - 1)];

            Console.WriteLine("\nChoose P2 (Standard Strategy) Algorithm:");
            Console.WriteLine("1. Beam Search (Original)");
            Console.WriteLine("2. MCTS (Monte Carlo Tree Search)");
            var algoKey = Console.ReadKey(true).KeyChar;
            bool p2UsesMcts = algoKey == '2';

            Console.Clear();
            Console.WriteLine("1. Overwatch Mode (Step-by-step)");
            Console.WriteLine("2. Auto Mode (Fast simulation)");
            _isAutoMode = Console.ReadKey(true).KeyChar == '2';

            CardLibrary.Instance.LoadFromJson("Data/Cards/cards.json");

            while (true)
            {
                _displayLogs.Clear(); _eventsSeenSoFar = 0;
                var rng = new DeterministicRng(new Random().Next());
                var factory = new CardFactory(CardLibrary.Instance, rng);

                // Mirror Deck: Both use the evolved bot's deck
                var deck1 = selectedBot.DeckDNA.Select(id => factory.CreateCard(id, 1)).ToList();
                var deck2 = selectedBot.DeckDNA.Select(id => factory.CreateCard(id, 2)).ToList();

                var engine = new GameEngine(GameState.Initial(1, deck1, deck2, rng), rng.Seed);

                // Solvers
                var solver1 = new BotSolver(engine, 1, new EvolvableStrategy(selectedBot.StrategyDNA), beamWidth: 4, maxDepth: 5);

                BotSolver solver2_Beam = p2UsesMcts ? null : new BotSolver(engine, 2, new StandardStrategy(), beamWidth: 4, maxDepth: 5);
                MctsSolver solver2_Mcts = p2UsesMcts ? new MctsSolver(engine, 2, new StandardStrategy()) : null;

                while (!engine.IsGameOver)
                {
                    var state = engine.CurrentState;
                    UpdateLogs(engine);

                    if (state.CurrentPhase != GamePhase.Mulligan && state.CurrentPhase != GamePhase.Combat)
                    {
                        if (state.ActivePlayerId == 1)
                        {
                            _lastThoughts = solver1.FindBestMoves(state);
                        }
                        else
                        {
                            if (p2UsesMcts)
                            {
                                var mctsMove = solver2_Mcts.FindBestMove(state, thinkingTimeMs: _isAutoMode ? 200 : 1000);
                                _lastThoughts = new List<EvaluatedMove> { mctsMove };
                            }
                            else
                            {
                                _lastThoughts = solver2_Beam.FindBestMoves(state);
                            }
                        }

                        DrawUI(state, selectedBot.Id, p2UsesMcts);
                        if (!_isAutoMode) { var k = Console.ReadKey(true); if (k.Key == ConsoleKey.Escape) return; }
                    }

                    if (state.CurrentPhase == GamePhase.Mulligan)
                    {
                        engine.ExecuteCommand(new ConfirmMulliganCommand(1, new()));
                        engine.ExecuteCommand(new ConfirmMulliganCommand(2, new()));
                    }
                    else if (state.CurrentPhase == GamePhase.Combat)
                    {
                        engine.ExecuteCommand(new EndPhaseCommand(state.ActivePlayerId));
                    }
                    else
                    {
                        var best = _lastThoughts.FirstOrDefault();
                        if (best != null) engine.ExecuteCommand(best.Command);
                        else engine.ExecuteCommand(new EndPhaseCommand(state.ActivePlayerId));
                    }
                    if (_isAutoMode) await Task.Delay(20);
                }

                if (engine.WinnerId == 1) _p1Wins++; else if (engine.WinnerId == 2) _p2Wins++;
                DrawUI(engine.CurrentState, selectedBot.Id, p2UsesMcts);
                _gamesPlayed++;
                await Task.Delay(2000);
            }
        }

        private static void DrawUI(GameState s, string botId, bool p2Mcts)
        {
            StringBuilder sb = new StringBuilder();
            Console.SetCursorPosition(0, 0);

            string phaseName = s.CurrentPhase.ToString().ToUpper();
            string p1Tag = "EVO BRAIN";
            string p2Tag = p2Mcts ? "STD (MCTS)" : "STD (BEAM)";
            string activeLabel = s.ActivePlayerId == 1 ? p1Tag : p2Tag;

            string headL = $" ROUND: {s.TurnNumber} | PHASE: {phaseName} | ACTIVE: {activeLabel}";
            string headR = $"[ MIRROR: {botId} DECK ] | EVO P1: {_p1Wins} | STD P2: {_p2Wins} ";

            sb.AppendLine("╔" + new string('═', UI_WIDTH - 2) + "╗");
            sb.AppendLine("║ " + headL.PadRight(UI_WIDTH - headR.Length - 4) + headR + "║");
            sb.AppendLine("╠" + new string('═', 106) + "╦" + new string('═', UI_WIDTH - 109) + "╣");

            string[] left = RenderBoard(s, p1Tag, p2Tag);
            string[] right = RenderBrain(s, s.ActivePlayerId == 1 ? "EVO" : "STD");

            for (int i = 0; i < 42; i++)
            {
                string l = i < left.Length ? left[i] : "";
                string r = i < right.Length ? right[i] : "";

                string leftPart = l.PadRight(104).Substring(0, 104);
                string rightPart = r.PadRight(UI_WIDTH - 111).Substring(0, UI_WIDTH - 111);
                sb.AppendLine($"║ {leftPart} ║ {rightPart} ║");
            }
            sb.AppendLine("╚" + new string('═', 106) + "╩" + new string('═', UI_WIDTH - 109) + "╝");
            Console.Write(sb.ToString());
        }

        private static string[] RenderBoard(GameState s, string p1T, string p2T)
        {
            var res = new List<string>();
            res.Add($" [ PLAYER P2 ({p2T}) | HP: {s.PlayerB.Health,2} | BLOOD: {s.PlayerB.CurrentBlood}/{s.PlayerB.MaxBlood} ]");
            res.AddRange(WrapHand(s.PlayerB.Hand, "P2"));
            res.Add("");
            for (int r = 0; r < 11; r++)
            {
                string row = "  ";
                for (int c = 0; c < 4; c++) row += GetRowSegment(r, s.Board.Lines[c]).PadRight(25);
                res.Add(row);
            }
            res.Add("");
            res.Add($" [ PLAYER P1 ({p1T}) | HP: {s.PlayerA.Health,2} | BLOOD: {s.PlayerA.CurrentBlood}/{s.PlayerA.MaxBlood} ]");
            res.AddRange(WrapHand(s.PlayerA.Hand, "P1"));
            res.Add(new string('-', 104));
            res.Add(" EVENT LOG:");
            res.AddRange(_displayLogs.Reverse().Take(8).Select(x => " " + x));
            return res.ToArray();
        }

        private static List<string> WrapHand(IReadOnlyList<CardInstance> hand, string pId)
        {
            string prefix = $" Hand {pId}: [";
            var cards = hand.Select(c => $"({c.CurrentStats.BloodCost}){c.Definition.Name}").ToList();
            var result = new List<string>();
            string firstLine = prefix;
            int splitAt = -1;
            if (!cards.Any()) { result.Add(firstLine + "EMPTY]"); result.Add(""); return result; }
            for (int i = 0; i < cards.Count; i++)
            {
                string part = cards[i] + (i == cards.Count - 1 ? "" : ", ");
                if ((firstLine + part).Length > 95) { splitAt = i; break; }
                firstLine += part;
            }
            if (splitAt == -1) { result.Add(firstLine + "]"); result.Add(""); }
            else { result.Add(firstLine); result.Add("           " + string.Join(", ", cards.Skip(splitAt)) + "]"); }
            return result;
        }

        private static string GetRowSegment(int row, Line line) => row switch
        {
            0 => "┌───────────────────┐",
            1 => $"│ {Center(line.Player2Unit?.Definition.Name ?? "[ EMPTY ]", 17)} │",
            2 => $"│ {Stats(line.Player2Unit)} │",
            3 => "└───────────────────┘",
            4 => "         ┃          ",
            5 => "         VS         ",
            6 => "         ┃          ",
            7 => "┌───────────────────┐",
            8 => $"│ {Center(line.Player1Unit?.Definition.Name ?? "[ EMPTY ]", 17)} │",
            9 => $"│ {Stats(line.Player1Unit)} │",
            10 => "└───────────────────┘",
            _ => ""
        };

        private static string Center(string t, int w) => t.Length > w ? t.Substring(0, w) : t.PadLeft((w + t.Length) / 2).PadRight(w);
        private static string Stats(CardInstance? u)
        {
            if (u == null) return "                 ";
            string kw = u.CurrentStats.Keywords.Contains(Keyword.Marked) ? "!!" : (u.IsSilenced ? "S!" : "  ");
            return $"ATK:{u.CurrentStats.Attack,-2} HP:{u.CurrentStats.Health,-2} {kw}";
        }

        private static string[] RenderBrain(GameState s, string name)
        {
            var res = new List<string> { $"   BOT THOUGHTS [{name}]", "------------------------" };
            if (s.CurrentPhase == GamePhase.Combat) { res.Add(" [AUTO] Combat simulation..."); return res.ToArray(); }
            foreach (var t in _lastThoughts.Take(12))
            {
                string prefix = t == _lastThoughts.First() ? ">>" : "  ";
                string scoreStr = Math.Abs(t.Score) > 1000000 ? (t.Score > 0 ? "LETHAL" : "DEFEAT") : t.Score.ToString("F1");
                string desc = t.Description.Length > 22 ? t.Description.Substring(0, 21) + "…" : t.Description;
                res.Add($"{prefix} {desc.PadRight(22)} | {scoreStr.PadLeft(7)}");
                if (t == _lastThoughts.First()) { res.Add($"   L {Truncate(t.DeepReasoning, 35)}"); res.Add(""); }
            }
            return res.ToArray();
        }

        private static void UpdateLogs(GameEngine e)
        {
            var all = e.Events.GetGlobalHistory().ToList();
            for (int i = _eventsSeenSoFar; i < all.Count; i++)
            {
                var evt = all[i];
                if (evt is UnitDamagedEvent d) _displayLogs.Enqueue($"> {(d.Source?.Definition.Name ?? "Effect")} hits {(d.Unit?.Definition.Name ?? "Hero")} for {d.Amount}");
                else if (evt is CardPlayedEvent cp) _displayLogs.Enqueue($"> P{cp.PlayerId} plays {cp.Card.Definition.Name}");
                else if (evt is UnitDiedEvent dd) _displayLogs.Enqueue($"> {dd.Unit.Definition.Name} died");
                if (_displayLogs.Count > 15) _displayLogs.Dequeue();
            }
            _eventsSeenSoFar = all.Count;
        }

        private static string Truncate(string s, int max) => s.Length <= max ? s : s.Substring(0, max - 1) + "…";
        private static string CenterText(string text, int width)
        {
            if (text.Length >= width) return text.Substring(0, width);
            int leftPadding = (width - text.Length) / 2;
            return new string(' ', leftPadding) + text + new string(' ', width - text.Length - leftPadding);
        }
    }
}