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
        private const int UI_WIDTH = 155;
        private static StringBuilder _fullAudit = new StringBuilder();
        private static int _p1Wins = 0, _p2Wins = 0, _gamesPlayed = 0;

        public static async Task RunAsync()
        {
            Console.Clear();
            while (true)
            {
                _fullAudit.Clear();
                _displayLogs.Clear();
                _eventsSeenSoFar = 0;

                CardLibrary.Instance.LoadFromJson("Data/Cards/cards.json");
                var rng = new DeterministicRng(new Random().Next());
                var factory = new CardFactory(CardLibrary.Instance, rng);

                bool p1IsCtrl = _gamesPlayed % 2 == 0;
                var engine = new GameEngine(GameState.Initial(1, CreateDeck(factory, 1, p1IsCtrl), CreateDeck(factory, 2, !p1IsCtrl), rng), rng.Seed);
                var ai1 = new AIPlayerController(engine, 1, new BalanceStrategy());
                var ai2 = new AIPlayerController(engine, 2, new BalanceStrategy());

                _fullAudit.AppendLine($"=== MECZ NR {_gamesPlayed + 1} ===");

                while (!engine.IsGameOver)
                {
                    var state = engine.CurrentState;
                    UpdateLogs(engine);

                    if (state.CurrentPhase != GamePhase.Mulligan && state.CurrentPhase != GamePhase.Combat)
                    {
                        var activeAI = state.ActivePlayerId == 1 ? ai1 : ai2;
                        _lastThoughts = activeAI.Solver.FindBestMoves(state);
                    }

                    DrawUI(state, p1IsCtrl);

                    // SYSTEM KROKOWY (Omija Combat i Mulligan)
                    if (state.CurrentPhase != GamePhase.Mulligan && state.CurrentPhase != GamePhase.Combat)
                    {
                        var key = Console.ReadKey(true);
                        if (key.Key == ConsoleKey.Escape) return;
                        if (key.Key == ConsoleKey.A)
                        {
                            File.WriteAllText("battle_audit.txt", _fullAudit.ToString());
                            _displayLogs.Enqueue("!!! DUMP DO battle_audit.txt OK !!!");
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
                        await Task.Delay(100);
                    }
                    else
                    {
                        var best = _lastThoughts.FirstOrDefault();
                        if (best != null)
                        {
                            _fullAudit.AppendLine($"[T{state.TurnNumber} P{state.ActivePlayerId}] Ruch: {FormatCmdDetailed(best.Command, state)} (Score: {best.Score:F1})");
                            engine.ExecuteCommand(best.Command);
                        }
                        else
                        {
                            engine.ExecuteCommand(new EndPhaseCommand(state.ActivePlayerId));
                        }
                    }
                }
                if (engine.WinnerId == 1) _p1Wins++; else _p2Wins++;
                _gamesPlayed++;
                await Task.Delay(2000);
            }
        }

        private static void DrawUI(GameState s, bool p1Ctrl)
        {
            StringBuilder sb = new StringBuilder();
            Console.SetCursorPosition(0, 0);

            string score = $"[ ZWYCIĘSTWA P1: {_p1Wins} | P2: {_p2Wins} ]";
            string info = $" RUNDA: {(s.TurnNumber + 3) / 4} | P1:{(p1Ctrl ? "[CTRL]" : "[SACR]")} P2:{(!p1Ctrl ? "[CTRL]" : "[SACR]")}";
            sb.AppendLine("╔" + new string('═', UI_WIDTH - 2) + "╗");
            sb.AppendLine("║ " + info.PadRight(UI_WIDTH - score.Length - 4) + score + " ║");
            sb.AppendLine("╠" + new string('═', 106) + "╦" + new string('═', UI_WIDTH - 109) + "╣");

            string[] board = RenderBoard(s);
            string[] brain = RenderBrain(s);

            for (int i = 0; i < 38; i++)
            {
                string l = i < board.Length ? board[i] : "";
                string r = i < brain.Length ? brain[i] : "";
                sb.AppendLine($"║ {l.PadRight(104)} ║ {r.PadRight(UI_WIDTH - 111)} ║");
            }
            sb.AppendLine("╚" + new string('═', 106) + "╩" + new string('═', UI_WIDTH - 109) + "╝");
            Console.Write(sb.ToString());
        }

        private static string[] RenderBoard(GameState s)
        {
            var res = new List<string>();
            res.Add($"[ P2 ENEMY | HP: {s.PlayerB.Health,2} | KREW: {s.PlayerB.CurrentBlood}/{s.PlayerB.MaxBlood} ]");
            res.Add(WrapHand(s.PlayerB.Hand, "P2"));
            res.Add("");
            for (int r = 0; r < 11; r++)
            {
                string row = "  ";
                for (int c = 0; c < 4; c++) row += GetRowSegment(r, s.Board.Lines[c]).PadRight(25);
                res.Add(row);
            }
            res.Add("");
            res.Add($"[ P1 YOU   | HP: {s.PlayerA.Health,2} | KREW: {s.PlayerA.CurrentBlood}/{s.PlayerA.MaxBlood} ]");
            res.Add(WrapHand(s.PlayerA.Hand, "P1"));
            res.Add(new string('-', 104));
            res.Add("DZIENNIK:");
            res.AddRange(_displayLogs.Reverse().Take(8));
            return res.ToArray();
        }

        private static string WrapHand(IReadOnlyList<CardInstance> hand, string p)
        {
            var names = hand.Select(c => $"({c.CurrentStats.BloodCost}){c.Definition.Name}");
            return $" Ręka {p}: [{string.Join(", ", names)}]";
        }

        private static string GetRowSegment(int row, Line line)
        {
            return row switch
            {
                0 => "┌───────────────────┐",
                1 => RenderUnitLine(line.Player2Unit),
                2 => RenderUnitStats(line.Player2Unit),
                3 => "└───────────────────┘",
                4 => "         ┃          ",
                5 => "         VS         ",
                6 => "         ┃          ",
                7 => "┌───────────────────┐",
                8 => RenderUnitLine(line.Player1Unit),
                9 => RenderUnitStats(line.Player1Unit),
                10 => "└───────────────────┘",
                _ => ""
            };
        }

        private static string RenderUnitLine(CardInstance? u) => u == null ? "│     [ WOLNE ]     │" : $"│ {u.Definition.Name.PadRight(17).Substring(0, 17)} │";
        private static string RenderUnitStats(CardInstance? u) => u == null ? "│                   │" : $"│ ATK:{u.CurrentStats.Attack,-2} HP:{u.CurrentStats.Health,-2} {(u.CurrentStats.Keywords.Contains(Keyword.Marked) ? "!!" : "  ")} │";

        private static string[] RenderBrain(GameState s)
        {
            var res = new List<string> { $"   MYŚLI BOTA P{s.ActivePlayerId}", "------------------------" };
            if (s.CurrentPhase == GamePhase.Combat) { res.Add(" [AUTO] Walka..."); return res.ToArray(); }
            foreach (var t in _lastThoughts.Take(25))
            {
                string prefix = t == _lastThoughts.First() ? ">>" : "  ";
                res.Add($"{prefix} {FormatCmdDetailed(t.Command, s).PadRight(24)} | {t.Score:F0}");
            }
            return res.ToArray();
        }

        private static string FormatCmdDetailed(IGameCommand? c, GameState s)
        {
            if (c == null) return "...";
            if (c is PlayUnitCommand pu)
            {
                var card = s.GetPlayer(pu.PlayerId).Hand.FirstOrDefault(x => x.InstanceId == pu.CardInstanceId);
                return $"Graj {card?.Definition.Name} (L{pu.TargetLineIndex})";
            }
            if (c is PlaySpellCommand ps)
            {
                var card = s.GetPlayer(ps.PlayerId).Hand.FirstOrDefault(x => x.InstanceId == ps.CardInstanceId);
                return $"Czar {card?.Definition.Name}";
            }
            if (c is SelectTargetCommand st)
            {
                var target = s.Board.GetAllUnits().FirstOrDefault(u => u.InstanceId == st.TargetId);
                return $"Cel: {target?.Definition.Name ?? "Bohater"}";
            }
            return "KONIEC TURY";
        }

        private static void UpdateLogs(GameEngine e)
        {
            var all = e.Events.GetGlobalHistory().ToList();
            for (int i = _eventsSeenSoFar; i < all.Count; i++)
            {
                var evt = all[i];
                if (evt is UnitDamagedEvent d)
                {
                    string src = d.Source?.Definition.Name ?? "Efekt";
                    string trg = d.Unit?.Definition.Name ?? "Bohatera";
                    _displayLogs.Enqueue($"> {src} bije {trg} za {d.Amount}");
                }
                else if (evt is CardPlayedEvent cp) _displayLogs.Enqueue($"> P{cp.PlayerId} gra {cp.Card.Definition.Name}");
                else if (evt is UnitDiedEvent dd) _displayLogs.Enqueue($"> {dd.Unit.Definition.Name} zniszczony");
                if (_displayLogs.Count > 15) _displayLogs.Dequeue();
            }
            _eventsSeenSoFar = all.Count;
        }

        private static void PerformMulligan(GameEngine e, int pid, bool ctrl)
        {
            var p = e.CurrentState.GetPlayer(pid);
            var rej = p.Hand.Where(c => (ctrl && c.CurrentStats.BloodCost > 2) || (!ctrl && c.CurrentStats.BloodCost > 4)).Select(c => c.InstanceId).ToList();
            e.ExecuteCommand(new ConfirmMulliganCommand(pid, rej));
        }

        private static List<CardInstance> CreateDeck(CardFactory f, int id, bool ctrl)
        {
            int[] ids = ctrl ? new[] { 4, 32, 24, 25, 26, 17, 6, 31, 15, 7 } : new[] { 12, 10, 11, 3, 9, 1, 2, 5, 16, 14 };
            var d = new List<CardInstance>();
            foreach (var cid in ids) for (int i = 0; i < 3; i++) d.Add(f.CreateCard(cid, id));
            return d;
        }
    }
}