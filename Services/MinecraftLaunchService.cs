using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using MinecraftLauncher.Models;
using MinecraftLauncher.Services.LaunchEngine;
using MinecraftLauncher.Services.LaunchEngine.Models;

namespace MinecraftLauncher.Services
{
    public interface IMinecraftLaunchService
    {
        event Action<int, int>? FileProgressChanged;
        event Action<long, long, double, string>? ByteProgressChanged;

        void Initialize(string gamePath);
        Task<List<string>> GetAllReleaseVersionsAsync();
        Task<Process> LaunchGameAsync(
            string launchVersion,
            AccountProfile account,
            LauncherSettings settings,
            string? autoJoinServerIp,
            Action<string>? statusCallback);
        Task<string> InstallFabricAsync(string gameVersion, string gamePath);
        Task<string> InstallQuiltAsync(string gameVersion, string gamePath);
        Task<string> InstallForgeAsync(string gameVersion);
        Task<string> InstallNeoForgeAsync(string gameVersion);
        Task DownloadModrinthModAsync(string slug, string gameVersion, string modsPath);
    }

    public class MinecraftLaunchService : IMinecraftLaunchService
    {
        private readonly IMinecraftLaunchEngine _engine;
        private readonly IAntiBlockService _antiBlockService;
        private readonly IJavaService _javaService;
        private readonly ISettingsService _settingsService;

        private string _gamePath = "";

        public event Action<int, int>? FileProgressChanged;
        public event Action<long, long, double, string>? ByteProgressChanged;

        public static MinecraftLaunchService Instance { get; } = new MinecraftLaunchService(
            MinecraftLaunchEngine.Instance,
            AntiBlockService.Instance,
            JavaService.Instance,
            SettingsService.Instance);

        public MinecraftLaunchService(
            IMinecraftLaunchEngine engine,
            IAntiBlockService antiBlockService,
            IJavaService javaService,
            ISettingsService settingsService)
        {
            _engine = engine;
            _antiBlockService = antiBlockService;
            _javaService = javaService;
            _settingsService = settingsService;
        }

        public void Initialize(string gamePath)
        {
            _gamePath = gamePath;
            Directory.CreateDirectory(_gamePath);
            _antiBlockService.FixAllFabricVersions(_gamePath);
        }

        public async Task<List<string>> GetAllReleaseVersionsAsync()
        {
            var result = new List<string>();

            string versionsDir = Path.Combine(_gamePath, "versions");
            if (Directory.Exists(versionsDir))
            {
                foreach (var dir in Directory.GetDirectories(versionsDir))
                {
                    string verName = Path.GetFileName(dir);
                    string jsonPath = Path.Combine(dir, $"{verName}.json");
                    if (File.Exists(jsonPath))
                    {
                        result.Add(verName);
                    }
                }
            }

            try
            {
                var manifest = await _engine.GetManifestAsync();
                if (manifest != null)
                {
                    foreach (var v in manifest.Versions)
                    {
                        if (v.Type == "release" && !result.Contains(v.Id))
                        {
                            result.Add(v.Id);
                        }
                    }
                }
            }
            catch { }

            return result;
        }

