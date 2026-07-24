using System;
using System.Collections.Generic;

namespace MinecraftLauncher
{
    public static class JvmOptimizationHelper
    {
        public static string[] GetOptimizedJvmArguments(int ramMb, string preset, string customArgs = "")
        {
            var args = new List<string>();

            // Базовые параметры памяти
            args.Add($"-Xms{Math.Max(1024, ramMb / 2)}M");
            args.Add($"-Xmx{ramMb}M");

            switch (preset.ToLowerInvariant())
            {
                case "aikar":
                    // Флаги Айкара для обеспечения ровного FPS без фризов
                    args.AddRange(new[]
                    {
                        "-XX:+UseG1GC",
                        "-XX:+ParallelRefProcEnabled",
                        "-XX:MaxGCPauseMillis=200",
                        "-XX:+UnlockExperimentalVMOptions",
                        "-XX:+DisableExplicitGC",
                        "-XX:+AlwaysPreTouch",
                        "-XX:G1NewSizePercent=30",
                        "-XX:G1MaxNewSizePercent=40",
                        "-XX:G1HeapRegionSize=8M",
                        "-XX:G1ReservePercent=20",
                        "-XX:G1HeapWastePercent=5",
                        "-XX:G1MixedGCCountTarget=4",
                        "-XX:InitiatingHeapOccupancyPercent=15",
                        "-XX:G1MixedGCLiveThresholdPercent=90",
                        "-XX:G1RSetUpdatingPauseTimePercent=5",
                        "-XX:SurviorRatio=32",
                        "-XX:+PerfDisableSharedMem"
                    });
                    break;

                case "zgc_shenandoah":
                    // Низколатентный сборщик мусора ZGC / Shenandoah (Java 17+)
                    args.AddRange(new[]
                    {
                        "-XX:+UseZGC",
                        "-XX:+UnlockExperimentalVMOptions",
                        "-XX:+AlwaysPreTouch",
                        "-XX:+DisableExplicitGC",
                        "-XX:ZAllocationSpikeTolerance=5"
                    });
                    break;

                default:
                    // Стандартный G1GC
                    args.AddRange(new[]
                    {
                        "-XX:+UseG1GC",
                        "-XX:+UnlockExperimentalVMOptions",
                        "-XX:MaxGCPauseMillis=100",
                        "-XX:+DisableExplicitGC"
                    });
                    break;
            }

            if (!string.IsNullOrWhiteSpace(customArgs))
            {
                var customSplit = customArgs.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                args.AddRange(customSplit);
            }

            return args.ToArray();
        }
    }
}
