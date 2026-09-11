using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MinecraftLauncher.Models;
using MinecraftLauncher.Services.LaunchEngine.Models;

namespace MinecraftLauncher.Services.LaunchEngine
{
    public class MrPackInstaller
    {
        private static readonly HttpClient HttpClient = new(new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            MaxConnectionsPerServer = 16
        });

        public static async Task<ModpackProfile> InstallMrPackAsync(
            string mrPackFilePath,
            string gameRootPath,
            IProgress<LaunchProgress>? progress = null)
        {
            if (!File.Exists(mrPackFilePath))
                throw new FileNotFoundException("Файл .mrpack не найден.", mrPackFilePath);

            using var archive = ZipFile.OpenRead(mrPackFilePath);
            var indexEntry = archive.GetEntry("modrinth.index.json");
            if (indexEntry == null)
                throw new InvalidDataException("В архиве отсутствует modrinth.index.json");

            using var indexStream = indexEntry.Open();
            using var doc = await JsonDocument.ParseAsync(indexStream);
            var root = doc.RootElement;

            string packName = root.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? "" : "";
            if (string.IsNullOrWhiteSpace(packName))
                packName = Path.GetFileNameWithoutExtension(mrPackFilePath);

            string mcVersion = "1.20.1";
            string loader = "Fabric";

            if (root.TryGetProperty("dependencies", out var depsEl))
            {
                if (depsEl.TryGetProperty("minecraft", out var mcEl))
                    mcVersion = mcEl.GetString() ?? mcVersion;

                if (depsEl.TryGetProperty("fabric-loader", out _))
                    loader = "Fabric";
                else if (depsEl.TryGetProperty("quilt-loader", out _))
                    loader = "Quilt";
                else if (depsEl.TryGetProperty("forge", out _) || depsEl.TryGetProperty("neoforge", out _))
                    loader = "Forge";
            }

            string instanceDir = Path.Combine(gameRootPath, "instances", packName);
            if (Directory.Exists(instanceDir))
            {
                instanceDir += "_" + DateTime.Now.ToString("HHmmss");
            }
            Directory.CreateDirectory(instanceDir);

            foreach (var entry in archive.Entries)
            {
                if (entry.FullName.StartsWith("overrides/", StringComparison.OrdinalIgnoreCase))
                {
                    string relPath = entry.FullName.Substring(10);
                    if (string.IsNullOrEmpty(relPath) || relPath.EndsWith("/")) continue;

                    string destPath = Path.Combine(instanceDir, relPath);
                    Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                    entry.ExtractToFile(destPath, true);
                }
            }

            if (root.TryGetProperty("files", out var filesEl))
            {
                var filesToDownload = new System.Collections.Generic.List<(string url, string destPath)>();
                foreach (var fileEl in filesEl.EnumerateArray())
                {
                    string path = fileEl.GetProperty("path").GetString() ?? "";
                    if (fileEl.TryGetProperty("downloads", out var downloadsEl) && downloadsEl.GetArrayLength() > 0)
                    {
                        string downloadUrl = downloadsEl[0].GetString() ?? "";
                        if (!string.IsNullOrEmpty(downloadUrl) && !string.IsNullOrEmpty(path))
                        {
                            filesToDownload.Add((downloadUrl, Path.Combine(instanceDir, path)));
                        }
                    }
                }

                int total = filesToDownload.Count;
                int completed = 0;
                using var semaphore = new SemaphoreSlim(8);
                var tasks = new System.Collections.Generic.List<Task>();

                foreach (var item in filesToDownload)
                {
                    tasks.Add(Task.Run(async () =>
                    {
                        await semaphore.WaitAsync();
                        try
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(item.destPath)!);
                            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                            byte[] data = await HttpClient.GetByteArrayAsync(item.url, cts.Token);
                            await File.WriteAllBytesAsync(item.destPath, data);

                            int cur = Interlocked.Increment(ref completed);
                            progress?.Report(new LaunchProgress
                            {
                                Phase = LaunchPhase.DownloadingAssets,
                                StatusText = $"Импорт модов сборки ({cur}/{total})...",
                                Percentage = (int)((cur / (double)total) * 100)
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

            return new ModpackProfile
            {
                Name = packName,
                GameVersion = mcVersion,
                Loader = loader,
                FolderPath = instanceDir
            };
        }
    }
}
