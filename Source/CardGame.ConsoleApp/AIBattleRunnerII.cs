using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CardGame.Core.AI;
using CardGame.Core.AI.Strategies;
using CardGame.Core.Application;
using CardGame.Core.Cards.Data;
using CardGame.Core.Cards.Factories;
using CardGame.Core.Cards.Models;
using CardGame.Core.State.Models;
using CardGame.Core.State.Enums;
using CardGame.Core.Events;
using CardGame.Core.Events.Interfaces;

namespace CardGame.ConsoleApp
{
    public static class AIBattleRunnerII
    {
        private static readonly Queue<string> _displayLogs = new();
        private const int MaxLogLines = 12; // Wydłużona tabela logów
        private static int _eventsSeenSoFar = 0;
        private const int UI_WIDTH = 114;

        public static async Task RunAsync()
        {
            _displayLogs.Clear();
            _eventsSeenSoFar = 0;

            Console.Clear();
            Console.CursorVisible = false;
            Console.OutputEncoding = Encoding.UTF8;

            CardLibrary.Instance.LoadFromJson("Data/Cards/cards.json");
            var rng = new DeterministicRng(new Random().Next());
            var factory = new CardFactory(CardLibrary.Instance, rng);

            //var deckA = CreateHighlanderDeck(factory, 1, 30);
            //var deckB = CreateHighlanderDeck(factory, 2, 30);

            var deckA = CreateDeckForBot(factory, 1, true);  // Gracz 1: Control
            var deckB = CreateDeckForBot(factory, 2, false); // Gracz 2: Sacrifice

            var state = GameState.Initial(1, deckA, deckB, rng);
            var engine = new GameEngine(state, rng.Seed);

            var ai1 = new AIPlayerController(engine, 1, new StandardStrategy());
            var ai2 = new AIPlayerController(engine, 2, new StandardStrategy());

            ai1.StartAutoPlay();
            ai2.StartAutoPlay();

            while (!engine.IsGameOver)
            {
                var allEvents = engine.Events.GetGlobalHistory().ToList();

                if (allEvents.Count > _eventsSeenSoFar)
                {
                    for (int i = _eventsSeenSoFar; i < allEvents.Count; i++)
                    {
                        string? logText = ParseEventToText(allEvents[i], engine.CurrentState);
                        if (logText != null)
                        {
                            _displayLogs.Enqueue(logText);
                            while (_displayLogs.Count > MaxLogLines) _displayLogs.Dequeue();

                            DrawNuclearUI(engine.CurrentState);
                            await Task.Delay(350); // Lekko przyspieszona animacja przy dużej liczbie eventów
                        }
                    }
                    _eventsSeenSoFar = allEvents.Count;
                }

                DrawNuclearUI(engine.CurrentState);
                await Task.Delay(100);
            }

            DrawNuclearUI(engine.CurrentState);
            Console.SetCursorPosition(0, 45);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n  === KONIEC SYMULACJI: ZWYCIĘSTWO GRACZA {engine.WinnerId ?? 0} ===");
            Console.ResetColor();
            Console.CursorVisible = true;
        }

