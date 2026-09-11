using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using MinecraftLauncher.Helpers;

namespace MinecraftLauncher.Services
{
    public interface IDiagnosticReportService
    {
        Task<string> GenerateReportZipAsync(string destinationFolder);
    }

    public class DiagnosticReportService : IDiagnosticReportService
    {
        public static DiagnosticReportService Instance { get; } = new DiagnosticReportService();

        public async Task<string> GenerateReportZipAsync(string destinationPath)
        {
            return await Task.Run(() =>
            {
                string zipPath;
                if (destinationPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    zipPath = destinationPath;
                    string? dir = Path.GetDirectoryName(zipPath);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                }
                else
                {
                    Directory.CreateDirectory(destinationPath);
                    string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    zipPath = Path.Combine(destinationPath, $"qlauncher-report-{timestamp}.zip");
                }

                if (File.Exists(zipPath))
                {
                    File.Delete(zipPath);
                }

                using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                {
                    // 1. Системная информация
                    var sysInfo = new StringBuilder();
                    sysInfo.AppendLine("=== QLauncher System & Environment Report ===");
                    sysInfo.AppendLine($"Timestamp: {DateTime.Now:O}");
                    sysInfo.AppendLine($"Launcher Version: {UpdateService.CurrentVersion}");
                    sysInfo.AppendLine($"Portable Mode: {LauncherPathHelper.IsPortableMode}");
                    sysInfo.AppendLine($"OS: {RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})");
                    sysInfo.AppendLine($"Framework: {RuntimeInformation.FrameworkDescription}");
                    sysInfo.AppendLine($"Process Architecture: {RuntimeInformation.ProcessArchitecture}");
                    sysInfo.AppendLine($"Data Directory: {LauncherPathHelper.GetDefaultDataDirectory()}");

                    var gpus = GpuService.Instance.GetAvailableGpus();
                    sysInfo.AppendLine("\n=== Detected Graphics Adapters ===");
                    foreach (var gpu in gpus)
                    {
                        sysInfo.AppendLine($"- {gpu.DisplayName} | Driver: {gpu.DriverVersion} | VRAM: {gpu.VramFormatted} | Discrete: {gpu.IsDiscrete}");
                    }

                    var settings = SettingsService.Instance.Settings;
                    sysInfo.AppendLine("\n=== Selected Settings Summary ===");
                    sysInfo.AppendLine($"Allocated RAM: {settings.RamMb} MB");
                    sysInfo.AppendLine($"Java Path: {(string.IsNullOrWhiteSpace(settings.JavaPath) ? "Auto/System" : settings.JavaPath)}");
                    sysInfo.AppendLine($"JVM Preset: {settings.JvmPreset}");
                    sysInfo.AppendLine($"Update Channel: {settings.UpdateChannel}");
                    sysInfo.AppendLine($"Update Mirror: {settings.UpdateMirror}");
                    sysInfo.AppendLine($"Modpacks Count: {settings.Modpacks.Count}");

                    var infoEntry = zip.CreateEntry("system_info.txt", CompressionLevel.Optimal);
                    using (var writer = new StreamWriter(infoEntry.Open(), Encoding.UTF8))
                    {
                        writer.Write(sysInfo.ToString());
                    }

                    // 2. Последние логи игры и лаунчера (с санитизацией токенов)
                    try
                    {
                        string logsDir = Path.Combine(settings.GamePath, "logs");
                        if (Directory.Exists(logsDir))
                        {
                            var logFiles = Directory.GetFiles(logsDir, "*.log")
                                .OrderByDescending(File.GetLastWriteTime)
                                .Take(3);

                            foreach (var logFile in logFiles)
                            {
                                string content = File.ReadAllText(logFile);
                                string sanitized = SanitizeLogContent(content);

                                var logEntry = zip.CreateEntry($"logs/{Path.GetFileName(logFile)}", CompressionLevel.Optimal);
                                using var writer = new StreamWriter(logEntry.Open(), Encoding.UTF8);
                                writer.Write(sanitized);
                            }
                        }
                    }
                    catch
                    {
                        // Ошибки чтения логов не прерывают создание репорта
                    }
                }

                return zipPath;
            });
        }

        private static string SanitizeLogContent(string log)
        {
            if (string.IsNullOrEmpty(log)) return "";
            // Вырезаем возможные токены сессии и приватные ключи
            string cleaned = log;
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, "(?i)(accessToken|token|session|auth_token|uuid)[\"=:\\s]+[a-zA-Z0-9_-]{20,}", "$1=REDACTED");
            return cleaned;
        }
    }
}
