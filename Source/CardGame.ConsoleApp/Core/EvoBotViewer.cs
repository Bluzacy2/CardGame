using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using CardGame.ConsoleApp.Evolution.V1_Legacy;
using CardGame.Core.Cards.Data;

namespace CardGame.ConsoleApp.Core
{
    public static class EvoBotViewer
    {
        private const int UI_WIDTH = 158;
        private const int LEFT_PANEL_WIDTH = 110;
        private const int COLUMN_WIDTH = 34;
        private static HallOfFame _loadedHof = new();
        private static List<GeneticIndividual> _allViewableBots = new();
        private static int _currentIndex = 0;

        public static void ViewBestBot()
        {
            _loadedHof.Load();
            _allViewableBots.Clear();

            string masterPath = "best_bot_dna.json";
            GeneticIndividual realMaster = null;

            // 1. Wczytaj Mastera
            if (File.Exists(masterPath))
            {
                realMaster = JsonSerializer.Deserialize<GeneticIndividual>(File.ReadAllText(masterPath));
                if (realMaster != null)
                {
                    // Tworzymy kopię widokową
                    var viewMaster = new GeneticIndividual(realMaster.StrategyDNA, realMaster.DeckDNA, realMaster.Generation);
                    viewMaster.Id = "MASTER";
                    viewMaster.Wins = realMaster.Wins;
                    viewMaster.GamesPlayed = realMaster.GamesPlayed;
                    viewMaster.Fitness = realMaster.Fitness;
                    _allViewableBots.Add(viewMaster);
                }
            }

            // 2. Dodaj Championów z HoF, ale TYLKO jeśli nie są identyczni z Masterem
            foreach (var champ in _loadedHof.Champions)
            {
                bool isDuplicateOfMaster = false;
                if (realMaster != null)
                {
                    // Sprawdzamy różnicę DNA - jeśli suma różnic jest bliska 0, to ten sam bot
                    float dnaDiff = 0;
                    for (int i = 0; i < realMaster.StrategyDNA.Length; i++)
                        dnaDiff += Math.Abs(realMaster.StrategyDNA[i] - champ.StrategyDNA[i]);

                    if (dnaDiff < 0.01f) isDuplicateOfMaster = true;
                }

                // Dodaj tylko jeśli unikalny i nie mamy jeszcze 5 "innych" stylów
                if (!isDuplicateOfMaster && _allViewableBots.Count < 5)
                {
                    _allViewableBots.Add(champ);
                }
            }

            if (_allViewableBots.Count == 0)
            {
                Console.WriteLine("No bots found to display.");
                Console.ReadKey();
                return;
            }

            _currentIndex = 0;
            bool browsing = true;
            while (browsing)
            {
                Console.Clear();
                DrawUI(_allViewableBots[_currentIndex]);

                // Centrowana i obniżona stopka
                string footer = $" [Viewing {_currentIndex + 1} of {_allViewableBots.Count}] | Use ARROWS to switch | ESC to return ";
                Console.SetCursorPosition(0, 44); // Linijka niżej niż ramka
                Console.WriteLine(CenterText(footer, UI_WIDTH));

                var key = Console.ReadKey(true);
                if (key.Key == ConsoleKey.RightArrow) _currentIndex = (_currentIndex + 1) % _allViewableBots.Count;
                else if (key.Key == ConsoleKey.LeftArrow) _currentIndex = (_currentIndex - 1 + _allViewableBots.Count) % _allViewableBots.Count;
                else if (key.Key == ConsoleKey.Escape) browsing = false;
                else if (char.IsDigit(key.KeyChar))
                {
                    int num = (int)char.GetNumericValue(key.KeyChar) - 1;
                    if (num >= 0 && num < _allViewableBots.Count) _currentIndex = num;
                }
            }
        }

        private static void DrawUI(GeneticIndividual bot)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("╔" + new string('═', UI_WIDTH - 2) + "╗");

            string winRateStr = bot.GamesPlayed > 0 ? $"{bot.WinRate * 100:F1}%" : "N/A";
            string statsHeader = $" BOT PROFILE - ID: {bot.Id} | GEN: {bot.Generation} | WR: {winRateStr} ({bot.Wins}/{bot.GamesPlayed}) | FIT: {bot.Fitness:F0} ";

            sb.AppendLine("║" + CenterText(statsHeader, UI_WIDTH - 2) + "║");
            sb.AppendLine("╠" + new string('═', LEFT_PANEL_WIDTH - 1) + "╦" + new string('═', UI_WIDTH - LEFT_PANEL_WIDTH - 2) + "╣");

            var genes = GetSortedGenes(bot.StrategyDNA).Take(30).ToList();
            string[] dnaRows = new string[10];
            for (int i = 0; i < 10; i++)
            {
                var g1 = genes.Count > i ? genes[i] : null;
                var g2 = genes.Count > i + 10 ? genes[i + 10] : null;
                var g3 = genes.Count > i + 20 ? genes[i + 20] : null;
                dnaRows[i] = $"{FormatGene(g1).PadRight(COLUMN_WIDTH)} │ {FormatGene(g2).PadRight(COLUMN_WIDTH)} │ {FormatGene(g3).PadRight(COLUMN_WIDTH)}";
            }

