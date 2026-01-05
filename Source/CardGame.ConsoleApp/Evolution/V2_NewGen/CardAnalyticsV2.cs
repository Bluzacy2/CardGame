using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using CardGame.Core.Cards.Data;

namespace CardGame.ConsoleApp.Evolution.V2_NewGen
{
    public static class CardAnalyticsV2
    {
        // Dodajemy opcjonalny parametr cardStats - już obliczone statystyki kart
        public static void ProcessAndSave(List<EvolvableIndividual> population, int generation,
                                         Dictionary<int, CardStatsEnhanced> cardStats = null,
                                         string fileName = null)
        {
            if (population == null || !population.Any())
                return;

            var analytics = new CardAnalyticsData
            {
                Generation = generation,
                Timestamp = DateTime.Now,
                TotalIndividuals = population.Count
            };

            // 1. ANALIZA WYSTĘPOWANIA KART - UŻYJ JUŻ OBLICZONYCH STATYSTYK JEŚLI DOSTĘPNE
            List<CardStatistic> cardStatistics;

            if (cardStats != null && cardStats.Any())
            {
                // Użyj wstępnie obliczonych statystyk z EvolutionRunner
                cardStatistics = cardStats.Values
                    .Select(cs => new CardStatistic
                    {
                        CardId = cs.CardId,
                        CardName = cs.CardName,
                        TotalCopies = cs.TotalCopiesInPopulation,
                        DeckCount = cs.DeckCount,
                        PercentageInDecks = cs.UseRate * 100, // Use Rate jako % decków z kartą
                        AverageCopiesPerDeck = cs.AverageDensity, // Średnia liczba kopii w deckach z kartą
                        WinRate = cs.WinRate * 100,
                        GamesPlayed = cs.GamesWithCard,
                        Wins = cs.WinsWithCard
                    })
                    .OrderByDescending(c => c.DeckCount) // Sortuj po liczbie decków z kartą
                    .ToList();
            }
            else
            {
                // Fallback - stara logika (ale poprawiona)
                var deckCardPresence = new Dictionary<int, int>(); // cardId -> liczba decków z tą kartą
                var cardCopyCounts = new Dictionary<int, int>(); // cardId -> łączna liczba kopii

                foreach (var individual in population)
                {
                    var rng = new Random();
                    var deck = individual.Dna.BuildDeck(CardLibrary.Instance.GetAllIds()
                        .Where(id => id < 900).ToArray(), rng);

                    var uniqueCards = deck.Distinct();
                    foreach (var cardId in uniqueCards)
                    {
                        if (!deckCardPresence.ContainsKey(cardId)) deckCardPresence[cardId] = 0;
                        deckCardPresence[cardId]++;
                    }

                    foreach (var cardId in deck)
                    {
                        if (!cardCopyCounts.ContainsKey(cardId)) cardCopyCounts[cardId] = 0;
                        cardCopyCounts[cardId]++;
                    }
                }

                cardStatistics = deckCardPresence
                    .Select(kvp => new CardStatistic
                    {
                        CardId = kvp.Key,
                        CardName = GetCardName(kvp.Key),
                        DeckCount = kvp.Value,
                        TotalCopies = cardCopyCounts.ContainsKey(kvp.Key) ? cardCopyCounts[kvp.Key] : 0,
                        PercentageInDecks = (double)kvp.Value * 100.0 / population.Count, // POPRAWNE: % decków
                        AverageCopiesPerDeck = cardCopyCounts.ContainsKey(kvp.Key) ?
                            (double)cardCopyCounts[kvp.Key] / kvp.Value : 0, // Średnia kopii w deckach z kartą
                        WinRate = 0, // Brak danych w tej metodzie
                        GamesPlayed = 0,
                        Wins = 0
                    })
                    .OrderByDescending(c => c.DeckCount)
                    .ToList();
            }

            analytics.CardStatistics = cardStatistics;

            // 2. ANALIZA ARCHETYPÓW
            var archetypeStats = population
                .GroupBy(ind => ind.Dna.ClassifyArchetype())
                .Select(g => new ArchetypeStatistic
                {
                    Archetype = g.Key,
                    Count = g.Count(),
                    Percentage = (double)g.Count() * 100.0 / population.Count,
                    AvgFitness = g.Average(ind => ind.CompositeFitness),
                    BestFitness = g.Max(ind => ind.CompositeFitness)
                })
                .OrderByDescending(a => a.Count)
                .ToList();

            analytics.ArchetypeStatistics = archetypeStats;

            // 3. TOP KARTY PER ARCHETYP (ulepszona wersja)
            analytics.TopCardsByArchetype = GetTopCardsByArchetype(population, cardStats);

            // 4. SYNERGIE KART - ulepszona wersja
            analytics.CardSynergies = AnalyzeCardSynergies(population.Take(Math.Min(20, population.Count)).ToList());

            // 5. DODATKOWO: Karty z najlepszym win rate (jeśli dane dostępne)
            if (cardStats != null)
            {
                analytics.BestWinRateCards = cardStats.Values
                    .Where(cs => cs.GamesWithCard >= 10) // Min. 10 gier dla wiarygodności
                    .OrderByDescending(cs => cs.WinRate)
                    .Take(15)
                    .Select(cs => new WinRateCardStat
                    {
                        CardId = cs.CardId,
                        CardName = cs.CardName,
                        WinRate = cs.WinRate * 100,
                        GamesPlayed = cs.GamesWithCard,
                        UseRate = cs.UseRate * 100,
                        AverageDensity = cs.AverageDensity
                    })
                    .ToList();
            }

            // Zapisz
            fileName ??= $"EvolutionData/Analytics/gen_{generation:D4}.json";
            SaveToFile(analytics, fileName);

            // Wypisz podsumowanie
            PrintSummary(analytics);
        }