        private static void DrawNuclearUI(GameState state)
        {
            StringBuilder sb = new StringBuilder();
            Console.SetCursorPosition(0, 0);

            int roundNum = (state.TurnNumber + 1) / 2;
            string headerInfo = $" RUNDA: {roundNum} | FAZA: {state.CurrentPhase} | AKTYWNY: Gracz {state.ActivePlayerId} ";

            sb.AppendLine("╔" + new string('═', UI_WIDTH - 2) + "╗");
            sb.AppendLine("║" + CenterText(headerInfo, UI_WIDTH - 2) + "║");
            sb.AppendLine("╠" + new string('═', UI_WIDTH - 2) + "╣");

            string p2Info = $" [ ENEMY P2 ]  HP: {state.PlayerB.Health,2} | Blood: {state.PlayerB.CurrentBlood,2}/{state.PlayerB.MaxBlood,2} ";
            var (p2Hand1, p2Hand2) = GetHandDisplayStrings(state.PlayerB);
            sb.AppendLine("║" + CenterText(p2Info, UI_WIDTH - 2) + "║");
            sb.AppendLine("║" + CenterText(p2Hand1, UI_WIDTH - 2) + "║");
            sb.AppendLine("║" + CenterText(p2Hand2, UI_WIDTH - 2) + "║");
            sb.AppendLine("║" + new string(' ', UI_WIDTH - 2) + "║");

            for (int row = 0; row < 11; row++)
            {
                string lineRow = "║     ";
                for (int col = 0; col < 4; col++)
                {
                    lineRow += GetRowSegment(row, state.Board.Lines[col]).PadRight(26);
                }
                sb.AppendLine(lineRow.PadRight(UI_WIDTH - 1) + "║");
            }

            sb.AppendLine("║" + new string(' ', UI_WIDTH - 2) + "║");
            string p1Info = $" [ FRIEND P1 ]  HP: {state.PlayerA.Health,2} | Blood: {state.PlayerA.CurrentBlood,2}/{state.PlayerA.MaxBlood,2} ";
            var (p1Hand1, p1Hand2) = GetHandDisplayStrings(state.PlayerA);
            sb.AppendLine("║" + CenterText(p1Info, UI_WIDTH - 2) + "║");
            sb.AppendLine("║" + CenterText(p1Hand1, UI_WIDTH - 2) + "║");
            sb.AppendLine("║" + CenterText(p1Hand2, UI_WIDTH - 2) + "║");

            sb.AppendLine("╠" + new string('═', UI_WIDTH - 2) + "╣");
            sb.AppendLine("║  DZIENNIK ZDARZEŃ:".PadRight(UI_WIDTH - 1) + "║");

            int displayed = 0;
            foreach (var log in _displayLogs)
            {
                sb.AppendLine("║  » " + log.PadRight(UI_WIDTH - 7) + "║");
                displayed++;
            }
            for (int i = 0; i < (MaxLogLines - displayed); i++)
                sb.AppendLine("║".PadRight(UI_WIDTH - 1) + "║");

            sb.AppendLine("╚" + new string('═', UI_WIDTH - 2) + "╝");

            Console.Write(sb.ToString());
        }

        private static string? ParseEventToText(IGameEvent evt, GameState state)
        {
            // --- STATUSY I KLUCZOWE MECHANIKI ---
            if (evt is StatusAppliedEvent sae)
            {
                var target = state.Board.GetAllUnits().FirstOrDefault(u => u.InstanceId == sae.TargetUnitId);
                string name = target?.Definition.Name ?? $"Jedn.{sae.TargetUnitId}";
                return $"[STATUS] {sae.Status.ToString().ToUpper()} nałożony na {name}";
            }

            // --- OBRAŻENIA (DETEKCJA KONTEKSTU) ---
            if (evt is UnitDamagedEvent ude)
            {
                string src = ude.Source?.Definition.Name ?? "Efekt";
                string target = ude.Unit?.Definition.Name ?? "BOHATER";

                if (ude.Source == null && ude.Amount == 1) return $"[BURN] Ogień parzy {target} za 1 DMG";
                if (ude.Source != null && ude.Source.CurrentStats.Keywords.Contains(Keyword.SplashDamage))
                    return $"[SPLASH] {src} rani sąsiedni cel: {target} ({ude.Amount} DMG)";

                return $"[DMG] {src} zadaje {ude.Amount} DMG -> {target}";
            }

            // --- ŚMIERĆ I POŚWIĘCENIE ---
            if (evt is UnitDiedEvent ud)
            {
                string killerInfo = ud.KillerInstanceId.HasValue ? $" przez Jedn.{ud.KillerInstanceId}" : "";
                return $"[ŚMIERĆ] {ud.Unit.Definition.Name} ginie{killerInfo}";
            }

            if (evt is UnitSacrificedEvent us)
                return $"[OFIARA] {us.Unit.Definition.Name} został złożony w ofierze";

            // --- ZAGRANIA I DOBIERY ---
            if (evt is CardPlayedEvent cpe)
                return $"[AKCJA] P{cpe.PlayerId} zagrywa {cpe.Card.Definition.Name}";

            if (evt is CardDrawnEvent cde)
                return $"[TALIA] Gracz {cde.PlayerId} dobrał kartę";

            // --- SYSTEMOWE ---
            if (evt is TurnStartedEvent tse)
                return $"--- TURA GRACZA {tse.ActivePlayerId} ---";

            if (evt is PreLineCombatEvent ple)
                return $"[WALKA] Rozliczanie linii {ple.LineIndex}...";

            if (evt is TargetSelectedEvent tse2)
                return $"[INTERAKCJA] Wybrano cel ID: {tse2.SelectedTargetId}";

            if (evt is GameOverEvent goe)
                return $"!!! KONIEC GRY: ZWYCIĘZCA P{goe.WinnerId} !!!";

            return null;
        }

