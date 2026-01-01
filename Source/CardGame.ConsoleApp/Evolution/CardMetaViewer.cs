using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using CardGame.Core.Cards.Data;

namespace CardGame.ConsoleApp.Evolution
{
    public static class CardMetaViewer
    {
        private const int UI_WIDTH = 158;
        private const int PAGE_SIZE = 30; // Ile kart widzimy naraz
        private enum SortMode { WinRate, Usage, Density, Games }
        private static SortMode _currentSort = SortMode.WinRate;
        private static int _scrollOffset = 0;

        public static void ViewMeta()
        {
            string path = "Analytics/current_meta.json";
            if (!File.Exists(path))
            {
                Console.WriteLine("No analytics data found. Run evolution for at least 10 generations!");
                Console.ReadKey();
                return;
            }

            bool running = true;
            while (running)
            {
                var data = JsonSerializer.Deserialize<List<CardAnalytics.CardEntry>>(File.ReadAllText(path));
                if (data == null) return;

                Console.Clear();
                DrawUI(data);

                var key = Console.ReadKey(true);
                switch (key.Key)
                {
                    case ConsoleKey.D1: _currentSort = SortMode.WinRate; _scrollOffset = 0; break;
                    case ConsoleKey.D2: _currentSort = SortMode.Usage; _scrollOffset = 0; break;
                    case ConsoleKey.D3: _currentSort = SortMode.Density; _scrollOffset = 0; break;
                    case ConsoleKey.D4: _currentSort = SortMode.Games; _scrollOffset = 0; break;
                    case ConsoleKey.DownArrow:
                        if (_scrollOffset + PAGE_SIZE < data.Count) _scrollOffset++;
                        break;
                    case ConsoleKey.UpArrow:
                        if (_scrollOffset > 0) _scrollOffset--;
                        break;
                    case ConsoleKey.Escape: running = false; break;
                }
            }
        }

        private static void DrawUI(List<CardAnalytics.CardEntry> data)
        {
            IEnumerable<CardAnalytics.CardEntry> sorted = _currentSort switch
            {
                SortMode.WinRate => data.OrderByDescending(d => d.WinRate),
                SortMode.Usage => data.OrderByDescending(d => d.UsageRate),
                SortMode.Density => data.OrderByDescending(d => d.AvgDensity),
                SortMode.Games => data.OrderByDescending(d => d.TotalGames),
                _ => data
            };

            var list = sorted.ToList();
            var viewPort = list.Skip(_scrollOffset).Take(PAGE_SIZE).ToList();

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("╔" + new string('═', UI_WIDTH - 2) + "╗");
            sb.AppendLine("║" + CenterText($"CARD META ANALYTICS - SORTED BY {_currentSort.ToString().ToUpper()}", UI_WIDTH - 2) + "║");
            sb.AppendLine("║" + CenterText($"Showing cards {_scrollOffset + 1} - {Math.Min(_scrollOffset + PAGE_SIZE, list.Count)} of {list.Count}", UI_WIDTH - 2) + "║");

            sb.AppendLine("╠" + new string('═', 6) + "╦" + new string('═', 25) + "╦" + new string('═', 15) + "╦" + new string('═', 15) + "╦" + new string('═', 15) + "╦" + new string('═', 20) + "╦" + new string('═', UI_WIDTH - 105) + "╣");
            sb.AppendLine("║  ID  ║ NAME                    ║ WIN RATE      ║ USAGE RATE    ║ AVG DENSITY   ║ WINS / GAMES       ║ PERFORMANCE TAG      ║");
            sb.AppendLine("╠" + new string('═', 6) + "╬" + new string('═', 25) + "╬" + new string('═', 15) + "╬" + new string('═', 15) + "╬" + new string('═', 15) + "╬" + new string('═', 20) + "╬" + new string('═', UI_WIDTH - 105) + "╣");

            foreach (var card in viewPort)
            {
                string id = card.Id.ToString().PadRight(4);
                string name = Truncate(card.Name, 23).PadRight(23);
                string wr = $"{(card.WinRate * 100):F1}%".PadRight(13);
                string usage = $"{(card.UsageRate * 100):F1}%".PadRight(13);
                string dens = $"{card.AvgDensity:F2}".PadRight(13);
                string stats = $"{card.TotalWins} / {card.TotalGames}".PadRight(18);
                string tag = GetPerformanceTag(card);

                sb.AppendLine($"║ {id} ║ {name} ║ {wr} ║ {usage} ║ {dens} ║ {stats} ║ {tag.PadRight(UI_WIDTH - 107)} ║");
            }

            // Dopełnienie pustymi liniami, jeśli lista jest krótka
            for (int i = viewPort.Count; i < PAGE_SIZE; i++)
                sb.AppendLine("║      ║                         ║               ║               ║               ║                    ║                      ║");

            sb.AppendLine("╚" + new string('═', 6) + "╩" + new string('═', 25) + "╩" + new string('═', 15) + "╩" + new string('═', 15) + "╩" + new string('═', 15) + "╩" + new string('═', 20) + "╩" + new string('═', UI_WIDTH - 105) + "╝");

            Console.Write(sb.ToString());
            Console.WriteLine(CenterText("[1] WinRate | [2] Usage | [3] Density | [4] TotalGames | [UP/DOWN] Scroll | [ESC] Back", UI_WIDTH));
        }

        private static string GetPerformanceTag(CardAnalytics.CardEntry card)
        {
            if (card.UsageRate > 0.6 && card.WinRate > 0.48) return "!!! OVERPOWERED !!!";
            if (card.UsageRate > 0.5) return "Meta Staple";
            if (card.WinRate > 0.5) return "Hidden Gem";
            if (card.UsageRate < 0.1) return "Underplayed";
            return "Balanced";
        }

        private static string Truncate(string value, int maxChars) => value.Length <= maxChars ? value : value.Substring(0, maxChars - 3) + "...";
        private static string CenterText(string text, int width)
        {
            int leftPadding = (width - text.Length) / 2;
            return new string(' ', Math.Max(0, leftPadding)) + text + new string(' ', Math.Max(0, width - text.Length - leftPadding));
        }
    }
}