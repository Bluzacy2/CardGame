using CardGame.ConsoleApp.Evolution.V1_Legacy;
using System;
using System.IO;

namespace CardGame.ConsoleApp.Core
{
    public static class EvolutionLogger
    {
        private const string StatsPath = "EvolutionData/evolution_stats.csv";

        public static void Log(int gen, GeneticIndividual best, float avgAggro, float avgControl)
        {
            // Tworzy folder jeśli nie istnieje
            if (!Directory.Exists("EvolutionData"))
                Directory.CreateDirectory("EvolutionData");

            // Tworzy nagłówek jeśli plik jest nowy
            if (!File.Exists(StatsPath))
                File.WriteAllText(StatsPath, "Gen;WinRate;AvgAggro;AvgControl;BestId\n");

            // Dopisywanie nowej linii statystyk
            string line = $"{gen};{best.WinRate:F3};{avgAggro:F3};{avgControl:F3};{best.Id}\n";
            File.AppendAllText(StatsPath, line);
        }
    }
}