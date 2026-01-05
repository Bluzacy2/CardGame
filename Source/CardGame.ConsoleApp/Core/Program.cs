using CardGame.ConsoleApp.Evolution.Analytics;
using CardGame.ConsoleApp.Evolution.V1_Legacy;
using CardGame.ConsoleApp.Evolution.V2_NewGen;
using CardGame.Core.Cards.Data;
using System;
using System.Threading.Tasks;

namespace CardGame.ConsoleApp.Core
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            try
            {
                CardLibrary.Instance.LoadFromJson("Data/Cards/cards.json");
                Console.WriteLine($"[SYSTEM] Karty za³adowane: {CardLibrary.Instance.GetAllIds().Length}");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[CRITICAL ERROR] B³¹d ³adowania kart: {ex.Message}");
                Console.ResetColor();
                return;
            }

            bool exitApp = false;
            while (!exitApp)
            {
                Console.Clear();
                Console.WriteLine("================================================");
                Console.WriteLine("           CARD GAME ENGINE - MASTER CLI        ");
                Console.WriteLine("================================================");
                Console.WriteLine("1. STANDARD BATTLE (AI v3.1 - High Quality)");
                Console.WriteLine("2. EVOLUTION CENTER (Training & Master Matches)");
                Console.WriteLine("3. LEGACY & DEBUG TOOLS (Old versions & Tests)");
                Console.WriteLine("------------------------------------------------");
                Console.WriteLine("0. Exit Application");
                Console.WriteLine("================================================");
                Console.Write("\nWybierz opcjê: ");

                var key = Console.ReadKey(true);
                switch (key.KeyChar)
                {
                    case '1': await AIBattleRunnerIII.RunAsync(); break;
                    case '2': await ShowEvolutionMenu(); break;
                    case '3': await ShowLegacyMenu(); break;
                    case '0': exitApp = true; break;
                }
            }
        }

        static async Task ShowEvolutionMenu()
        {
            bool back = false;
            while (!back)
            {
                Console.Clear();
                Console.WriteLine("================================================");
                Console.WriteLine("             AI EVOLUTION CENTER                ");
                Console.WriteLine("================================================");
                Console.WriteLine("1. URUCHOM NOW¥ EWOLUCJÊ (V2 - Gatunki i Nowoœæ)");
                Console.WriteLine("2. MASTER VS STANDARD (Testuj best_bot_dna.json)");
                Console.WriteLine("3. STATYSTYKI MASTER BOTA (DNA & Deck)");
                Console.WriteLine("4. ANALIZA META (Usage & WinRate)");
                Console.WriteLine("------------------------------------------------");
                Console.WriteLine("0. Powrót do menu g³ównego");
                Console.WriteLine("================================================");
                Console.Write("\nWybierz narzêdzie ewolucji: ");

                var key = Console.ReadKey(true);
                switch (key.KeyChar)
                {
                    case '1':
                        var runnerV2 = new EvolutionRunnerV2();
                        await runnerV2.RunEvolutionAsync();
                        break;
                    case '2': await AIvsEvolutionRunner.RunAsync(); break;
                    case '3': EvoBotViewer.ViewBestBot(); break;
                    case '4': CardMetaViewer.ViewMeta(); break;
                    case '0': back = true; break;
                }
            }
        }

        static async Task ShowLegacyMenu()
        {
            bool back = false;
            while (!back)
            {
                Console.Clear();
                Console.WriteLine("================================================");
                Console.WriteLine("            NARZÊDZIA DEBUG & LEGACY            ");
                Console.WriteLine("================================================");
                Console.WriteLine("1. Testy mechanik (Unit Tests)");
                Console.WriteLine("2. AI Battle v1 (Original)");
                Console.WriteLine("3. AI Battle v2 (Nuclear UI)");
                Console.WriteLine("4. Balance AI Tester (Long-term)");
                Console.WriteLine("5. Optimized AI Logger (Debug Panel)");
                Console.WriteLine("6. URUCHOM STAR¥ EWOLUCJÊ (V1 - Klasyczna)");
                Console.WriteLine("------------------------------------------------");
                Console.WriteLine("0. Powrót");
                Console.WriteLine("================================================");
                Console.Write("\nWybierz narzêdzie: ");

                var key = Console.ReadKey(true);
                switch (key.KeyChar)
                {
                    case '1': MechanicsTester.Run(); Pause(); break;
                    case '2': await AIBattleRunner.RunAsync(); Pause(); break;
                    case '3': await AIBattleRunnerII.RunAsync(); Pause(); break;
                    case '4': await BalanceAITester.RunAsync(); Pause(); break;
                    case '5': await OptimizedAILogger.RunAsync(); Pause(); break;
                    case '6':
                        var runnerV1 = new EvolutionRunner();
                        await runnerV1.RunEvolutionAsync();
                        break;
                    case '0': back = true; break;
                }
            }
        }

        static void Pause() { Console.WriteLine("\nNaciœnij dowolny klawisz..."); Console.ReadKey(true); }
    }
}