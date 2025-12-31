using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CardGame.ConsoleApp.Evolution; // Import do DNA
using CardGame.Core.AI.Logic;
using CardGame.Core.AI.Logic.Mcts; // Import do MCTS
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

namespace CardGame.ConsoleApp
{
    public static class AIBattleRunnerIII
    {
        private static readonly Queue<string> _displayLogs = new();
        private static List<EvaluatedMove> _lastThoughts = new List<EvaluatedMove>();
        private static int _eventsSeenSoFar = 0;
        private const int UI_WIDTH = 158;
        private static StringBuilder _matchAudit = new StringBuilder();
        private static int _p1Wins = 0, _p2Wins = 0, _gamesPlayed = 0;
        private static string _sessionLogPath = "";
        private static bool _isAutoMode = false;

        public static async Task RunAsync()
        {
            Console.Clear();
            Console.WriteLine("=== ARCHITECT OF ROUNDS: BEAM vs MCTS ===");
            Console.WriteLine("1. Overwatch Mode (Step-by-step - Space)");
            Console.WriteLine("2. Auto Mode (Fast simulation - Logs only)");
            var modeKey = Console.ReadKey(true);
            _isAutoMode = modeKey.KeyChar == '2';

            CardLibrary.Instance.Clear();
            CardLibrary.Instance.LoadFromJson("Data/Cards/cards.json");

            _sessionLogPath = $"audit_3.1_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
            File.WriteAllText(_sessionLogPath, $"=== START SESSION BRAIN 3.1: {DateTime.Now} ===\n\n");

            // --- ŁADOWANIE DNA (Dla Gracza 2 - MCTS) ---
            float[] championDna = null;
            if (File.Exists("best_bot_dna.json"))
            {
                try
                {
                    var json = File.ReadAllText("best_bot_dna.json");
                    var botData = JsonSerializer.Deserialize<GeneticIndividual>(json);
                    championDna = botData?.StrategyDNA;
                }
                catch { /* Ignoruj błędy ładowania */ }
            }

            while (true)
            {
                _matchAudit.Clear(); _displayLogs.Clear(); _eventsSeenSoFar = 0;
                var rng = new DeterministicRng(new Random().Next());
                var factory = new CardFactory(CardLibrary.Instance, rng);

                bool p1IsCtrl = _gamesPlayed % 2 == 0;
                var deck1 = CreateDeck(factory, 1, p1IsCtrl);
                var deck2 = CreateDeck(factory, 2, !p1IsCtrl);

                var engine = new GameEngine(GameState.Initial(1, deck1, deck2, rng), rng.Seed);

                // --- KONFIGURACJA BOTÓW ---
                // P1: Stary Beam Search (Szybki, Deterministyczny)
                var solver1 = new BotSolver(engine, 1, new EvolvableStrategy(championDna), beamWidth: 4, maxDepth: 5);

                // P2: Nowy MCTS (Wolniejszy, Strategiczny, Ewolucyjny)
                var solver2 = new MctsSolver(engine, 2, new EvolvableStrategy(championDna));

                LogToAudit($"\n--- MATCH NO {_gamesPlayed + 1} ---");

                while (!engine.IsGameOver)
                {
                    var state = engine.CurrentState;
                    UpdateLogs(engine);

                    if (state.CurrentPhase != GamePhase.Mulligan && state.CurrentPhase != GamePhase.Combat)
                    {
                        // --- LOGIKA WYBORU RUCHU ---
                        if (state.ActivePlayerId == 1)
                        {
                            // P1 (Beam): Zwraca listę, bierzemy najlepszy
                            _lastThoughts = solver1.FindBestMoves(state);
                        }
                        else
                        {
                            // P2 (MCTS): Zwraca jeden najlepszy po czasie. Opakowujemy w listę dla UI.
                            int thinkTime = _isAutoMode ? 100 : 1000; // Szybciej w auto, wolniej w oglądaniu
                            var bestMcts = solver2.FindBestMove(state, thinkTime);
                            _lastThoughts = new List<EvaluatedMove> { bestMcts };
                        }

                        DrawUI(state, p1IsCtrl);

                        if (!_isAutoMode)
                        {
                            var key = Console.ReadKey(true);
                            if (key.Key == ConsoleKey.Escape) return;
                        }
                    }

                    if (state.CurrentPhase == GamePhase.Mulligan)
                    {
                        PerformMulligan(engine, 1, p1IsCtrl);
                        PerformMulligan(engine, 2, !p1IsCtrl);
                    }
                    else if (state.CurrentPhase == GamePhase.Combat)
                    {
                        engine.ExecuteCommand(new EndPhaseCommand(state.ActivePlayerId));
                        if (!_isAutoMode) await Task.Delay(200);
                    }
                    else
                    {
                        var best = _lastThoughts.FirstOrDefault();
                        if (best != null)
                        {
                            string logEntry = $"[R{state.TurnNumber} P{state.ActivePlayerId}] Action: {FormatCmdDetailed(best.Command, state)} | Score: {best.Score:F2}";
                            LogToAudit(logEntry);
                            engine.ExecuteCommand(best.Command);
                        }
                        else engine.ExecuteCommand(new EndPhaseCommand(state.ActivePlayerId));
                    }

                    if (_isAutoMode) await Task.Delay(10);
                }

                if (engine.WinnerId == 1) _p1Wins++; else if (engine.WinnerId == 2) _p2Wins++;
                LogToAudit($"WINNER: P{engine.WinnerId}");

                _gamesPlayed++;
                DrawUI(engine.CurrentState, p1IsCtrl);
                await Task.Delay(_isAutoMode ? 500 : 2000);
            }
        }

