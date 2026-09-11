using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using MinecraftLauncher.Services.LaunchEngine.Models;

namespace MinecraftLauncher.Services.LaunchEngine
{
    public interface IMinecraftLaunchEngine
    {
        Task<Process> LaunchAsync(LaunchOptions options, IProgress<LaunchProgress>? progress = null);
        Task<MojangManifest?> GetManifestAsync();
        Task<string> InstallFabricAsync(string gameVersion, string gameRootPath);
        Task<string> InstallQuiltAsync(string gameVersion, string gameRootPath);
    }

    public class MinecraftLaunchEngine : IMinecraftLaunchEngine
    {
        public static MinecraftLaunchEngine Instance { get; } = new();

        private readonly MojangManifestService _manifestService = new();
        private readonly LibraryService _libraryService = new();
        private readonly AssetService _assetService = new();
        private readonly ModLoaderService _modLoaderService = new();
        private readonly MinecraftArgumentBuilder _argumentBuilder = new();

        public Task<MojangManifest?> GetManifestAsync() => _manifestService.GetManifestAsync();
        public Task<string> InstallFabricAsync(string gameVersion, string gameRootPath) => _modLoaderService.InstallFabricAsync(gameVersion, gameRootPath);
        public Task<string> InstallQuiltAsync(string gameVersion, string gameRootPath) => _modLoaderService.InstallQuiltAsync(gameVersion, gameRootPath);

        public async Task<Process> LaunchAsync(LaunchOptions options, IProgress<LaunchProgress>? progress = null)
        {
            progress?.Report(new LaunchProgress
            {
                Phase = LaunchPhase.Initializing,
                StatusText = "Подготовка к запуску игры...",
                Percentage = 5
            });

            var versionInfo = await _manifestService.ResolveVersionInfoAsync(options.GameRootPath, options.VersionId);
            if (versionInfo == null)
            {
                throw new FileNotFoundException($"Не удалось найти конфигурацию для версии '{options.VersionId}'");
            }

            await _manifestService.EnsureClientJarAsync(options.GameRootPath, versionInfo, progress);

            string nativesDir = Path.Combine(options.GameRootPath, "natives", versionInfo.Id);
            var classpathJars = await _libraryService.EnsureLibrariesAsync(options.GameRootPath, versionInfo, nativesDir, progress);

            await _assetService.EnsureAssetsAsync(options.GameRootPath, versionInfo, progress);

            progress?.Report(new LaunchProgress
            {
                Phase = LaunchPhase.BuildingArguments,
                StatusText = "Формирование параметров запуска JVM...",
                Percentage = 95
            });

            var arguments = _argumentBuilder.BuildArguments(versionInfo, options, classpathJars, nativesDir);

            progress?.Report(new LaunchProgress
            {
                Phase = LaunchPhase.StartingProcess,
                StatusText = "Запуск процесса Minecraft...",
                Percentage = 100
            });

            string workingDir = !string.IsNullOrEmpty(options.InstancePath) && Directory.Exists(options.InstancePath)
                ? options.InstancePath
                : options.GameRootPath;

            var startInfo = new ProcessStartInfo
            {
                FileName = options.JavaPath,
                WorkingDirectory = workingDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            GpuService.Instance.ApplyGpuPreference(options.JavaPath, options.GpuPreference);
            GpuService.Instance.ConfigureProcessEnvironment(startInfo, options.GpuPreference);

            foreach (var arg in arguments)
            {
                startInfo.ArgumentList.Add(arg);
            }

            var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            if (!process.Start())
            {
                throw new InvalidOperationException("Не удалось запустить процесс Java.");
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            return process;
        }
    }
}