        private static Dictionary<string, List<CardStatistic>> GetTopCardsByArchetype(
            List<EvolvableIndividual> population,
            Dictionary<int, CardStatsEnhanced> cardStats = null)
        {
            var result = new Dictionary<string, List<CardStatistic>>();
            var archetypes = population.Select(ind => ind.Dna.ClassifyArchetype()).Distinct();

            foreach (var archetype in archetypes)
            {
                var archetypeIndividuals = population.Where(ind => ind.Dna.ClassifyArchetype() == archetype).ToList();

                if (cardStats != null)
                {
                    // Użyj wstępnie obliczonych statystyk dla tego archetypu
                    var cardIdsInArchetype = new HashSet<int>();
                    var cardDeckCounts = new Dictionary<int, int>();
                    var cardCopyCounts = new Dictionary<int, int>();

                    foreach (var ind in archetypeIndividuals)
                    {
                        var rng = new Random();
                        var deck = ind.Dna.BuildDeck(CardLibrary.Instance.GetAllIds()
                            .Where(id => id < 900).ToArray(), rng);

                        var uniqueCards = deck.Distinct();
                        foreach (var cardId in uniqueCards)
                        {
                            if (!cardDeckCounts.ContainsKey(cardId)) cardDeckCounts[cardId] = 0;
                            cardDeckCounts[cardId]++;
                            cardIdsInArchetype.Add(cardId);
                        }

                        foreach (var cardId in deck)
                        {
                            if (!cardCopyCounts.ContainsKey(cardId)) cardCopyCounts[cardId] = 0;
                            cardCopyCounts[cardId]++;
                        }
                    }

                    var topCards = cardIdsInArchetype
                        .Select(cardId =>
                        {
                            if (cardStats.TryGetValue(cardId, out var cs))
                            {
                                return new CardStatistic
                                {
                                    CardId = cardId,
                                    CardName = cs.CardName,
                                    DeckCount = cardDeckCounts[cardId],
                                    TotalCopies = cardCopyCounts.ContainsKey(cardId) ? cardCopyCounts[cardId] : 0,
                                    PercentageInDecks = (double)cardDeckCounts[cardId] * 100.0 / archetypeIndividuals.Count,
                                    AverageCopiesPerDeck = cardDeckCounts[cardId] > 0 ?
                                        (double)cardCopyCounts[cardId] / cardDeckCounts[cardId] : 0,
                                    WinRate = cs.WinRate * 100,
                                    GamesPlayed = cs.GamesWithCard,
                                    Wins = cs.WinsWithCard
                                };
                            }
                            return null;
                        })
                        .Where(c => c != null)
                        .OrderByDescending(c => c.DeckCount)
                        .Take(15)
                        .ToList();

                    result[archetype] = topCards;
                }
                else
                {
                    // Stara logika (ulepszona)
                    var deckCardPresence = new Dictionary<int, int>();
                    var cardCopyCounts = new Dictionary<int, int>();

                    foreach (var ind in archetypeIndividuals)
                    {
                        var rng = new Random();
                        var deck = ind.Dna.BuildDeck(CardLibrary.Instance.GetAllIds()
                            .Where(id => id < 900).ToArray(), rng);

                        var uniqueCards = deck.Distinct();
                        foreach (var cardId in uniqueCards)
                        {
                            if (!deckCardPresence.ContainsKey(cardId)) deckCardPresence[cardId] = 0;
                            deckCardPresence[cardId]++;
                        }

                        foreach (var cardId in deck)
                        {
                            if (!cardCopyCounts.ContainsKey(cardId)) cardCopyCounts[cardId] = 0;
                            cardCopyCounts[cardId]++;
                        }
                    }

                    var topCards = deckCardPresence
                        .Select(kvp => new CardStatistic
                        {
                            CardId = kvp.Key,
                            CardName = GetCardName(kvp.Key),
                            DeckCount = kvp.Value,
                            TotalCopies = cardCopyCounts.ContainsKey(kvp.Key) ? cardCopyCounts[kvp.Key] : 0,
                            PercentageInDecks = (double)kvp.Value * 100.0 / archetypeIndividuals.Count,
                            AverageCopiesPerDeck = cardCopyCounts.ContainsKey(kvp.Key) ?
                                (double)cardCopyCounts[kvp.Key] / kvp.Value : 0
                        })
                        .OrderByDescending(c => c.DeckCount)
                        .Take(15)
                        .ToList();

                    result[archetype] = topCards;
                }
            }

            return result;
        }

