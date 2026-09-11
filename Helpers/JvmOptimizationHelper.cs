using System;
using System.Collections.Generic;

namespace MinecraftLauncher.Helpers
{
    public static class JvmOptimizationHelper
    {
        public static string[] GetOptimizedJvmArguments(int ramMb, string preset, string customArgs = "")
        {
            var args = new List<string>
            {
                $"-Xms{Math.Max(1024, ramMb / 2)}M",
                $"-Xmx{ramMb}M"
            };

            switch (preset.ToLowerInvariant())
            {
                case "aikar":
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
                        "-XX:SurvivorRatio=32",
                        "-XX:+PerfDisableSharedMem"
                    });
                    break;

                case "zgc_shenandoah":
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
