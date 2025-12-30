using System;
using System.Threading.Tasks;

namespace CardGame.ConsoleApp
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            while (true)
            {
                Console.Clear();
                Console.WriteLine("=== CARD GAME ENGINE CLI ===");
                Console.WriteLine("1. Uruchom Testy Mechanik (Unit Tests)");
                Console.WriteLine("2. Uruchom Symulację Bitwy (AI vs AI)");
                Console.WriteLine("3. Uruchom Symulację Bitwy (AI vs AI) v2");
                Console.WriteLine("4. Uruchom Symulację Bitwy (AI vs AI) v3");
                Console.WriteLine("5. Pętla Symulacji Bitwy (Balance AI Tester)");
                Console.WriteLine("6. Zoptymalizowana Symulacja (Optimized AI Logger)");
                Console.WriteLine("7. Wyjście");
                Console.Write("\nWybierz opcję: ");

                var key = Console.ReadKey();
                Console.WriteLine();

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
                        await AIBattleRunnerIII.RunAsync();
                        Pause();
                        break;
                    case '5':
                        await BalanceAITester.RunAsync();
                        Pause();
                        break;
                    case '6':
                        await OptimizedAILogger.RunAsync();
                        Pause();
                        break;
                    case '7':
                        return;
                    default:
                        Console.WriteLine("Nieznana opcja.");
                        await Task.Delay(1000);
                        break;
                }
            }
        }

        static void Pause()
        {
            Console.WriteLine("\nNaciśnij dowolny klawisz, aby wrócić do menu...");
            Console.ReadKey();
        }
    }
}