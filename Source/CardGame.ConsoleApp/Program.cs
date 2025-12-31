using CardGame.ConsoleApp.Evolution;
using CardGame.Core.Cards.Data;
using System;
using System.Threading.Tasks;

namespace CardGame.ConsoleApp
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            // Initialization
            try
            {
                CardLibrary.Instance.LoadFromJson("Data/Cards/cards.json");
                Console.WriteLine($"[SYSTEM] Cards loaded: {CardLibrary.Instance.GetAllIds().Length}");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[CRITICAL ERROR] Failed to load cards: {ex.Message}");
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
                Console.Write("\nSelect option: ");

                var key = Console.ReadKey(true);
                switch (key.KeyChar)
                {
                    case '1':
                        await AIBattleRunnerIII.RunAsync();
                        break;
                    case '2':
                        await ShowEvolutionMenu();
                        break;
                    case '3':
                        await ShowLegacyMenu();
                        break;
                    case '0':
                        exitApp = true;
                        break;
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
                Console.WriteLine("1. RUN EVOLUTION TRAINING (Generate DNA)");
                Console.WriteLine("2. MASTER VS STANDARD (Test best_bot_dna.json)");
                Console.WriteLine("3. VIEW MASTER BOT STATISTICS (DNA & Deck)"); 
                Console.WriteLine("------------------------------------------------");
                Console.WriteLine("0. Back to Main Menu");
                Console.WriteLine("================================================");
                Console.Write("\nSelect evolution tool: ");

                var key = Console.ReadKey(true);
                switch (key.KeyChar)
                {
                    case '1':
                        Console.Write("\nHow many generations to simulate? ");
                        if (int.TryParse(Console.ReadLine(), out int gCount))
                        {
                            var runner = new EvolutionRunner();
                            await runner.RunEvolutionAsync(gCount);
                        }
                        Pause();
                        break;
                    case '2':
                        await AIvsEvolutionRunner.RunAsync();
                        break;
                    case '3':
                        EvoBotViewer.ViewBestBot(); 
                        break;
                    case '0':
                        back = true;
                        break;
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
                Console.WriteLine("            LEGACY & DEBUG TOOLS                ");
                Console.WriteLine("================================================");
                Console.WriteLine("1. Unit Mechanics Tests (Unit Tests)");
                Console.WriteLine("2. AI Battle v1 (Original)");
                Console.WriteLine("3. AI Battle v2 (Nuclear UI Prototype)");
                Console.WriteLine("4. Balance AI Tester (Long-term simulation)");
                Console.WriteLine("5. Optimized AI Logger (Debug Panel)");
                Console.WriteLine("------------------------------------------------");
                Console.WriteLine("0. Back to Main Menu");
                Console.WriteLine("================================================");
                Console.Write("\nSelect legacy tool: ");

                var key = Console.ReadKey(true);
                switch (key.KeyChar)
                {
                    case '1':
                        MechanicsTester.Run();
                        Pause();
                        break;
                    case '2':
                        await AIBattleRunner.RunAsync();
                        Pause();
                        break;
                    case '3':
                        await AIBattleRunnerII.RunAsync();
                        Pause();
                        break;
                    case '4':
                        await BalanceAITester.RunAsync();
                        Pause();
                        break;
                    case '5':
                        await OptimizedAILogger.RunAsync();
                        Pause();
                        break;
                    case '0':
                        back = true;
                        break;
                }
            }
        }

        static void Pause()
        {
            Console.WriteLine("\nPress any key to continue...");
            Console.ReadKey(true);
        }
    }
}