        // --- METODY POMOCNICZE UI (Bez zmian, tylko skopiowane dla spójności) ---

        private static void LogToAudit(string message)
        {
            _matchAudit.AppendLine(message);
            File.AppendAllText(_sessionLogPath, message + "\n");
        }

        private static void DrawUI(GameState s, bool p1IsCtrl)
        {
            StringBuilder sb = new StringBuilder();
            Console.SetCursorPosition(0, 0);

            string phaseName = s.CurrentPhase.ToString();
            string p1Name = "P1 (BEAM)";
            string p2Name = "P2 (MCTS)";
            string active = s.ActivePlayerId == 1 ? p1Name : p2Name;

            string head = $" ROUND: {s.TurnNumber} | PHASE: {phaseName} | ACTIVE: {active} | SCORE: {p1Name}:{_p1Wins} vs {p2Name}:{_p2Wins}";

            sb.AppendLine("╔" + new string('═', UI_WIDTH - 2) + "╗");
            sb.AppendLine("║ " + head.PadRight(UI_WIDTH - 4) + " ║");
            sb.AppendLine("╠" + new string('═', 106) + "╦" + new string('═', UI_WIDTH - 109) + "╣");

            string[] left = RenderBoard(s);
            string[] right = RenderBrain(s);

            for (int i = 0; i < 40; i++)
            {
                string l = i < left.Length ? left[i] : "";
                string r = i < right.Length ? right[i] : "";
                sb.AppendLine($"║ {l.PadRight(104)} ║ {r.PadRight(UI_WIDTH - 111)} ║");
            }
            sb.AppendLine("╚" + new string('═', 106) + "╩" + new string('═', UI_WIDTH - 109) + "╝");
            Console.Write(sb.ToString());
        }

        private static string[] RenderBoard(GameState s)
        {
            var res = new List<string>();
            res.Add($" [ P2 (MCTS) | HP: {s.PlayerB.Health,2} | BLOOD: {s.PlayerB.CurrentBlood}/{s.PlayerB.MaxBlood} ]");
            res.AddRange(WrapHand(s.PlayerB.Hand, "P2"));
            res.Add("");
            for (int r = 0; r < 11; r++)
            {
                string row = "  ";
                for (int c = 0; c < 4; c++) row += GetRowSegment(r, s.Board.Lines[c]).PadRight(25);
                res.Add(row);
            }
            res.Add("");
            res.Add($" [ P1 (BEAM) | HP: {s.PlayerA.Health,2} | BLOOD: {s.PlayerA.CurrentBlood}/{s.PlayerA.MaxBlood} ]");
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
            if (!cards.Any()) return new List<string> { prefix + "EMPTY]", "" };

            // Proste łamanie linii dla czytelności
            var res = new List<string>();
            string line = prefix;
            foreach (var c in cards)
            {
                if ((line + c).Length > 95) { res.Add(line); line = "           " + c + ", "; }
                else line += c + ", ";
            }
            res.Add(line.TrimEnd(',', ' ') + "]");
            return res;
        }

