using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using MinecraftLauncher.Services.LaunchEngine.Models;

namespace MinecraftLauncher.Services.LaunchEngine
{
    public class MojangManifestService
    {
        private static readonly HttpClient HttpClient = new(new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            MaxConnectionsPerServer = 32
        });

        private const string PrimaryManifestUrl = "https://piston-meta.mojang.com/mc/game/version_manifest_v2.json";
        private const string MirrorManifestUrl = "https://bmclapi2.bangbang93.com/mc/game/version_manifest_v2.json";

        public async Task<MojangManifest?> GetManifestAsync()
        {
            string? json = await FetchWithFallbackAsync(PrimaryManifestUrl, MirrorManifestUrl);
            if (string.IsNullOrEmpty(json)) return null;

            return JsonSerializer.Deserialize<MojangManifest>(json);
        }

        public async Task<MojangVersionInfo?> ResolveVersionInfoAsync(string gameRootPath, string versionId)
        {
            string versionDir = Path.Combine(gameRootPath, "versions", versionId);
            string versionJsonPath = Path.Combine(versionDir, $"{versionId}.json");

            MojangVersionInfo? info = null;

            if (File.Exists(versionJsonPath))
            {
                try
                {
                    string localJson = await File.ReadAllTextAsync(versionJsonPath);
                    info = JsonSerializer.Deserialize<MojangVersionInfo>(localJson);
                }
                catch { }
            }

            if (info == null)
            {
                var manifest = await GetManifestAsync();
                var verHeader = manifest?.Versions.Find(v => v.Id.Equals(versionId, StringComparison.OrdinalIgnoreCase));
                if (verHeader != null && !string.IsNullOrEmpty(verHeader.Url))
                {
                    string mirrorUrl = verHeader.Url.Replace("https://piston-meta.mojang.com", "https://bmclapi2.bangbang93.com");
                    string? json = await FetchWithFallbackAsync(verHeader.Url, mirrorUrl);
                    if (!string.IsNullOrEmpty(json))
                    {
                        Directory.CreateDirectory(versionDir);
                        await File.WriteAllTextAsync(versionJsonPath, json);
                        info = JsonSerializer.Deserialize<MojangVersionInfo>(json);
                    }
                }
            }

            if (info != null && !string.IsNullOrEmpty(info.InheritsFrom))
            {
                var parentInfo = await ResolveVersionInfoAsync(gameRootPath, info.InheritsFrom);
                if (parentInfo != null)
                {
                    MergeVersionInfo(info, parentInfo);
                }
            }

            return info;
        }

        public async Task EnsureClientJarAsync(string gameRootPath, MojangVersionInfo versionInfo, IProgress<LaunchProgress>? progress = null)
        {
            if (versionInfo.Downloads?.Client == null) return;

            string targetVersionId = !string.IsNullOrEmpty(versionInfo.InheritsFrom) ? versionInfo.InheritsFrom : versionInfo.Id;
            string clientJarPath = Path.Combine(gameRootPath, "versions", targetVersionId, $"{targetVersionId}.jar");
            var clientDownload = versionInfo.Downloads.Client;

            if (File.Exists(clientJarPath) && new FileInfo(clientJarPath).Length == clientDownload.Size)
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(clientJarPath)!);
            progress?.Report(new LaunchProgress
            {
                Phase = LaunchPhase.CheckingVersionMetadata,
                StatusText = $"Загрузка ядра Minecraft {targetVersionId}...",
                Percentage = 10
            });

            string mirrorUrl = clientDownload.Url.Replace("https://piston-data.mojang.com", "https://bmclapi2.bangbang93.com")
                                               .Replace("https://launcher.mojang.com", "https://bmclapi2.bangbang93.com");
            byte[]? data = await DownloadBytesWithFallbackAsync(mirrorUrl, clientDownload.Url);

            if (data != null)
            {
                await File.WriteAllBytesAsync(clientJarPath, data);
            }
        }

        private static void MergeVersionInfo(MojangVersionInfo child, MojangVersionInfo parent)
        {
            if (child.AssetIndex == null) child.AssetIndex = parent.AssetIndex;
            if (string.IsNullOrEmpty(child.Assets)) child.Assets = parent.Assets;
            if (child.Downloads == null) child.Downloads = parent.Downloads;

            if (string.IsNullOrEmpty(child.MinecraftArguments))
            {
                child.MinecraftArguments = parent.MinecraftArguments;
            }

            if (child.Arguments == null)
            {
                child.Arguments = parent.Arguments;
            }
            else if (parent.Arguments != null)
            {
                if (child.Arguments.Game == null || child.Arguments.Game.Count == 0)
                {
                    child.Arguments.Game = parent.Arguments.Game;
                }
                else if (parent.Arguments.Game != null && parent.Arguments.Game.Count > 0)
                {
                    var combinedGame = new List<object>(parent.Arguments.Game);
                    combinedGame.AddRange(child.Arguments.Game);
                    child.Arguments.Game = combinedGame;
                }

                if (child.Arguments.Jvm == null || child.Arguments.Jvm.Count == 0)
                {
                    child.Arguments.Jvm = parent.Arguments.Jvm;
                }
                else if (parent.Arguments.Jvm != null && parent.Arguments.Jvm.Count > 0)
                {
                    var combinedJvm = new List<object>(child.Arguments.Jvm);
                    combinedJvm.AddRange(parent.Arguments.Jvm);
                    child.Arguments.Jvm = combinedJvm;
                }
            }

            if (parent.Libraries != null && parent.Libraries.Count > 0)
            {
                var combinedLibs = new List<LibraryInfo>(child.Libraries);
                foreach (var parentLib in parent.Libraries)
                {
                    if (!combinedLibs.Exists(l => l.Name == parentLib.Name))
                    {
                        combinedLibs.Add(parentLib);
                    }
                }
                child.Libraries = combinedLibs;
            }
        }

        private static async Task<string?> FetchWithFallbackAsync(string primaryUrl, string? fallbackUrl)
        {
            try
            {
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(8));
                return await HttpClient.GetStringAsync(primaryUrl, cts.Token);
            }
            catch
            {
                if (!string.IsNullOrEmpty(fallbackUrl))
                {
                    try
                    {
                        using var cts2 = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(8));
                        return await HttpClient.GetStringAsync(fallbackUrl, cts2.Token);
                    }
                    catch { }
                }
            }
            return null;
        }

        private static async Task<byte[]?> DownloadBytesWithFallbackAsync(string primaryUrl, string? fallbackUrl)
        {
            try
            {
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(15));
                return await HttpClient.GetByteArrayAsync(primaryUrl, cts.Token);
            }
            catch
            {
                if (!string.IsNullOrEmpty(fallbackUrl))
                {
                    try
                    {
                        using var cts2 = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(15));
                        return await HttpClient.GetByteArrayAsync(fallbackUrl, cts2.Token);
                    }
                    catch { }
                }
            }
            return null;
        }
    }
}
