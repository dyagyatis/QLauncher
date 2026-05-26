using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace MinecraftLauncher
{
    public static class ModLoaderInstaller
    {
        // === УСТАНОВКА FABRIC (Наш бронированный метод) ===
        public static async Task<string> InstallFabricAsync(string gameVersion, string gamePath, HttpClient client)
        {
            string loadersJson = await client.GetStringAsync($"https://meta.fabricmc.net/v2/versions/loader/{gameVersion}");
            using JsonDocument doc = JsonDocument.Parse(loadersJson);
            var root = doc.RootElement;

            if (root.GetArrayLength() == 0) throw new Exception($"Нет доступного Fabric для версии {gameVersion}");

            string loaderVersion = root[0].GetProperty("loader").GetProperty("version").GetString() ?? "";
            string fabricVersionName = $"fabric-loader-{loaderVersion}-{gameVersion}";

            string versionFolder = Path.Combine(gamePath, "versions", fabricVersionName);
            Directory.CreateDirectory(versionFolder);

            string profileUrl = $"https://meta.fabricmc.net/v2/versions/loader/{gameVersion}/{loaderVersion}/profile/json";
            string profileJson = await client.GetStringAsync(profileUrl);

            File.WriteAllText(Path.Combine(versionFolder, $"{fabricVersionName}.json"), profileJson);
            return fabricVersionName;
        }

        // === УСТАНОВКА QUILT (Наш бронированный метод) ===
        public static async Task<string> InstallQuiltAsync(string gameVersion, string gamePath, HttpClient client)
        {
            string loadersJson = await client.GetStringAsync($"https://meta.quiltmc.org/v3/versions/loader/{gameVersion}");
            using JsonDocument doc = JsonDocument.Parse(loadersJson);
            var root = doc.RootElement;

            if (root.GetArrayLength() == 0) throw new Exception($"Нет доступного Quilt для версии {gameVersion}");

            string loaderVersion = root[0].GetProperty("loader").GetProperty("version").GetString() ?? "";
            string quiltVersionName = $"quilt-loader-{loaderVersion}-{gameVersion}";

            string versionFolder = Path.Combine(gamePath, "versions", quiltVersionName);
            Directory.CreateDirectory(versionFolder);

            string profileUrl = $"https://meta.quiltmc.org/v3/versions/loader/{gameVersion}/{loaderVersion}/profile/json";
            string profileJson = await client.GetStringAsync(profileUrl);

            File.WriteAllText(Path.Combine(versionFolder, $"{quiltVersionName}.json"), profileJson);
            return quiltVersionName;
        }

        // === УСТАНОВКА FORGE (Метод CmlLib) ===
        public static async Task<string> InstallForgeAsync(string gameVersion, CmlLib.Core.MinecraftLauncher launcher)
        {
            var forge = new CmlLib.Core.Installer.Forge.ForgeInstaller(launcher);
            return await forge.Install(gameVersion);
        }

        // === УСТАНОВКА NEOFORGE (Метод CmlLib) ===
        public static async Task<string> InstallNeoForgeAsync(string gameVersion, CmlLib.Core.MinecraftLauncher launcher)
        {
            var neoForge = new CmlLib.Core.Installer.NeoForge.NeoForgeInstaller(launcher);
            return await neoForge.Install(gameVersion);
        }
    }
}