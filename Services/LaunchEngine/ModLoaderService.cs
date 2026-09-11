using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace MinecraftLauncher.Services.LaunchEngine
{
    public class ModLoaderService
    {
        private static readonly HttpClient HttpClient = new(new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(10)
        });

        public async Task<string> InstallFabricAsync(string gameVersion, string gameRootPath)
        {
            string primaryMetaUrl = $"https://meta.fabricmc.net/v2/versions/loader/{gameVersion}";
            string mirrorMetaUrl = $"https://bmclapi2.bangbang93.com/fabric-meta/v2/versions/loader/{gameVersion}";

            string? metaJson = await FetchStringWithFallbackAsync(primaryMetaUrl, mirrorMetaUrl);
            if (string.IsNullOrEmpty(metaJson))
            {
                throw new Exception($"Не удалось получить данные Fabric Meta для версии {gameVersion}");
            }

            using var doc = JsonDocument.Parse(metaJson);
            var array = doc.RootElement;
            if (array.GetArrayLength() == 0)
            {
                throw new Exception($"Fabric не найден для версии Minecraft {gameVersion}");
            }

            string loaderVersion = array[0].GetProperty("loader").GetProperty("version").GetString() ?? "";
            string profileId = $"fabric-loader-{loaderVersion}-{gameVersion}";

            string primaryProfileUrl = $"https://meta.fabricmc.net/v2/versions/loader/{gameVersion}/{loaderVersion}/profile/json";
            string mirrorProfileUrl = $"https://bmclapi2.bangbang93.com/fabric-meta/v2/versions/loader/{gameVersion}/{loaderVersion}/profile/json";

            string? profileJson = await FetchStringWithFallbackAsync(primaryProfileUrl, mirrorProfileUrl);
            if (string.IsNullOrEmpty(profileJson))
            {
                throw new Exception("Не удалось загрузить профиль Fabric JSON");
            }

            string versionDir = Path.Combine(gameRootPath, "versions", profileId);
            Directory.CreateDirectory(versionDir);

            string versionJsonPath = Path.Combine(versionDir, $"{profileId}.json");
            await File.WriteAllTextAsync(versionJsonPath, profileJson);

            return profileId;
        }

        public async Task<string> InstallQuiltAsync(string gameVersion, string gameRootPath)
        {
            string primaryMetaUrl = $"https://meta.quiltmc.net/v3/versions/loader/{gameVersion}";
            string? metaJson = await FetchStringWithFallbackAsync(primaryMetaUrl, null);
            if (string.IsNullOrEmpty(metaJson))
            {
                throw new Exception($"Не удалось получить данные Quilt Meta для версии {gameVersion}");
            }

            using var doc = JsonDocument.Parse(metaJson);
            var array = doc.RootElement;
            if (array.GetArrayLength() == 0)
            {
                throw new Exception($"Quilt не найден для версии Minecraft {gameVersion}");
            }

            string loaderVersion = array[0].GetProperty("loader").GetProperty("version").GetString() ?? "";
            string profileId = $"quilt-loader-{loaderVersion}-{gameVersion}";

            string profileUrl = $"https://meta.quiltmc.net/v3/versions/loader/{gameVersion}/{loaderVersion}/profile/json";
            string? profileJson = await FetchStringWithFallbackAsync(profileUrl, null);
            if (string.IsNullOrEmpty(profileJson))
            {
                throw new Exception("Не удалось загрузить профиль Quilt JSON");
            }

            string versionDir = Path.Combine(gameRootPath, "versions", profileId);
            Directory.CreateDirectory(versionDir);

            string versionJsonPath = Path.Combine(versionDir, $"{profileId}.json");
            await File.WriteAllTextAsync(versionJsonPath, profileJson);

            return profileId;
        }

        private static async Task<string?> FetchStringWithFallbackAsync(string primaryUrl, string? fallbackUrl)
        {
            try
            {
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(10));
                return await HttpClient.GetStringAsync(primaryUrl, cts.Token);
            }
            catch
            {
                if (!string.IsNullOrEmpty(fallbackUrl))
                {
                    try
                    {
                        using var cts2 = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(10));
                        return await HttpClient.GetStringAsync(fallbackUrl, cts2.Token);
                    }
                    catch { }
                }
            }
            return null;
        }
    }
}
