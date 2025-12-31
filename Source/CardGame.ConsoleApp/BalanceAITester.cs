using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CardGame.Core.AI;
using CardGame.Core.AI.Logic;
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
    public static class BalanceAITester
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
            Console.WriteLine("=== SYSTEM TESTOWY AI ===");
            Console.WriteLine("1. Tryb Nadzoru (Krok po kroku - Spacja)");
            Console.WriteLine("2. Tryb Auto (Szybka symulacja - tylko Logi)");
            var modeKey = Console.ReadKey(true);
            _isAutoMode = modeKey.KeyChar == '2';

            CardLibrary.Instance.Clear();
            CardLibrary.Instance.LoadFromJson("Data/Cards/cards.json");

            _sessionLogPath = $"session_audit_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
            File.WriteAllText(_sessionLogPath, $"=== START SESJI TESTOWEJ AI ({(_isAutoMode ? "AUTO" : "NADZÓR")}): {DateTime.Now} ===\n\n");

            while (true)
            {
                _matchAudit.Clear(); _displayLogs.Clear(); _eventsSeenSoFar = 0;
                var rng = new DeterministicRng(new Random().Next());
                var factory = new CardFactory(CardLibrary.Instance, rng);

                bool p1IsCtrl = _gamesPlayed % 2 == 0;
                var engine = new GameEngine(GameState.Initial(1, CreateDeck(factory, 1, p1IsCtrl), CreateDeck(factory, 2, !p1IsCtrl), rng), rng.Seed);

                // POPRAWKA 1: Dodano AISolverType.BeamSearch
                var ai1 = new AIPlayerController(engine, 1, new StandardStrategy(), AISolverType.BeamSearch);
                var ai2 = new AIPlayerController(engine, 2, new StandardStrategy(), AISolverType.BeamSearch);

                LogToAudit($"\n--- MECZ NR {_gamesPlayed + 1} | P1: {(p1IsCtrl ? "CONTROL" : "SACR")} vs P2: {(!p1IsCtrl ? "CONTROL" : "SACR")} ---");

                while (!engine.IsGameOver)
                {
                    var state = engine.CurrentState;
                    UpdateLogs(engine);

                    if (state.CurrentPhase != GamePhase.Mulligan && state.CurrentPhase != GamePhase.Combat)
                    {
                        var activeAI = state.ActivePlayerId == 1 ? ai1 : ai2;

                        // POPRAWKA 2: Zmieniono .Solver na .BeamSolver
                        _lastThoughts = activeAI.BeamSolver.FindBestMoves(state);

                        DrawUI(state, p1IsCtrl);

                        if (!_isAutoMode)
                        {
                            var key = Console.ReadKey(true);
                            if (key.Key == ConsoleKey.Escape) return;
                            if (key.Key == ConsoleKey.A) LogToAudit("\n--- RĘCZNY ZRZUT STANU ---\n" + _matchAudit.ToString());
                        }
                        else
                        {
                            await Task.Delay(50);
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
                            LogToAudit($"[T{state.TurnNumber} P{state.ActivePlayerId}] {FormatCmdDetailed(best.Command, state)} | Score: {best.Score:F1} | {best.DeepReasoning}");
                            engine.ExecuteCommand(best.Command);
                        }
                        else engine.ExecuteCommand(new EndPhaseCommand(state.ActivePlayerId));
                    }
                }

                if (engine.WinnerId == 1) _p1Wins++; else if (engine.WinnerId == 2) _p2Wins++;
                LogToAudit($"MECZ ZAKOŃCZONY. ZWYCIĘZCA: P{engine.WinnerId ?? 0} | WYNIK: P1:{_p1Wins} P2:{_p2Wins}\n" + new string('=', 60));

                _gamesPlayed++;
                DrawUI(engine.CurrentState, p1IsCtrl);
                await Task.Delay(_isAutoMode ? 500 : 2000);
            }
        }

        // ... Reszta metod prywatnych bez zmian (LogToAudit, DrawUI, itd.) ...
        // Skopiuj resztę metod z oryginalnego pliku BalanceAITester.cs jeśli ich tu nie wkleiłem
        private static void LogToAudit(string message)
        {
            _matchAudit.AppendLine(message);
            File.AppendAllText(_sessionLogPath, message + "\n");
        }

        private static void DrawUI(GameState s, bool p1IsCtrl)
        {
            StringBuilder sb = new StringBuilder();
            Console.SetCursorPosition(0, 0);
            int roundNum = s.TurnNumber;
            string phaseName = s.CurrentPhase switch
            {
                GamePhase.UnitOnly => "1/3 (JEDNOSTKI)",
                GamePhase.UnitAndAction => "2/3 (MIESZANA)",
                GamePhase.ActionOnly => "3/3 (AKCJE)",
                GamePhase.Combat => "WALKA",
                _ => s.CurrentPhase.ToString()
            };
            string p1Tag = p1IsCtrl ? "CONTROL" : "SACRIFICE";
            string p2Tag = !p1IsCtrl ? "CONTROL" : "SACRIFICE";
            string activeTag = s.ActivePlayerId == 1 ? $"P1 [{p1Tag}]" : $"P2 [{p2Tag}]";
            string headL = $" RUNDA: {roundNum} | FAZA: {phaseName} | AKTYWNY: {activeTag}";
            string headR = $"[ WINS P1: {_p1Wins} | P2: {_p2Wins} ] | MODE: {(_isAutoMode ? "AUTO" : "STEP")} ";
            sb.AppendLine("╔" + new string('═', UI_WIDTH - 2) + "╗");
            sb.AppendLine("║ " + headL.PadRight(UI_WIDTH - headR.Length - 4) + headR + "║");
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
            res.Add($" [ GRACZ P2 | HP: {s.PlayerB.Health,2} | KREW: {s.PlayerB.CurrentBlood}/{s.PlayerB.MaxBlood} ]");
            res.AddRange(WrapHand(s.PlayerB.Hand, "P2"));
            res.Add("");
            for (int r = 0; r < 11; r++)
            {
                string row = "  ";
                for (int c = 0; c < 4; c++) row += GetRowSegment(r, s.Board.Lines[c]).PadRight(25);
                res.Add(row);
            }
            res.Add("");
            res.Add($" [ GRACZ P1 | HP: {s.PlayerA.Health,2} | KREW: {s.PlayerA.CurrentBlood}/{s.PlayerA.MaxBlood} ]");
            res.AddRange(WrapHand(s.PlayerA.Hand, "P1"));
            res.Add(new string('-', 104));
            res.Add(" DZIENNIK ZDARZEŃ (LOG SESJI: " + Path.GetFileName(_sessionLogPath) + ")");
            res.AddRange(_displayLogs.Reverse().Take(8).Select(x => " " + x));
            return res.ToArray();
        }
        private static List<string> WrapHand(IReadOnlyList<CardInstance> hand, string pId)
        {
            string prefix = $" Ręka {pId}: [";
            var cards = hand.Select(c => $"({c.CurrentStats.BloodCost}){c.Definition.Name}").ToList();
            var result = new List<string>();
            string currentLine = prefix;
            if (cards.Count == 0) { result.Add(prefix + "PUSTA]"); return result; }
            for (int i = 0; i < cards.Count; i++)
            {
                string part = cards[i] + (i == cards.Count - 1 ? "" : ", ");
                if ((currentLine + part).Length > 98) { result.Add(currentLine); currentLine = "           " + part; } else currentLine += part;
            }
            result.Add(currentLine + "]"); return result;
        }
        private static string GetRowSegment(int row, Line line) => row switch
        {
            0 => "┌───────────────────┐",
            1 => $"│ {Center(line.Player2Unit?.Definition.Name ?? "[ WOLNE ]", 17)} │",
            2 => $"│ {Stats(line.Player2Unit)} │",
            3 => "└───────────────────┘",
            4 => "         ┃          ",
            5 => "         VS         ",
            6 => "         ┃          ",
            7 => "┌───────────────────┐",
            8 => $"│ {Center(line.Player1Unit?.Definition.Name ?? "[ WOLNE ]", 17)} │",
            9 => $"│ {Stats(line.Player1Unit)} │",
            10 => "└───────────────────┘",
            _ => ""
        };
        private static string Center(string t, int w) => t.Length > w ? t.Substring(0, w) : t.PadLeft((w + t.Length) / 2).PadRight(w);
        private static string Stats(CardInstance? u)
        {
            if (u == null) return "                 ";
            string status = u.CurrentStats.Keywords.Contains(Keyword.Marked) ? "!!" : "  ";
            if (u.IsSilenced) status = "S!";
            return $"ATK:{u.CurrentStats.Attack,-2} HP:{u.CurrentStats.Health,-2} {status}";
        }
        private static string[] RenderBrain(GameState s)
        {
            var res = new List<string> { $"   MYŚLI BOTA P{s.ActivePlayerId}", "------------------------" };
            if (s.CurrentPhase == GamePhase.Combat) { res.Add(" [AUTO] Walka..."); return res.ToArray(); }
            foreach (var t in _lastThoughts.Take(15))
            {
                string prefix = t == _lastThoughts.First() ? ">>" : "  ";
                res.Add($"{prefix} {t.Description.PadRight(24)} | {t.Score:F0}");
                if (t == _lastThoughts.First() && !string.IsNullOrEmpty(t.DeepReasoning)) { res.Add($"   L {t.DeepReasoning}"); res.Add(""); }
            }
            return res.ToArray();
        }
        private static string FormatCmdDetailed(IGameCommand? c, GameState s)
        {
            if (c == null) return "...";
            if (c is PlayUnitCommand pu) return $"Graj {s.GetPlayer(pu.PlayerId).Hand.FirstOrDefault(x => x.InstanceId == pu.CardInstanceId)?.Definition.Name} (L{pu.TargetLineIndex + 1})";
            if (c is PlaySpellCommand ps) return $"Czar: {s.GetPlayer(ps.PlayerId).Hand.FirstOrDefault(x => x.InstanceId == ps.CardInstanceId)?.Definition.Name}";
            if (c is SelectTargetCommand st) { var target = s.Board.GetAllUnits().FirstOrDefault(u => u.InstanceId == st.TargetId); return $"Cel: {target?.Definition.Name ?? "Bohater"}"; }
            return "PAS (Koniec)";
        }
        private static void UpdateLogs(GameEngine e)
        {
            var all = e.Events.GetGlobalHistory().ToList();
            for (int i = _eventsSeenSoFar; i < all.Count; i++)
            {
                var evt = all[i];
                if (evt is UnitDamagedEvent d) _displayLogs.Enqueue($"> {(d.Source?.Definition.Name ?? "Efekt")} bije {(d.Unit?.Definition.Name ?? "Bohater")} za {d.Amount}");
                else if (evt is CardPlayedEvent cp) _displayLogs.Enqueue($"> P{cp.PlayerId} gra {cp.Card.Definition.Name}");
                else if (evt is UnitDiedEvent dd) _displayLogs.Enqueue($"> {dd.Unit.Definition.Name} zginął");
                if (_displayLogs.Count > 15) _displayLogs.Dequeue();
            }
            _eventsSeenSoFar = all.Count;
        }
        private static void PerformMulligan(GameEngine e, int pid, bool ctrl)
        {
            var p = e.CurrentState.GetPlayer(pid);
            var rej = p.Hand.Where(c => (ctrl && c.CurrentStats.BloodCost > 2) || (!ctrl && c.CurrentStats.BloodCost > 4)).Select(c => c.InstanceId).ToList();
            e.ExecuteCommand(new ConfirmMulliganCommand(pid, rej));
            LogToAudit($"[MULLIGAN] P{pid} wymienił {rej.Count} kart.");
        }
        private static List<CardInstance> CreateDeck(CardFactory f, int id, bool ctrl)
        {
            int[] ids = ctrl ? new[] { 4, 32, 24, 25, 26, 17, 6, 31, 15, 7 } : new[] { 12, 10, 11, 3, 9, 1, 2, 5, 18, 14 };
            var d = new List<CardInstance>();
            foreach (var cid in ids) for (int i = 0; i < 3; i++) d.Add(f.CreateCard(cid, id));
            return d;
        }
    }
}