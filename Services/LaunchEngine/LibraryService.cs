using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MinecraftLauncher.Services.LaunchEngine.Models;

namespace MinecraftLauncher.Services.LaunchEngine
{
    public class LibraryService
    {
        private static readonly HttpClient HttpClient = new(new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            MaxConnectionsPerServer = 32,
            AllowAutoRedirect = true
        });

        public async Task<List<string>> EnsureLibrariesAsync(
            string gameRootPath,
            MojangVersionInfo versionInfo,
            string nativesExtractDir,
            IProgress<LaunchProgress>? progress = null)
        {
            var classpathJars = new List<string>();
            string librariesRoot = Path.Combine(gameRootPath, "libraries");
            Directory.CreateDirectory(librariesRoot);
            Directory.CreateDirectory(nativesExtractDir);

            var downloadTasks = new List<(List<string> candidateUrls, string localPath, bool isNative)>();

            foreach (var lib in versionInfo.Libraries)
            {
                if (!IsRuleAllowed(lib.Rules)) continue;

                if (lib.Downloads?.Artifact != null)
                {
                    string localRel = GetArtifactRelativePath(lib.Name, lib.Downloads.Artifact.Url);
                    string localPath = Path.Combine(librariesRoot, localRel);
                    classpathJars.Add(localPath);

                    if (!File.Exists(localPath) || new FileInfo(localPath).Length == 0)
                    {
                        string primaryUrl = lib.Downloads.Artifact.Url;
                        string cleanRel = localRel.Replace('\\', '/');
                        var candidates = new List<string>
                        {
                            primaryUrl,
                            $"https://repo1.maven.org/maven2/{cleanRel}",
                            $"https://bmclapi2.bangbang93.com/maven/{cleanRel}"
                        };
                        downloadTasks.Add((candidates, localPath, false));
                    }
                }
                else if (!string.IsNullOrEmpty(lib.Name))
                {
                    string localRel = CoordinateToRelativePath(lib.Name);
                    string localPath = Path.Combine(librariesRoot, localRel);
                    classpathJars.Add(localPath);

                    if (!File.Exists(localPath) || new FileInfo(localPath).Length == 0)
                    {
                        string cleanRel = localRel.Replace('\\', '/');
                        string baseUrl = string.IsNullOrEmpty(lib.Url) ? "https://libraries.minecraft.net/" : lib.Url;
                        if (!baseUrl.EndsWith("/")) baseUrl += "/";

                        string primaryUrl = baseUrl + cleanRel;
                        var candidates = new List<string> { primaryUrl };

                        if (!primaryUrl.Contains("maven.fabricmc.net"))
                        {
                            candidates.Add($"https://maven.fabricmc.net/{cleanRel}");
                        }
                        candidates.Add($"https://repo1.maven.org/maven2/{cleanRel}");
                        candidates.Add($"https://bmclapi2.bangbang93.com/maven/{cleanRel}");

                        downloadTasks.Add((candidates, localPath, false));
                    }
                }

                if (lib.Natives != null && lib.Natives.TryGetValue("windows", out var classifierKey) && lib.Downloads?.Classifiers != null)
                {
                    classifierKey = classifierKey.Replace("${arch}", Environment.Is64BitOperatingSystem ? "64" : "32");
                    if (lib.Downloads.Classifiers.TryGetValue(classifierKey, out var nativeArtifact))
                    {
                        string nativeRel = GetArtifactRelativePath(lib.Name + "-" + classifierKey, nativeArtifact.Url);
                        string nativePath = Path.Combine(librariesRoot, nativeRel);

                        if (!File.Exists(nativePath) || new FileInfo(nativePath).Length == 0)
                        {
                            string cleanRel = nativeRel.Replace('\\', '/');
                            var candidates = new List<string>
                            {
                                nativeArtifact.Url,
                                $"https://bmclapi2.bangbang93.com/maven/{cleanRel}"
                            };
                            downloadTasks.Add((candidates, nativePath, true));
                        }
                        else
                        {
                            ExtractNativeJar(nativePath, nativesExtractDir);
                        }
                    }
                }
            }

            int total = downloadTasks.Count;
            if (total > 0)
            {
                int completed = 0;
                using var semaphore = new SemaphoreSlim(16);

                var tasks = new List<Task>();
                foreach (var item in downloadTasks)
                {
                    tasks.Add(Task.Run(async () =>
                    {
                        await semaphore.WaitAsync();
                        try
                        {
                            await DownloadFileWithFallbackAsync(item.candidateUrls, item.localPath);
                            if (item.isNative && File.Exists(item.localPath))
                            {
                                ExtractNativeJar(item.localPath, nativesExtractDir);
                            }

                            int cur = Interlocked.Increment(ref completed);
                            int pct = 20 + (int)((cur / (double)total) * 40);
                            progress?.Report(new LaunchProgress
                            {
                                Phase = LaunchPhase.DownloadingLibraries,
                                StatusText = $"Загрузка библиотек ({cur}/{total})...",
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

            string clientJar = Path.Combine(gameRootPath, "versions", versionInfo.Id, $"{versionInfo.Id}.jar");
            if (File.Exists(clientJar) && !classpathJars.Contains(clientJar))
            {
                classpathJars.Add(clientJar);
            }
            else if (!string.IsNullOrEmpty(versionInfo.InheritsFrom))
            {
                string parentClientJar = Path.Combine(gameRootPath, "versions", versionInfo.InheritsFrom, $"{versionInfo.InheritsFrom}.jar");
                if (File.Exists(parentClientJar) && !classpathJars.Contains(parentClientJar))
                {
                    classpathJars.Add(parentClientJar);
                }
            }

            var validClasspath = new List<string>();
            var missingLibraries = new List<string>();

            foreach (var jar in classpathJars)
            {
                if (File.Exists(jar) && new FileInfo(jar).Length > 0)
                {
                    if (!validClasspath.Contains(jar))
                    {
                        validClasspath.Add(jar);
                    }
                }
                else
                {
                    missingLibraries.Add(Path.GetFileName(jar));
                }
            }

            if (missingLibraries.Count > 0)
            {
                bool criticalMissing = missingLibraries.Exists(m =>
                    m.Contains("asm", StringComparison.OrdinalIgnoreCase) ||
                    m.Contains("fabric-loader", StringComparison.OrdinalIgnoreCase) ||
                    m.Contains("sponge-mixin", StringComparison.OrdinalIgnoreCase));

                if (criticalMissing)
                {
                    throw new InvalidOperationException($"Не удалось загрузить важные библиотеки: {string.Join(", ", missingLibraries.GetRange(0, Math.Min(3, missingLibraries.Count)))}. Проверьте подключение к интернету.");
                }
            }

            return validClasspath;
        }

        private static bool IsRuleAllowed(List<RuleInfo>? rules)
        {
            if (rules == null || rules.Count == 0) return true;

            bool allowed = false;
            foreach (var rule in rules)
            {
                if (rule.Action == "allow")
                {
                    if (rule.Os == null || rule.Os.Name == "windows")
                    {
                        allowed = true;
                    }
                }
                else if (rule.Action == "disallow")
                {
                    if (rule.Os != null && rule.Os.Name == "windows")
                    {
                        allowed = false;
                    }
                }
            }
            return allowed;
        }

        private static string CoordinateToRelativePath(string coordinate)
        {
            var parts = coordinate.Split(':');
            if (parts.Length < 3) return coordinate;

            string group = parts[0].Replace('.', Path.DirectorySeparatorChar);
            string artifact = parts[1];
            string version = parts[2];
            string classifier = parts.Length > 3 ? $"-{parts[3]}" : "";

            return Path.Combine(group, artifact, version, $"{artifact}-{version}{classifier}.jar");
        }

        private static string GetArtifactRelativePath(string name, string url)
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                string path = uri.AbsolutePath.TrimStart('/');
                if (path.StartsWith("maven/")) path = path.Substring(6);
                return path.Replace('/', Path.DirectorySeparatorChar);
            }
            return CoordinateToRelativePath(name);
        }

        private static void ExtractNativeJar(string jarPath, string extractTo)
        {
            try
            {
                using var zip = ZipFile.OpenRead(jarPath);
                foreach (var entry in zip.Entries)
                {
                    if (entry.FullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) && !entry.FullName.Contains('/'))
                    {
                        string dest = Path.Combine(extractTo, entry.Name);
                        entry.ExtractToFile(dest, true);
                    }
                }
            }
            catch { }
        }

        private static async Task DownloadFileWithFallbackAsync(IEnumerable<string> candidateUrls, string destinationPath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

            foreach (var url in candidateUrls)
            {
                if (string.IsNullOrWhiteSpace(url)) continue;

                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    var response = await HttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                    if (response.IsSuccessStatusCode)
                    {
                        byte[] data = await response.Content.ReadAsByteArrayAsync(cts.Token);
                        if (data.Length > 0)
                        {
                            await File.WriteAllBytesAsync(destinationPath, data);
                            return;
                        }
                    }
                }
                catch { }
            }
        }
    }
}
