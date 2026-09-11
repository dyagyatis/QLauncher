using System;
using System.Collections.Generic;

namespace MinecraftLauncher.Services.LaunchEngine.Models
{
    public class LaunchOptions
    {
        public string GameRootPath { get; set; } = "";
        public string InstancePath { get; set; } = "";
        public string VersionId { get; set; } = "";
        public string JavaPath { get; set; } = "";
        public string PlayerName { get; set; } = "Player";
        public string Uuid { get; set; } = "";
        public string AccessToken { get; set; } = "offline";
        public int RamMb { get; set; } = 4096;
        public string JvmPreset { get; set; } = "default";
        public string CustomJvmArgs { get; set; } = "";
        public int ScreenWidth { get; set; } = 1920;
        public int ScreenHeight { get; set; } = 1080;
        public bool IsFullScreen { get; set; }
        public string GpuPreference { get; set; } = "HighPerformance";
        public bool IsDemo { get; set; }
        public string? QuickPlaySingleplayer { get; set; }
        public string? QuickPlayMultiplayer { get; set; }
        public string? QuickPlayRealms { get; set; }
        public string? ServerIp { get; set; }
        public int? ServerPort { get; set; }
        public Dictionary<string, string> CustomVariables { get; set; } = new();
    }

    public enum LaunchPhase
    {
        Initializing,
        CheckingVersionMetadata,
        DownloadingLibraries,
        DownloadingAssets,
        BuildingArguments,
        StartingProcess,
        Completed,
        Failed
    }

    public class LaunchProgress
    {
        public LaunchPhase Phase { get; set; }
        public string StatusText { get; set; } = "";
        public int Percentage { get; set; }
        public long CurrentBytes { get; set; }
        public long TotalBytes { get; set; }
    }
}