        private static (string line1, string line2) GetHandDisplayStrings(PlayerState player)
        {
            if (player.Hand.Count == 0) return ("[ RĘKA PUSTA ]", "");
            var cardStrings = player.Hand.Select(c => $"({c.CurrentStats.BloodCost}) {c.Definition.Name}").ToList();
            string fullString = string.Join(", ", cardStrings);
            int maxLineLen = UI_WIDTH - 12;

            if (fullString.Length <= maxLineLen) return ("[ " + fullString + " ]", "");

            int cardsInFirstLine = 0;
            string currentLine = "";
            for (int i = 0; i < cardStrings.Count; i++)
            {
                string nextPart = (currentLine == "" ? "" : ", ") + cardStrings[i];
                if ((currentLine + nextPart).Length > maxLineLen) { cardsInFirstLine = i; break; }
                currentLine += nextPart;
            }

            string l1 = "[ " + string.Join(", ", cardStrings.Take(cardsInFirstLine)) + ",";
            string l2 = "  " + string.Join(", ", cardStrings.Skip(cardsInFirstLine)) + " ]";
            return (l1, l2);
        }

        private static string GetRowSegment(int row, Line line)
        {
            return row switch
            {
                0 => "╭────────────────────╮",
                1 => RenderUnitName(line.Player2Unit),
                2 => RenderUnitStats(line.Player2Unit),
                3 => "╰────────────────────╯",
                4 => "          ┃           ",
                5 => "          VS          ",
                6 => "          ┃           ",
                7 => "╭────────────────────╮",
                8 => RenderUnitName(line.Player1Unit),
                9 => RenderUnitStats(line.Player1Unit),
                10 => "╰────────────────────╯",
                _ => ""
            };
        }

        private static string RenderUnitName(CardInstance? u)
        {
            if (u == null) return "│      [ WOLNE ]     │";
            string name = u.Definition.Name.Length > 16 ? u.Definition.Name.Substring(0, 16) : u.Definition.Name;
            return $"│ {name,-18} │";
        }

        private static string RenderUnitStats(CardInstance? u)
        {
            if (u == null) return "│                    │";
            string sil = u.IsSilenced ? "S!" : (u.CurrentStats.Keywords.Contains(Keyword.Stunned) ? "Zz" : "  ");
            return $"│ ATK:{u.CurrentStats.Attack,-2}  HP:{u.CurrentStats.Health,-2}  {sil} │";
        }

        private static string CenterText(string text, int width)
        {
            if (string.IsNullOrEmpty(text)) return new string(' ', width);
            int leftPadding = (width - text.Length) / 2;
            return text.PadLeft(Math.Max(0, leftPadding) + text.Length).PadRight(width);
        }

        private static List<CardInstance> CreateHighlanderDeck(CardFactory factory, int ownerId, int size)
        {
            var rand = new Random();
            var availableIds = Enumerable.Range(1, 36).OrderBy(x => rand.Next()).Take(size);
            return availableIds.Select(id => factory.CreateCard(id, ownerId)).ToList();
        }
        private static List<CardInstance> CreateDeckForBot(CardFactory factory, int ownerId, bool isControlDeck)
        {
            var deck = new List<CardInstance>();

          
            int[] controlIds = { 4, 32, 24, 25, 37, 21, 6, 31, 15, 7 };
            int[] sacrificeIds = { 12, 10, 11, 3, 9, 1, 2, 5, 16, 14 };

            int[] selectedIds = isControlDeck ? controlIds : sacrificeIds;

            foreach (var id in selectedIds)
            {
                for (int i = 0; i < 3; i++)
                {
                    deck.Add(factory.CreateCard(id, ownerId));
                }
            }
            return deck;
        }
    }
}