        private static List<CardSynergy> AnalyzeCardSynergies(List<EvolvableIndividual> sample)
        {
            var synergies = new List<CardSynergy>();
            if (sample.Count == 0) return synergies;

            // Zbierz wszystkie karty z próbki
            var allCards = new HashSet<int>();
            var decks = new List<int[]>();

            foreach (var ind in sample)
            {
                var rng = new Random();
                var deck = ind.Dna.BuildDeck(CardLibrary.Instance.GetAllIds()
                    .Where(id => id < 900).ToArray(), rng);

                decks.Add(deck);
                foreach (var cardId in deck)
                {
                    allCards.Add(cardId);
                }
            }

            // Ogranicz analizę do 40 najpopularniejszych kart dla wydajności
            var cardPopularity = allCards.ToDictionary(cardId => cardId, cardId => decks.Count(d => d.Contains(cardId)));
            var topCards = cardPopularity
                .OrderByDescending(kvp => kvp.Value)
                .Take(40)
                .Select(kvp => kvp.Key)
                .ToList();

            // Analizuj pary kart
            for (int i = 0; i < topCards.Count; i++)
            {
                for (int j = i + 1; j < topCards.Count; j++)
                {
                    int card1Id = topCards[i];
                    int card2Id = topCards[j];

                    int coOccurrence = decks.Count(d => d.Contains(card1Id) && d.Contains(card2Id));

                    if (coOccurrence > 2) // Tylko jeśli występuje w co najmniej 3 deckach
                    {
                        int card1Decks = decks.Count(d => d.Contains(card1Id));
                        int card2Decks = decks.Count(d => d.Contains(card2Id));

                        // Oblicz oczekiwaną współwystępowalność
                        double expected = (double)card1Decks * card2Decks / sample.Count;
                        double synergyRatio = coOccurrence / Math.Max(1.0, expected);

                        synergies.Add(new CardSynergy
                        {
                            Card1Id = card1Id,
                            Card1Name = GetCardName(card1Id),
                            Card2Id = card2Id,
                            Card2Name = GetCardName(card2Id),
                            CoOccurrence = coOccurrence,
                            CoOccurrenceRate = (double)coOccurrence * 100.0 / sample.Count,
                            SynergyRatio = synergyRatio,
                            Card1Usage = card1Decks,
                            Card2Usage = card2Decks
                        });
                    }
                }
            }

            return synergies
                .OrderByDescending(s => s.SynergyRatio)
                .ThenByDescending(s => s.CoOccurrence)
                .Take(25)
                .ToList();
        }

