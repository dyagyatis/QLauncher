using System;
using System.IO;

namespace MinecraftLauncher.Helpers
{
    public static class CacheCleanerHelper
    {
        public static long CalculateCacheSize(string gamePath)
        {
            long totalBytes = 0;

            try
            {
                string logsDir = Path.Combine(gamePath, "logs");
                if (Directory.Exists(logsDir))
                {
                    foreach (var file in Directory.GetFiles(logsDir, "*.*", SearchOption.AllDirectories))
                    {
                        totalBytes += new FileInfo(file).Length;
                    }
                }

                string crashDir = Path.Combine(gamePath, "crash-reports");
                if (Directory.Exists(crashDir))
                {
                    foreach (var file in Directory.GetFiles(crashDir, "*.*", SearchOption.AllDirectories))
                    {
                        totalBytes += new FileInfo(file).Length;
                    }
                }

                string cacheDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Cache");
                if (Directory.Exists(cacheDir))
                {
                    foreach (var file in Directory.GetFiles(cacheDir, "*.*", SearchOption.AllDirectories))
                    {
                        totalBytes += new FileInfo(file).Length;
                    }
                }
            }
            catch { }

            return totalBytes;
        }

        public static double CleanCache(string gamePath)
        {
            long bytesFreed = CalculateCacheSize(gamePath);

            try
            {
                string logsDir = Path.Combine(gamePath, "logs");
                if (Directory.Exists(logsDir)) Directory.Delete(logsDir, true);

                string crashDir = Path.Combine(gamePath, "crash-reports");
                if (Directory.Exists(crashDir)) Directory.Delete(crashDir, true);

                string cacheDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Cache");
                if (Directory.Exists(cacheDir)) Directory.Delete(cacheDir, true);
            }
            catch { }

            return bytesFreed / (1024.0 * 1024.0);
        }
    }
}
