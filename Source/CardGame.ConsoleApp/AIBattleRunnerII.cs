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
        private const int MaxLogLines = 6; // Rozmiar ramki logów
        private static int _eventsSeenSoFar = 0;
        private const int UI_WIDTH = 114;

        public static async Task RunAsync()
        {
            Console.Clear();
            Console.CursorVisible = false;
            Console.OutputEncoding = Encoding.UTF8;

            CardLibrary.Instance.LoadFromJson("Data/Cards/cards.json");
            var rng = new DeterministicRng(new Random().Next());
            var factory = new CardFactory(CardLibrary.Instance, rng);

            var deckA = CreateHighlanderDeck(factory, 1, 30);
            var deckB = CreateHighlanderDeck(factory, 2, 30);

            var state = GameState.Initial(1, deckA, deckB, rng);
            var engine = new GameEngine(state, rng.Seed);

            var ai1 = new AIPlayerController(engine, 1, new StandardStrategy());
            var ai2 = new AIPlayerController(engine, 2, new StandardStrategy());

            ai1.StartAutoPlay();
            ai2.StartAutoPlay();

            while (!engine.IsGameOver)
            {
                var allEvents = engine.Events.GetHistory().ToList();

                if (allEvents.Count > _eventsSeenSoFar)
                {
                    for (int i = _eventsSeenSoFar; i < allEvents.Count; i++)
                    {
                        string? logText = ParseEventToText(allEvents[i]);
                        if (logText != null)
                        {
                            _displayLogs.Enqueue(logText);
                            if (_displayLogs.Count > MaxLogLines) _displayLogs.Dequeue();

                            DrawNuclearUI(engine.CurrentState);
                            await Task.Delay(500); // Opóźnienie dla czytelności logów
                        }
                    }
                    _eventsSeenSoFar = allEvents.Count;
                }

                DrawNuclearUI(engine.CurrentState);
                await Task.Delay(200);
            }

            DrawNuclearUI(engine.CurrentState);
            Console.SetCursorPosition(0, 36);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n  === SYMULACJA ZAKOŃCZONA: ZWYCIĘSTWO GRACZA {engine.WinnerId ?? 0} ===");
            Console.ResetColor();
            Console.CursorVisible = true;
        }

        private static void DrawNuclearUI(GameState state)
        {
            StringBuilder sb = new StringBuilder();
            Console.SetCursorPosition(0, 0);

            // 1. HEADER (Główny pasek informacyjny)
            int roundNum = (state.TurnNumber + 1) / 2;
            string headerInfo = $" RUNDA: {roundNum} | FAZA: {state.CurrentPhase} | AKTYWNY: Gracz {state.ActivePlayerId} ";

            sb.AppendLine("╔" + new string('═', UI_WIDTH - 2) + "╗");
            sb.AppendLine("║" + CenterText(headerInfo, UI_WIDTH - 2) + "║");
            sb.AppendLine("╠" + new string('═', UI_WIDTH - 2) + "╣");

            // 2. STATYSTYKI GRACZA 2 (Nad jego kartami)
            string p2Info = $" [ ENEMY P2 ]  HP: {state.PlayerB.Health,2} | Blood: {state.PlayerB.CurrentBlood,2}/{state.PlayerB.MaxBlood,2} | Ręka: {state.PlayerB.Hand.Count} ";
            sb.AppendLine("║" + CenterText(p2Info, UI_WIDTH - 2) + "║");
            sb.AppendLine("║" + new string(' ', UI_WIDTH - 2) + "║");

            // 3. PLANSZA (Karty P2, vs, Karty P1)
            for (int row = 0; row < 11; row++)
            {
                string lineRow = "║     ";
                for (int col = 0; col < 4; col++)
                {
                    lineRow += GetRowSegment(row, state.Board.Lines[col]).PadRight(26);
                }
                sb.AppendLine(lineRow.PadRight(UI_WIDTH - 1) + "║");
            }

            // 4. STATYSTYKI GRACZA 1 (Pod jego kartami)
            sb.AppendLine("║" + new string(' ', UI_WIDTH - 2) + "║");
            string p1Info = $" [ FRIEND P1 ]  HP: {state.PlayerA.Health,2} | Blood: {state.PlayerA.CurrentBlood,2}/{state.PlayerA.MaxBlood,2} | Ręka: {state.PlayerA.Hand.Count} ";
            sb.AppendLine("║" + CenterText(p1Info, UI_WIDTH - 2) + "║");

            // 5. RAMKA LOGÓW (Na dole)
            sb.AppendLine("╠" + new string('═', UI_WIDTH - 2) + "╣");
            sb.AppendLine("║  DZIENNIK ZDARZEŃ:".PadRight(UI_WIDTH - 1) + "║");

            int displayed = 0;
            foreach (var log in _displayLogs)
            {
                string logLine = " » " + log;
                if (logLine.Length > UI_WIDTH - 6) logLine = logLine.Substring(0, UI_WIDTH - 9) + "...";
                sb.AppendLine("║  " + logLine.PadRight(UI_WIDTH - 6) + "║");
                displayed++;
            }
            // Dopełnienie pustych linii w ramce logów
            for (int i = 0; i < (MaxLogLines - displayed); i++)
                sb.AppendLine("║".PadRight(UI_WIDTH - 1) + "║");

            sb.AppendLine("╚" + new string('═', UI_WIDTH - 2) + "╝");

            Console.Write(sb.ToString());
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
            string sil = u.IsSilenced ? "S!" : "  ";
            return $"│ ATK:{u.CurrentStats.Attack,-2}  HP:{u.CurrentStats.Health,-2}  {sil} │";
        }

        private static string CenterText(string text, int width)
        {
            if (string.IsNullOrEmpty(text)) return new string(' ', width);
            int leftPadding = (width - text.Length) / 2;
            return text.PadLeft(leftPadding + text.Length).PadRight(width);
        }

        private static string? ParseEventToText(IGameEvent evt)
        {
            if (evt is UnitDamagedEvent ude)
            {
                string source = ude.Source?.Definition.Name ?? "Efekt";
                return ude.Unit == null
                    ? $"{source} uderza BOHATERA za {ude.Amount} DMG!"
                    : $"{source} zadaje {ude.Amount} DMG jednostce {ude.Unit.Definition.Name}";
            }
            if (evt is CardPlayedEvent cpe)
                return $"Gracz {cpe.PlayerId} zagrywa: {cpe.Card.Definition.Name}";
            if (evt is UnitDiedEvent ud)
                return $"ŚMIERĆ: {ud.Unit.Definition.Name} opuszcza pole bitwy";
            if (evt is UnitSacrificedEvent us)
                return $"OFIARA: {us.Unit.Definition.Name} został poświęcony";
            if (evt is StatusAppliedEvent sae)
                return $"STATUS: {sae.Status} nałożony na jednostkę";
            if (evt is CardDrawnEvent cde)
                return $"Gracz {cde.PlayerId} dobrał kartę";

            return null;
        }

        private static List<CardInstance> CreateHighlanderDeck(CardFactory factory, int ownerId, int size)
        {
            var rand = new Random();
            var availableIds = Enumerable.Range(1, 36).OrderBy(x => rand.Next()).Take(size);
            return availableIds.Select(id => factory.CreateCard(id, ownerId)).ToList();
        }
    }
}