        private static string GetCardName(int cardId)
        {
            try
            {
                var card = CardLibrary.Instance.GetCard(cardId);
                return card.Name;
            }
            catch
            {
                return $"Card_{cardId}";
            }
        }

        private static void SaveToFile(CardAnalyticsData data, string fileName)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(fileName));
                var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                File.WriteAllText(fileName, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to save analytics: {ex.Message}");
            }
        }

        private static void PrintSummary(CardAnalyticsData data)
        {
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine($"\n=== CARD ANALYTICS (Gen {data.Generation}) ===");
            Console.ResetColor();

            Console.WriteLine($"Total individuals: {data.TotalIndividuals}");

            // Top 10 kart po Use Rate (liczbie decków)
            Console.WriteLine("\nTop 10 cards by Use Rate (% of decks):");
            foreach (var card in data.CardStatistics.Take(10))
            {
                Console.WriteLine($"  {card.CardName}: {card.PercentageInDecks:F1}% decks ({card.DeckCount}/{data.TotalIndividuals})");
                Console.WriteLine($"     Avg copies: {card.AverageCopiesPerDeck:F2} | Total copies: {card.TotalCopies}");
                if (card.WinRate > 0)
                {
                    Console.WriteLine($"     Win Rate: {card.WinRate:F1}% ({card.Wins}/{card.GamesPlayed} games)");
                }
            }

            // Archetypy
            Console.WriteLine("\nArchetype distribution:");
            foreach (var arch in data.ArchetypeStatistics)
            {
                Console.WriteLine($"  {arch.Archetype}: {arch.Count} bots ({arch.Percentage:F1}%), Avg Fitness: {arch.AvgFitness:F0}");
            }

            // Top synergii
            if (data.CardSynergies.Any())
            {
                Console.WriteLine("\nTop 5 card synergies (by synergy ratio):");
                foreach (var syn in data.CardSynergies.Take(5))
                {
                    Console.WriteLine($"  {syn.Card1Name} + {syn.Card2Name}:");
                    Console.WriteLine($"     Co-occurrence: {syn.CoOccurrenceRate:F1}% ({syn.CoOccurrence} decks)");
                    Console.WriteLine($"     Synergy ratio: {syn.SynergyRatio:F2}x expected");
                }
            }

            // Karty z najlepszym win rate
            if (data.BestWinRateCards != null && data.BestWinRateCards.Any())
            {
                Console.WriteLine("\nTop cards by Win Rate (min 10 games):");
                foreach (var card in data.BestWinRateCards.Take(10))
                {
                    Console.WriteLine($"  {card.CardName}: {card.WinRate:F1}% win rate");
                    Console.WriteLine($"     Games: {card.GamesPlayed} | Use Rate: {card.UseRate:F1}% | Avg copies: {card.AverageDensity:F2}");
                }
            }
        }

        // Metoda do generowania raportu CSV (łatwy do importu do Excel)
        public static void ExportToCsv(List<EvolvableIndividual> population, int generation)
        {
            try
            {
                var lines = new List<string>();
                lines.Add("Generation;IndividualId;Archetype;Fitness;DeckSize;DeckList");

                foreach (var ind in population.OrderByDescending(i => i.CompositeFitness))
                {
                    var rng = new Random();
                    var deck = ind.Dna.BuildDeck(CardLibrary.Instance.GetAllIds().Where(id => id < 900).ToArray(), rng);

                    var cardGroups = deck.GroupBy(c => c).Select(g => $"{g.Key}:{g.Count()}").OrderBy(x => x);
                    var deckString = string.Join("|", cardGroups); // Use pipe so it doesn't break columns

                    // Force InvariantCulture here too!
                    string line = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "{0};{1};{2};{3:F0};{4};\"{5}\"",
                        generation, ind.Id, ind.Dna.ClassifyArchetype(), ind.CompositeFitness, deck.Length, deckString);

                    lines.Add(line);
                }

                Directory.CreateDirectory("EvolutionData/CSV");
                File.WriteAllLines($"EvolutionData/CSV/generation_{generation:D4}.csv", lines);
                Console.WriteLine($"CSV exported: generation_{generation:D4}.csv");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to export CSV: {ex.Message}");
            }
        }

        // Nowa metoda: Eksport statystyk kart do osobnego CSV
        public static void ExportCardStatsToCsv(Dictionary<int, CardStatsEnhanced> cardStats, int generation)
        {
            try
            {
                var lines = new List<string>();
                // 1. Switch header to Semicolons
                lines.Add("CardId;CardName;UseRate(%);DeckCount;TotalDecks;AvgCopiesPerDeck;WinRate(%);Games;Wins;TotalCopies");

                foreach (var stat in cardStats.Values.OrderByDescending(s => s.UseRate))
                {
                    // 2. Use string.Format with InvariantCulture to force dots (.) instead of commas (,)
                    // 3. Use Semicolons (;) as the separator
                    string line = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "{0};\"{1}\";{2:F2};{3};{4};{5:F2};{6:F2};{7};{8};{9}",
                        stat.CardId,
                        stat.CardName.Replace(";", ""), // Sanitize name
                        stat.UseRate * 100,
                        stat.DeckCount,
                        stat.TotalDecks,
                        stat.AverageDensity,
                        stat.WinRate * 100,
                        stat.GamesWithCard,
                        stat.WinsWithCard,
                        stat.TotalCopiesInPopulation);

                    lines.Add(line);
                }

                Directory.CreateDirectory("EvolutionData/CardStats");
                string filePath = $"EvolutionData/CardStats/card_stats_gen{generation:D4}.csv";
                File.WriteAllLines(filePath, lines);
                Console.WriteLine($"Card stats CSV exported: {filePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to export card stats CSV: {ex.Message}");
            }
        }

        // Metoda do generowania podsumowania mety (format tekstowy)
        public static void GenerateMetaReport(List<EvolvableIndividual> population, int generation,
                                             Dictionary<int, CardStatsEnhanced> cardStats = null)
        {
            try
            {
                var report = new List<string>();
                report.Add($"=== META REPORT - GENERATION {generation} ===");
                report.Add($"Date: {DateTime.Now}");
                report.Add($"Population: {population.Count} bots");
                report.Add("");

                // Najlepsze boty
                var top5 = population.OrderByDescending(p => p.CompositeFitness).Take(5).ToList();
                report.Add("TOP 5 BOTS:");
                for (int i = 0; i < top5.Count; i++)
                {
                    var bot = top5[i];
                    report.Add($"{i + 1}. ID:{bot.Id} | Archetype:{bot.Dna.ClassifyArchetype()} | Fitness:{bot.CompositeFitness:F0}");

                    // Dodaj statystyki decku
                    var rng = new Random();
                    var deck = bot.Dna.BuildDeck(CardLibrary.Instance.GetAllIds()
                        .Where(id => id < 900).ToArray(), rng);

                    var cardCounts = deck.GroupBy(c => c)
                        .Select(g => $"{GetCardName(g.Key)} x{g.Count()}")
                        .OrderByDescending(x => x);

                    report.Add($"   Deck ({deck.Length} cards): {string.Join(", ", cardCounts.Take(8))}");
                    if (cardCounts.Count() > 8)
                        report.Add($"   ... and {cardCounts.Count() - 8} more cards");
                    report.Add("");
                }

                // Popularność kart (po liczbie decków)
                var cardDeckCounts = new Dictionary<int, int>();
                var cardCopyCounts = new Dictionary<int, int>();

                foreach (var ind in population)
                {
                    var rng = new Random();
                    var deck = ind.Dna.BuildDeck(CardLibrary.Instance.GetAllIds()
                        .Where(id => id < 900).ToArray(), rng);

                    var uniqueCards = deck.Distinct();
                    foreach (var cardId in uniqueCards)
                    {
                        if (!cardDeckCounts.ContainsKey(cardId)) cardDeckCounts[cardId] = 0;
                        cardDeckCounts[cardId]++;
                    }

                    foreach (var cardId in deck)
                    {
                        if (!cardCopyCounts.ContainsKey(cardId)) cardCopyCounts[cardId] = 0;
                        cardCopyCounts[cardId]++;
                    }
                }

                var cardStatsList = cardDeckCounts
                    .Select(kvp => new
                    {
                        CardId = kvp.Key,
                        CardName = GetCardName(kvp.Key),
                        DeckCount = kvp.Value,
                        TotalCopies = cardCopyCounts.ContainsKey(kvp.Key) ? cardCopyCounts[kvp.Key] : 0,
                        Percentage = (double)kvp.Value * 100.0 / population.Count,
                        AvgCopies = cardCopyCounts.ContainsKey(kvp.Key) ? (double)cardCopyCounts[kvp.Key] / kvp.Value : 0
                    })
                    .OrderByDescending(c => c.DeckCount)
                    .ToList();

                report.Add("TOP 20 CARTS (by deck count):");
                foreach (var card in cardStatsList.Take(20))
                {
                    report.Add($"  {card.CardName}: {card.Percentage:F1}% decks ({card.DeckCount} decks)");
                    report.Add($"     Avg copies in decks: {card.AvgCopies:F2} | Total copies: {card.TotalCopies}");

                    // Jeśli dostępne statystyki win rate, dodaj je
                    if (cardStats != null && cardStats.ContainsKey(card.CardId))
                    {
                        var cs = cardStats[card.CardId];
                        if (cs.GamesWithCard > 0)
                        {
                            report.Add($"     Win Rate: {cs.WinRate * 100:F1}% ({cs.WinsWithCard}/{cs.GamesWithCard} games)");
                        }
                    }
                    report.Add("");
                }

                // Najgorsze karty (najrzadziej używane) - z win rate jeśli dostępne
                report.Add("BOTTOM 10 CARTS (rarely used):");
                foreach (var card in cardStatsList.TakeLast(10))
                {
                    string winRateInfo = "";
                    if (cardStats != null && cardStats.ContainsKey(card.CardId))
                    {
                        var cs = cardStats[card.CardId];
                        if (cs.GamesWithCard > 0)
                        {
                            winRateInfo = $", Win Rate: {cs.WinRate * 100:F1}%";
                        }
                    }
                    report.Add($"  {card.CardName}: {card.Percentage:F1}% decks{winRateInfo}");
                }

                Directory.CreateDirectory("EvolutionData/Reports");
                File.WriteAllLines($"EvolutionData/Reports/meta_report_gen_{generation:D4}.txt", report);
                Console.WriteLine($"Meta report saved: meta_report_gen_{generation:D4}.txt");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to generate meta report: {ex.Message}");
            }
        }
    }

    // ========== ROZSZERZONE KLASY DANYCH ==========

    public class CardAnalyticsData
    {
        public int Generation { get; set; }
        public DateTime Timestamp { get; set; }
        public int TotalIndividuals { get; set; }
        public List<CardStatistic> CardStatistics { get; set; } = new();
        public List<ArchetypeStatistic> ArchetypeStatistics { get; set; } = new();
        public Dictionary<string, List<CardStatistic>> TopCardsByArchetype { get; set; } = new();
        public List<CardSynergy> CardSynergies { get; set; } = new();
        public List<WinRateCardStat> BestWinRateCards { get; set; } = new();
    }

    public class CardStatistic
    {
        public int CardId { get; set; }
        public string CardName { get; set; }
        public int DeckCount { get; set; }          // Liczba decków zawierających kartę
        public int TotalCopies { get; set; }        // Łączna liczba kopii w populacji
        public double PercentageInDecks { get; set; } // Use Rate: % decków z kartą
        public double AverageCopiesPerDeck { get; set; } // Średnia liczba kopii w deckach z kartą
        public double WinRate { get; set; }         // % wygranych gier
        public int GamesPlayed { get; set; }
        public int Wins { get; set; }
    }

    public class ArchetypeStatistic
    {
        public string Archetype { get; set; }
        public int Count { get; set; }
        public double Percentage { get; set; }
        public double AvgFitness { get; set; }
        public double BestFitness { get; set; }
    }

    public class CardSynergy
    {
        public int Card1Id { get; set; }
        public string Card1Name { get; set; }
        public int Card2Id { get; set; }
        public string Card2Name { get; set; }
        public int CoOccurrence { get; set; }
        public double CoOccurrenceRate { get; set; }
        public double SynergyRatio { get; set; }    // Wskaźnik synergii (obserwowane/oczekiwane)
        public int Card1Usage { get; set; }
        public int Card2Usage { get; set; }
    }

    public class WinRateCardStat
    {
        public int CardId { get; set; }
        public string CardName { get; set; }
        public double WinRate { get; set; }
        public int GamesPlayed { get; set; }
        public double UseRate { get; set; }
        public double AverageDensity { get; set; }
    }
}