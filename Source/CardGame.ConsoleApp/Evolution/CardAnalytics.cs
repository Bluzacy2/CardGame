using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using CardGame.Core.Cards.Data;

namespace CardGame.ConsoleApp.Evolution
{
    public class CardAnalytics
    {
        public class CardEntry
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public int DecksCount { get; set; }
            public int TotalCopies { get; set; }
            public int TotalWins { get; set; }
            public int TotalGames { get; set; }

            public float WinRate => TotalGames == 0 ? 0 : (float)TotalWins / TotalGames;
            public float UsageRate => DecksCount / 100f;
            public float AvgDensity => DecksCount == 0 ? 0 : (float)TotalCopies / DecksCount;
        }

        public static void ProcessAndSave(List<GeneticIndividual> population, int generation)
        {
            var allIds = CardLibrary.Instance.GetAllIds();
            var report = new Dictionary<int, CardEntry>();

            foreach (var bot in population)
            {
                var uniqueInDeck = bot.DeckDNA.Distinct();
                foreach (var cardId in uniqueInDeck)
                {
                    if (!report.ContainsKey(cardId))
                    {
                        var def = CardLibrary.Instance.GetCard(cardId);
                        report[cardId] = new CardEntry { Id = cardId, Name = def.Name };
                    }

                    var entry = report[cardId];
                    entry.DecksCount++;
                    entry.TotalCopies += bot.DeckDNA.Count(id => id == cardId);
                    entry.TotalGames += bot.GamesPlayed;
                    entry.TotalWins += bot.Wins;
                }
            }

            var sortedReport = report.Values
                .OrderByDescending(r => r.WinRate)
                .ThenByDescending(r => r.UsageRate)
                .ToList();

            string json = JsonSerializer.Serialize(sortedReport, new JsonSerializerOptions { WriteIndented = true });
            if (!Directory.Exists("Analytics")) Directory.CreateDirectory("Analytics");
            File.WriteAllText($"Analytics/current_meta.json", json);
            File.WriteAllText($"Analytics/gen_{generation}.json", json);
        }
    }
}