using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MinecraftLauncher.Services.LaunchEngine.Models;

namespace MinecraftLauncher.Services.LaunchEngine
{
    public class AssetService
    {
        private static readonly HttpClient HttpClient = new(new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            MaxConnectionsPerServer = 32
        });

        private const string PrimaryResourceHost = "https://resources.download.minecraft.net";
        private const string MirrorResourceHost = "https://bmclapi2.bangbang93.com/assets";

        public async Task EnsureAssetsAsync(string gameRootPath, MojangVersionInfo versionInfo, IProgress<LaunchProgress>? progress = null)
        {
            if (versionInfo.AssetIndex == null || string.IsNullOrEmpty(versionInfo.AssetIndex.Url))
                return;

            string assetsRoot = Path.Combine(gameRootPath, "assets");
            string indexesDir = Path.Combine(assetsRoot, "indexes");
            string objectsDir = Path.Combine(assetsRoot, "objects");
            Directory.CreateDirectory(indexesDir);
            Directory.CreateDirectory(objectsDir);

            string indexFilePath = Path.Combine(indexesDir, $"{versionInfo.AssetIndex.Id}.json");

            if (!File.Exists(indexFilePath))
            {
                progress?.Report(new LaunchProgress
                {
                    Phase = LaunchPhase.DownloadingAssets,
                    StatusText = "Загрузка индекса ресурсов игры...",
                    Percentage = 62
                });

                string mirrorIndexUrl = versionInfo.AssetIndex.Url.Replace("https://piston-meta.mojang.com", "https://bmclapi2.bangbang93.com")
                                                                  .Replace("https://launchermeta.mojang.com", "https://bmclapi2.bangbang93.com");
                string? indexJson = await FetchStringWithFallbackAsync(versionInfo.AssetIndex.Url, mirrorIndexUrl);
                if (string.IsNullOrEmpty(indexJson)) return;

                await File.WriteAllTextAsync(indexFilePath, indexJson);
            }

            string rawIndex = await File.ReadAllTextAsync(indexFilePath);
            using var doc = JsonDocument.Parse(rawIndex);
            if (!doc.RootElement.TryGetProperty("objects", out var objectsElement)) return;

            var missingObjects = new List<(string hash, long size, string localPath)>();

            foreach (var prop in objectsElement.EnumerateObject())
            {
                if (prop.Value.TryGetProperty("hash", out var hashEl))
                {
                    string hash = hashEl.GetString() ?? "";
                    if (hash.Length >= 2)
                    {
                        string subFolder = hash.Substring(0, 2);
                        string localPath = Path.Combine(objectsDir, subFolder, hash);
                        long size = prop.Value.TryGetProperty("size", out var s) ? s.GetInt64() : 0;

                        if (!File.Exists(localPath) || (size > 0 && new FileInfo(localPath).Length != size))
                        {
                            missingObjects.Add((hash, size, localPath));
                        }
                    }
                }
            }

            int total = missingObjects.Count;
            if (total > 0)
            {
                int completed = 0;
                using var semaphore = new SemaphoreSlim(24);
                var tasks = new List<Task>();

                foreach (var obj in missingObjects)
                {
                    tasks.Add(Task.Run(async () =>
                    {
                        await semaphore.WaitAsync();
                        try
                        {
                            string sub = obj.hash.Substring(0, 2);
                            string primaryUrl = $"{PrimaryResourceHost}/{sub}/{obj.hash}";
                            string mirrorUrl = $"{MirrorResourceHost}/{sub}/{obj.hash}";

                            await DownloadFileWithFallbackAsync(primaryUrl, mirrorUrl, obj.localPath);

                            int cur = Interlocked.Increment(ref completed);
                            int pct = 65 + (int)((cur / (double)total) * 25);
                            progress?.Report(new LaunchProgress
                            {
                                Phase = LaunchPhase.DownloadingAssets,
                                StatusText = $"Загрузка ресурсов и звуков ({cur}/{total})...",
                                Percentage = pct
                            });
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    }));
                }

                await Task.WhenAll(tasks);
            }
        }

        private static async Task<string?> FetchStringWithFallbackAsync(string primaryUrl, string? fallbackUrl)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                return await HttpClient.GetStringAsync(primaryUrl, cts.Token);
            }
            catch
            {
                if (!string.IsNullOrEmpty(fallbackUrl))
                {
                    try
                    {
                        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                        return await HttpClient.GetStringAsync(fallbackUrl, cts2.Token);
                    }
                    catch { }
                }
            }
            return null;
        }

        private static async Task DownloadFileWithFallbackAsync(string primaryUrl, string? fallbackUrl, string destinationPath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                byte[] data = await HttpClient.GetByteArrayAsync(primaryUrl, cts.Token);
                await File.WriteAllBytesAsync(destinationPath, data);
            }
            catch
            {
                if (!string.IsNullOrEmpty(fallbackUrl))
                {
                    try
                    {
                        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                        byte[] data = await HttpClient.GetByteArrayAsync(fallbackUrl, cts2.Token);
                        await File.WriteAllBytesAsync(destinationPath, data);
                    }
                    catch { }
                }
            }
        }
    }
}