        private static string GetRowSegment(int row, Line line) => row switch
        {
            0 => "┌───────────────────┐",
            1 => $"│ {Center(line.Player2Unit?.Definition.Name ?? "[ . ]", 17)} │",
            2 => $"│ {Stats(line.Player2Unit)} │",
            3 => "└───────────────────┘",
            4 => "         ┃          ",
            5 => "         VS         ",
            6 => "         ┃          ",
            7 => "┌───────────────────┐",
            8 => $"│ {Center(line.Player1Unit?.Definition.Name ?? "[ . ]", 17)} │",
            9 => $"│ {Stats(line.Player1Unit)} │",
            10 => "└───────────────────┘",
            _ => ""
        };

        private static string Center(string t, int w) => t.Length > w ? t.Substring(0, w) : t.PadLeft((w + t.Length) / 2).PadRight(w);
        private static string Stats(CardInstance? u) => u == null ? "                 " : $"ATK:{u.CurrentStats.Attack} HP:{u.CurrentStats.Health}";

        private static string[] RenderBrain(GameState s)
        {
            var res = new List<string> { $"   BOT THOUGHTS P{s.ActivePlayerId}", "------------------------" };
            foreach (var t in _lastThoughts.Take(12))
            {
                string desc = t.Description.Length > 22 ? t.Description.Substring(0, 22) : t.Description;
                string score = t.Score > 10000 ? "WIN" : t.Score.ToString("F1");
                res.Add($">> {desc.PadRight(22)} | {score.PadLeft(6)}");
                if (t == _lastThoughts.First())
                {
                    res.Add(" L " + (t.DeepReasoning.Length > 35 ? t.DeepReasoning.Substring(0, 35) : t.DeepReasoning));
                    res.Add("");
                }
            }
            return res.ToArray();
        }

        private static string FormatCmdDetailed(IGameCommand? c, GameState s)
        {
            if (c is PlayUnitCommand pu) return $"Play Unit {pu.CardInstanceId} @ L{pu.TargetLineIndex}";
            if (c is PlaySpellCommand ps) return $"Play Spell {ps.CardInstanceId}";
            if (c is SelectTargetCommand st) return $"Target {st.TargetId}";
            return "Pass";
        }

        private static void UpdateLogs(GameEngine e)
        {
            var all = e.Events.GetGlobalHistory().ToList();
            for (int i = _eventsSeenSoFar; i < all.Count; i++)
            {
                var evt = all[i];
                if (evt is UnitDamagedEvent d) _displayLogs.Enqueue($"> Dmg {d.Amount} to {d.Unit?.Definition.Name ?? "Hero"}");
                else if (evt is CardPlayedEvent cp) _displayLogs.Enqueue($"> P{cp.PlayerId} play {cp.Card.Definition.Name}");
                else if (evt is UnitDiedEvent dd) _displayLogs.Enqueue($"> {dd.Unit.Definition.Name} died");
                if (_displayLogs.Count > 15) _displayLogs.Dequeue();
            }
            _eventsSeenSoFar = all.Count;
        }

        private static void PerformMulligan(GameEngine e, int pid, bool ctrl)
        {
            e.ExecuteCommand(new ConfirmMulliganCommand(pid, new List<int>()));
        }

        private static List<CardInstance> CreateDeck(CardFactory f, int id, bool ctrl)
        {
            int[] ids = ctrl ? new[] { 4, 32, 26, 24, 25, 31, 7, 21, 15, 13 } : new[] { 12, 3, 6, 10, 11, 36, 9, 18, 1, 5 };
            var d = new List<CardInstance>();
            foreach (var cid in ids) for (int i = 0; i < 3; i++) d.Add(f.CreateCard(cid, id));
            return d;
        }
    }
}