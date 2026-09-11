using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MinecraftLauncher.Services
{
    public interface IJavaService
    {
        int GetRequiredJavaVersion(string mcVersion);
        Task<string> ResolveJavaExecutableAsync(string configuredJavaPath, string gameVersion, Action<string>? statusCallback = null);
    }

    public class JavaService : IJavaService
    {
        public static JavaService Instance { get; } = new JavaService();

        public int GetRequiredJavaVersion(string mcVersion)
        {
            if (string.IsNullOrWhiteSpace(mcVersion)) return 21;

            var yearMatch = Regex.Match(mcVersion, @"^(\d{2,})(\.(\d+))?");
            if (yearMatch.Success)
            {
                int year = int.Parse(yearMatch.Groups[1].Value);
                if (year >= 25) return 25;
                if (year >= 24) return 21;
            }

            var match = Regex.Match(mcVersion, @"1\.(\d+)(\.(\d+))?");
            if (match.Success)
            {
                int minor = int.Parse(match.Groups[1].Value);
                int patch = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : 0;

                if (minor <= 16) return 8;
                if (minor < 20 || (minor == 20 && patch <= 4)) return 17;
                return 21;
            }

            if (Regex.IsMatch(mcVersion, @"2[5-9]w\d+[a-z]")) return 25;
            if (Regex.IsMatch(mcVersion, @"2[3-4]w\d+[a-z]")) return 21;

            return 21;
        }

        public async Task<string> ResolveJavaExecutableAsync(string configuredJavaPath, string gameVersion, Action<string>? statusCallback = null)
        {
            int requiredVer = GetRequiredJavaVersion(gameVersion);

            if (!string.IsNullOrWhiteSpace(configuredJavaPath) && File.Exists(configuredJavaPath))
            {
                return configuredJavaPath;
            }

            string runtimeFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "runtime", $"java-{requiredVer}");
            string localJavaw = Path.Combine(runtimeFolder, "bin", "javaw.exe");

            if (File.Exists(localJavaw))
            {
                return localJavaw;
            }

            if (Directory.Exists(runtimeFolder))
            {
                var files = Directory.GetFiles(runtimeFolder, "javaw.exe", SearchOption.AllDirectories);
                if (files.Length > 0) return files[0];
            }

            string? javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
            if (!string.IsNullOrEmpty(javaHome))
            {
                string sysJavaw = Path.Combine(javaHome, "bin", "javaw.exe");
                if (File.Exists(sysJavaw) && DetectJavaMajorVersion(sysJavaw) == requiredVer)
                {
                    return sysJavaw;
                }
            }

            statusCallback?.Invoke($"Загрузка Java {requiredVer}...");
            return await DownloadOpenJDKAsync(requiredVer, runtimeFolder, statusCallback);
        }

        public static int DetectJavaMajorVersion(string javaPath)
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = javaPath,
                    Arguments = "-version",
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var p = System.Diagnostics.Process.Start(psi);
                if (p != null)
                {
                    string output = p.StandardError.ReadToEnd() + p.StandardOutput.ReadToEnd();
                    p.WaitForExit(2000);

                    var m = Regex.Match(output, @"version ""(?:1\.)?(\d+)");
                    if (m.Success && int.TryParse(m.Groups[1].Value, out int v))
                    {
                        return v;
                    }
                }
            }
            catch { }
            return 21;
        }

        private async Task<string> DownloadOpenJDKAsync(int version, string destinationFolder, Action<string>? statusCallback)
        {
            Directory.CreateDirectory(destinationFolder);

            (string primaryUrl, string fallbackUrl) = version switch
            {
                8 => (
                    "https://api.adoptium.net/v3/binary/latest/8/ga/windows/x64/jre/hotspot/normal/eclipse",
                    "https://cdn.azul.com/zulu/bin/zulu8.74.0.17-ca-jre8.0.392-win_x64.zip"
                ),
                17 => (
                    "https://api.adoptium.net/v3/binary/latest/17/ga/windows/x64/jre/hotspot/normal/eclipse",
                    "https://cdn.azul.com/zulu/bin/zulu17.46.19-ca-jre17.0.9-win_x64.zip"
                ),
                21 => (
                    "https://api.adoptium.net/v3/binary/latest/21/ga/windows/x64/jre/hotspot/normal/eclipse",
                    "https://cdn.azul.com/zulu/bin/zulu21.38.21-ca-jre21.0.5-win_x64.zip"
                ),
                _ => (
                    "https://api.adoptium.net/v3/binary/latest/25/ea/windows/x64/jre/hotspot/normal/eclipse",
                    "https://cdn.azul.com/zulu/bin/zulu21.38.21-ca-jre21.0.5-win_x64.zip"
                )
            };

            string zipPath = Path.Combine(Path.GetTempPath(), $"java_{version}.zip");

            try
            {
                statusCallback?.Invoke($"Скачивание OpenJDK Java {version}...");
                byte[]? fileBytes = null;

                using var handler = new HttpClientHandler
                {
                    AllowAutoRedirect = true,
                    ServerCertificateCustomValidationCallback = (m, c, ch, e) => true
                };
                using var client = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(5) };

                try
                {
                    fileBytes = await client.GetByteArrayAsync(primaryUrl);
                }
                catch
                {
                    if (!string.IsNullOrEmpty(fallbackUrl))
                    {
                        fileBytes = await client.GetByteArrayAsync(fallbackUrl);
                    }
                }

                if (fileBytes == null || fileBytes.Length == 0)
                {
                    throw new Exception("Не удалось скачать архив Java (пустой ответ).");
                }

                await File.WriteAllBytesAsync(zipPath, fileBytes);

                statusCallback?.Invoke($"Распаковка Java {version}...");
                ZipFile.ExtractToDirectory(zipPath, destinationFolder, true);

                var javaws = Directory.GetFiles(destinationFolder, "javaw.exe", SearchOption.AllDirectories);
                if (javaws.Length > 0)
                {
                    return javaws[0];
                }

                throw new FileNotFoundException("Файл javaw.exe не найден в распакованном архиве.");
            }
            finally
            {
                if (File.Exists(zipPath))
                {
                    try { File.Delete(zipPath); } catch { }
                }
            }
        }
    }
}