        public async Task<Process> LaunchGameAsync(
            string launchVersion,
            AccountProfile account,
            LauncherSettings settings,
            string? autoJoinServerIp,
            Action<string>? statusCallback)
        {
            if (string.IsNullOrEmpty(_gamePath))
            {
                Initialize(settings.GamePath);
            }

            string cleanVersion = launchVersion.Replace("⭐", "").Trim();
            string candidatePackName = cleanVersion;
            if (candidatePackName.Contains(" ("))
            {
                candidatePackName = candidatePackName.Substring(0, candidatePackName.LastIndexOf(" (")).Trim();
            }

            var pack = settings.Modpacks.Find(p =>
                p.Name.Equals(candidatePackName, StringComparison.OrdinalIgnoreCase) ||
                $"{p.Name} ({p.Loader})".Equals(cleanVersion, StringComparison.OrdinalIgnoreCase) ||
                p.Name.Equals(cleanVersion, StringComparison.OrdinalIgnoreCase));

            string realVersionId = cleanVersion;
            string? instanceFolderPath = null;
            string targetGameVer = "1.20.1";

            if (pack != null)
            {
                instanceFolderPath = pack.FolderPath;
                targetGameVer = !string.IsNullOrWhiteSpace(pack.GameVersion)
                    ? pack.GameVersion
                    : "1.20.1";

                if (string.Equals(pack.Loader, "Fabric", StringComparison.OrdinalIgnoreCase))
                {
                    statusCallback?.Invoke("Подготовка профиля Fabric...");
                    realVersionId = await _engine.InstallFabricAsync(targetGameVer, _gamePath);
                }
                else if (string.Equals(pack.Loader, "Quilt", StringComparison.OrdinalIgnoreCase))
                {
                    statusCallback?.Invoke("Подготовка профиля Quilt...");
                    realVersionId = await _engine.InstallQuiltAsync(targetGameVer, _gamePath);
                }
                else
                {
                    realVersionId = targetGameVer;
                }
            }
            else
            {
                targetGameVer = cleanVersion;
                realVersionId = cleanVersion;
            }

            statusCallback?.Invoke("Проверка среды Java...");
            string javaPath = await _javaService.ResolveJavaExecutableAsync(settings.JavaPath, targetGameVer, statusCallback);

            string? serverIp = null;
            int? serverPort = null;
            if (!string.IsNullOrWhiteSpace(autoJoinServerIp))
            {
                serverIp = autoJoinServerIp;
                serverPort = 25565;
                if (serverIp.Contains(':'))
                {
                    var parts = serverIp.Split(':');
                    serverIp = parts[0];
                    if (int.TryParse(parts[1], out int p)) serverPort = p;
                }
            }

            var launchOptions = new LaunchOptions
            {
                GameRootPath = settings.GamePath,
                InstancePath = instanceFolderPath ?? settings.GamePath,
                VersionId = realVersionId,
                JavaPath = javaPath,
                PlayerName = account.Nickname,
                Uuid = account.Uuid,
                AccessToken = account.AccessToken,
                RamMb = settings.RamMb,
                JvmPreset = settings.JvmPreset,
                CustomJvmArgs = settings.CustomJvmArgs,
                ScreenWidth = settings.ScreenWidth,
                ScreenHeight = settings.ScreenHeight,
                IsFullScreen = settings.IsFullScreen,
                GpuPreference = settings.GpuPreference,
                ServerIp = serverIp,
                ServerPort = serverPort
            };

            var progress = new Progress<LaunchProgress>(p =>
            {
                if (!string.IsNullOrEmpty(p.StatusText))
                {
                    statusCallback?.Invoke(p.StatusText);
                }
                if (p.Percentage > 0)
                {
                    FileProgressChanged?.Invoke(p.Percentage, 100);
                    ByteProgressChanged?.Invoke(p.CurrentBytes, p.TotalBytes, p.Percentage / 100.0, "");
                }
            });

            return await _engine.LaunchAsync(launchOptions, progress);
        }

        public Task<string> InstallFabricAsync(string gameVersion, string gamePath) =>
            _engine.InstallFabricAsync(gameVersion, gamePath);

        public Task<string> InstallQuiltAsync(string gameVersion, string gamePath) =>
            _engine.InstallQuiltAsync(gameVersion, gamePath);

        public Task<string> InstallForgeAsync(string gameVersion) =>
            Task.FromResult(gameVersion);

        public Task<string> InstallNeoForgeAsync(string gameVersion) =>
            Task.FromResult(gameVersion);

        public async Task DownloadModrinthModAsync(string slug, string gameVersion, string modsPath)
        {
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "QLauncher/2.0");

                string url = $"https://api.modrinth.com/v2/project/{slug}/version?game_versions=[\"{gameVersion}\"]&loaders=[\"fabric\"]";
                string json = await client.GetStringAsync(url);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.GetArrayLength() > 0)
                {
                    JsonElement targetVersion = root[0];
                    foreach (var verItem in root.EnumerateArray())
                    {
                        if (verItem.TryGetProperty("version_type", out var vt) && vt.GetString() == "release")
                        {
                            targetVersion = verItem;
                            break;
                        }
                    }

                    var files = targetVersion.GetProperty("files");
                    if (files.GetArrayLength() > 0)
                    {
                        string downloadUrl = files[0].GetProperty("url").GetString() ?? "";
                        string fileName = files[0].GetProperty("filename").GetString() ?? "";
                        string filePath = Path.Combine(modsPath, fileName);

                        Directory.CreateDirectory(modsPath);
                        byte[] fileBytes = await client.GetByteArrayAsync(downloadUrl);
                        await File.WriteAllBytesAsync(filePath, fileBytes);
                    }
                }
            }
            catch { }
        }
    }
}
