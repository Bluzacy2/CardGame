using System.Diagnostics;

namespace CardGame.ConsoleApp.Evolution.V2_NewGen
{
    public static class PerformanceMonitor
    {
        private static PerformanceCounter _cpuCounter;
        private static PerformanceCounter _ramCounter;
        private static bool _countersInitialized = false;

        static PerformanceMonitor()
        {
            try
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _ramCounter = new PerformanceCounter("Memory", "Available MBytes");
                _countersInitialized = true;
            }
            catch
            {
                _countersInitialized = false;
                Console.WriteLine("⚠️  Performance counters not available - using basic monitoring");
            }
        }

        public static PerformanceMetrics GetCurrentMetrics()
        {
            var metrics = new PerformanceMetrics();

            if (_countersInitialized)
            {
                try
                {
                    // CPU Usage - need to call NextValue twice for accurate reading
                    _cpuCounter.NextValue();
                    Thread.Sleep(100);
                    metrics.CpuUsage = _cpuCounter.NextValue();

                    // RAM Usage
                    metrics.AvailableMemoryMB = _ramCounter.NextValue();
                }
                catch
                {
                    _countersInitialized = false;
                }
            }

            // Process-specific metrics (always available)
            try
            {
                var process = Process.GetCurrentProcess();
                metrics.ProcessMemoryMB = process.WorkingSet64 / 1024.0 / 1024.0;
                metrics.ThreadCount = process.Threads.Count;
                metrics.HandleCount = process.HandleCount;
                metrics.TotalProcessorTime = process.TotalProcessorTime.TotalSeconds;
            }
            catch { }

            return metrics;
        }

        public static void LogGenerationPerformance(int generation, TimeSpan generationTime,
                                                   int populationSize, int matchesPerBot)
        {
            var metrics = GetCurrentMetrics();
            int totalGames = populationSize * matchesPerBot;
            double gamesPerSecond = totalGames / generationTime.TotalSeconds;

            string logLine = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss},{generation}," +
                           $"{generationTime.TotalSeconds:F2},{gamesPerSecond:F2}," +
                           $"{metrics.CpuUsage:F1},{metrics.ProcessMemoryMB:F0}," +
                           $"{metrics.AvailableMemoryMB:F0},{metrics.ThreadCount},{metrics.TotalProcessorTime:F0}";

            try
            {
                Directory.CreateDirectory("EvolutionData/Performance");
                string logFile = $"EvolutionData/Performance/detailed_log.csv";
                bool fileExists = File.Exists(logFile);

                using (var writer = new StreamWriter(logFile, true))
                {
                    if (!fileExists)
                        writer.WriteLine("Timestamp,Generation,GenTimeSeconds,GamesPerSecond," +
                                       "CPUPercent,ProcessMemoryMB,AvailableMemoryMB,ThreadCount,TotalProcessorTime");

                    writer.WriteLine(logLine);
                }

                // Console output every 5 generations
                if (generation % 5 == 0)
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"📊 Perf: {gamesPerSecond:F1} games/s | " +
                                    $"CPU: {metrics.CpuUsage:F0}% | RAM: {metrics.ProcessMemoryMB:F0}MB | " +
                                    $"Threads: {metrics.ThreadCount}");
                    Console.ResetColor();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to log performance: {ex.Message}");
            }
        }

        public static void CheckForBottlenecks()
        {
            var metrics = GetCurrentMetrics();

            if (metrics.CpuUsage > 0 && metrics.CpuUsage < 60)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("⚠️  WARNING: Low CPU usage (<60%). Possible bottlenecks:");
                Console.WriteLine("   - I/O waits (disk/network access)");
                Console.WriteLine("   - Synchronization locks in parallel code");
                Console.WriteLine("   - Memory allocation/garbage collection pressure");
                Console.WriteLine("   - CardLibrary.Instance.GetCard() might be slow");
                Console.ResetColor();
            }

            if (metrics.AvailableMemoryMB < 1024) // Less than 1GB available
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"🚨 CRITICAL: Low memory! Only {metrics.AvailableMemoryMB:F0}MB available.");
                Console.ResetColor();
            }

            if (metrics.ThreadCount > 50)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"⚠️  WARNING: High thread count ({metrics.ThreadCount}). Check for thread leaks.");
                Console.ResetColor();
            }
        }

        public static void PrintSystemSummary()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n=== SYSTEM SUMMARY ===");
            Console.WriteLine($"Logical Processors: {Environment.ProcessorCount}");
            Console.WriteLine($"64-bit OS: {Environment.Is64BitOperatingSystem}");
            Console.WriteLine($"64-bit Process: {Environment.Is64BitProcess}");
            Console.WriteLine($"OS Version: {Environment.OSVersion}");
            Console.WriteLine($"Total Memory: {GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1024 / 1024}MB");
            Console.ResetColor();
        }
    }

    public class PerformanceMetrics
    {
        public float CpuUsage { get; set; }
        public float AvailableMemoryMB { get; set; }
        public double ProcessMemoryMB { get; set; }
        public int ThreadCount { get; set; }
        public int HandleCount { get; set; }
        public double TotalProcessorTime { get; set; }
    }
}