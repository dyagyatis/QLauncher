using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace MinecraftLauncher
{
    public static class AntiBlockManager
    {
        // BMCLAPI - Официальное доверенное зеркало ресурсов Minecraft / Fabric / Forge
        public const string BmclapiFabricMeta = "https://bmclapi2.bangbang93.com/fabric-meta";
        public const string BmclapiMcManifest = "https://bmclapi2.bangbang93.com/mc/game/version_manifest_v2.json";

        /// <summary>
        /// Устанавливает Fabric Loader напрямую через зеркало BMCLAPI в обход DPI и блокировок провайдера (Без VPN)
        /// </summary>
        public static async Task<string> InstallFabricViaMirrorAsync(HttpClient client, string gameVersion, string minecraftBasePath)
        {
            // 1. Получаем доступные версии лоадера через зеркало BMCLAPI
            string url = $"{BmclapiFabricMeta}/v2/versions/loader/{gameVersion}";
            string jsonStr = await client.GetStringAsync(url);

            using var doc = JsonDocument.Parse(jsonStr);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0)
                throw new Exception($"Не найдена версия Fabric для Minecraft {gameVersion}");

            var firstItem = root[0];
            string loaderVersion = firstItem.GetProperty("loader").GetProperty("version").GetString() ?? "";

            if (string.IsNullOrEmpty(loaderVersion))
                throw new Exception("Не удалось распарсить версию Fabric Loader");

            // 2. Скачиваем готовый профиль версии JSON через зеркало
            string profileUrl = $"{BmclapiFabricMeta}/v2/versions/loader/{gameVersion}/{loaderVersion}/profile/json";
            string profileJson = await client.GetStringAsync(profileUrl);

            using var profileDoc = JsonDocument.Parse(profileJson);
            string versionId = profileDoc.RootElement.GetProperty("id").GetString() ?? $"fabric-loader-{loaderVersion}-{gameVersion}";

            // 3. Сохраняем в папку .minecraft/versions/
            string targetDir = Path.Combine(minecraftBasePath, "versions", versionId);
            Directory.CreateDirectory(targetDir);

            string targetJsonPath = Path.Combine(targetDir, $"{versionId}.json");
            File.WriteAllText(targetJsonPath, profileJson);

            return versionId;
        }

        /// <summary>
        /// Выполняет HTTP-запрос. Если основной URL заблокирован провайдером,
        /// автоматически переключается на рабочее зеркало (Mirror Fallback).
        /// </summary>
        public static async Task<string> FetchWithFallbackAsync(HttpClient client, string primaryUrl, string? fallbackUrl = null)
        {
            try
            {
                var response = await client.GetAsync(primaryUrl);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsStringAsync();
                }
            }
            catch { }

            if (!string.IsNullOrEmpty(fallbackUrl))
            {
                try
                {
                    var fallbackResponse = await client.GetAsync(fallbackUrl);
                    if (fallbackResponse.IsSuccessStatusCode)
                    {
                        return await fallbackResponse.Content.ReadAsStringAsync();
                    }
                }
                catch { }
            }

            throw new Exception("Не удалось подключиться ни к основному серверу, ни к зеркалу. Проверьте интернет-соединение.");
        }
    }
}