            var deckGrouped = bot.DeckDNA
                .GroupBy(id => id)
                .Select(g => {
                    var def = CardLibrary.Instance.GetCard(g.Key);
                    return new { def.Name, def.Cost, Count = g.Count() };
                })
                .OrderBy(c => c.Cost).ThenBy(c => c.Name)
                .Select(c => $"[{c.Cost}] {c.Name} x{c.Count}").ToList();

            for (int i = 0; i < 38; i++)
            {
                string leftContent = "";
                string rightContent = "";

                if (i == 0) leftContent = " --- TOP 30 STRATEGY DNA (PRIORITIES) ---";
                else if (i >= 2 && i < 12) leftContent = "  " + dnaRows[i - 2];
                else if (i == 14) leftContent = " --- BOT DECK COMPOSITION (SORTED BY COST) ---";
                else if (i >= 16)
                {
                    int rowIdx = i - 16;
                    int startIdx = rowIdx * 3;
                    if (startIdx < deckGrouped.Count)
                    {
                        var d1 = deckGrouped.Count > startIdx ? Truncate(deckGrouped[startIdx], COLUMN_WIDTH - 2) : "";
                        var d2 = deckGrouped.Count > startIdx + 1 ? Truncate(deckGrouped[startIdx + 1], COLUMN_WIDTH - 2) : "";
                        var d3 = deckGrouped.Count > startIdx + 2 ? Truncate(deckGrouped[startIdx + 2], COLUMN_WIDTH - 2) : "";
                        leftContent = $"  {d1.PadRight(COLUMN_WIDTH)}   {d2.PadRight(COLUMN_WIDTH)}   {d3.PadRight(COLUMN_WIDTH)}";
                    }
                }

                // PANEL BOCZNY (Hall of Fame)
                if (i == 0) rightContent = " HALL OF FAME (DIVERSE)";
                else if (i >= 2)
                {
                    int sideIdx = (i - 2) / 2;
                    if (sideIdx < _allViewableBots.Count)
                    {
                        var sideBot = _allViewableBots[sideIdx];
                        bool isCurrent = sideBot.Id == bot.Id; // Podświetlenie aktualnie wybranego

                        if ((i - 2) % 2 == 0)
                        {
                            string prefix = isCurrent ? ">> " : "   ";
                            rightContent = $"{prefix}{sideIdx + 1}. [{sideBot.Id}] {GetStrategyTag(sideBot)}";
                        }
                        else
                        {
                            rightContent = $"      Fit: {sideBot.Fitness:F0}";
                        }
                    }
                }

                string leftFinal = leftContent.PadRight(LEFT_PANEL_WIDTH - 3);
                if (leftFinal.Length > LEFT_PANEL_WIDTH - 3) leftFinal = leftFinal.Substring(0, LEFT_PANEL_WIDTH - 3);
                string rightFinal = rightContent.PadRight(UI_WIDTH - LEFT_PANEL_WIDTH - 5);
                if (rightFinal.Length > UI_WIDTH - LEFT_PANEL_WIDTH - 5) rightFinal = rightFinal.Substring(0, UI_WIDTH - LEFT_PANEL_WIDTH - 5);

                sb.AppendLine($"║ {leftFinal} ║ {rightFinal} ║");
            }

            sb.AppendLine("╚" + new string('═', LEFT_PANEL_WIDTH - 1) + "╩" + new string('═', UI_WIDTH - LEFT_PANEL_WIDTH - 2) + "╝");
            Console.Write(sb.ToString());
        }

        private static List<GeneInfo> GetSortedGenes(float[] dna)
        {
            var geneNames = typeof(DNA).GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(int))
                .ToDictionary(f => (int)f.GetValue(null), f => f.Name);

            var list = new List<GeneInfo>();
            for (int i = 0; i < dna.Length; i++)
                if (geneNames.TryGetValue(i, out string name))
                    list.Add(new GeneInfo { Name = name, Value = dna[i] });
            return list.OrderByDescending(g => g.Value).ToList();
        }

        private static string FormatGene(GeneInfo g)
        {
            if (g == null) return "";
            return $"{Truncate(g.Name, 25)}: {g.Value:F2}";
        }

        private static string Truncate(string value, int maxChars)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value.Length <= maxChars ? value : value.Substring(0, maxChars - 3) + "...";
        }

        private static string CenterText(string text, int width)
        {
            if (text.Length >= width) return text.Substring(0, width);
            int leftPadding = (width - text.Length) / 2;
            return new string(' ', leftPadding) + text + new string(' ', width - text.Length - leftPadding);
        }

        private static string GetStrategyTag(GeneticIndividual bot)
        {
           
            var biases = new Dictionary<string, float>
    {
        { "Aggro", bot.StrategyDNA[DNA.Aggro_Bias] },
        { "Control", bot.StrategyDNA[DNA.Control_Bias] },
        { "Tempo", bot.StrategyDNA[DNA.Tempo_Bias] },
        { "Combo", bot.StrategyDNA[DNA.Combo_Bias] },
        { "Burn", bot.StrategyDNA[DNA.Burn_Bias] },
        { "Midrange", bot.StrategyDNA[DNA.Midrange_Bias] }
    };

          
            var topBias = biases.OrderByDescending(kv => kv.Value).First();

           
            float avg = biases.Values.Average();
            bool isBalanced = biases.Values.All(v => Math.Abs(v - avg) < 0.8f);

            if (isBalanced) return "Balanced";

            return topBias.Key;
        }

        private class GeneInfo { public string Name; public float Value; }
    }
}