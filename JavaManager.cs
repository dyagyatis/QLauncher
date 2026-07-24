using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MinecraftLauncher
{
    public static class JavaManager
    {
        public static int GetRequiredJavaVersion(string mcVersion)
        {
            if (string.IsNullOrWhiteSpace(mcVersion)) return 17;

            // Извлекаем цифры версии (например, 1.20.4 -> 20, 1.16.5 -> 16)
            var match = Regex.Match(mcVersion, @"1\.(\d+)(\.(\d+))?");
            if (match.Success)
            {
                int minor = int.Parse(match.Groups[1].Value);
                int patch = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : 0;

                if (minor <= 16) return 8;
                if (minor < 20 || (minor == 20 && patch <= 4)) return 17;
                return 21;
            }

            return 17; // По умолчанию
        }

        public static async Task<string> ResolveJavaExecutableAsync(string configuredJavaPath, string gameVersion, Action<string>? statusCallback = null)
        {
            int requiredVer = GetRequiredJavaVersion(gameVersion);

            // 1. Проверяем указанную вручную Java
            if (!string.IsNullOrWhiteSpace(configuredJavaPath) && File.Exists(configuredJavaPath))
            {
                return configuredJavaPath;
            }

            // 2. Проверяем локально скачанную JRE в рунтайме
            string runtimeFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "runtime", $"java-{requiredVer}");
            string localJavaw = Path.Combine(runtimeFolder, "bin", "javaw.exe");

            if (File.Exists(localJavaw))
            {
                return localJavaw;
            }

            // Ищем глубже, если распаковалось с подпапкой (например zulu17.46.19-ca-jre17.0.9-win_x64/bin/javaw.exe)
            if (Directory.Exists(runtimeFolder))
            {
                var files = Directory.GetFiles(runtimeFolder, "javaw.exe", SearchOption.AllDirectories);
                if (files.Length > 0) return files[0];
            }

            // 3. Проверяем стандартную системную JAVA_HOME
            string? javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
            if (!string.IsNullOrEmpty(javaHome))
            {
                string sysJavaw = Path.Combine(javaHome, "bin", "javaw.exe");
                if (File.Exists(sysJavaw)) return sysJavaw;
            }

            // 4. Скачиваем нужную версию Java (Azul Zulu CDN + BMCLAPI Mirror Fallback)
            statusCallback?.Invoke($"Загрузка Java {requiredVer}...");
            return await DownloadOpenJDKAsync(requiredVer, runtimeFolder, statusCallback);
        }

        private static async Task<string> DownloadOpenJDKAsync(int version, string destinationFolder, Action<string>? statusCallback)
        {
            Directory.CreateDirectory(destinationFolder);

            // Первичный надежный CDN Azul Zulu + Резервное зеркало BMCLAPI
            (string primaryUrl, string fallbackUrl) = version switch
            {
                8 => ("https://cdn.azul.com/zulu/bin/zulu8.74.0.17-ca-jre8.0.392-win_x64.zip", "https://bmclapi2.bangbang93.com/open-java/8/windows-x64.zip"),
                17 => ("https://cdn.azul.com/zulu/bin/zulu17.46.19-ca-jre17.0.9-win_x64.zip", "https://bmclapi2.bangbang93.com/open-java/17/windows-x64.zip"),
                _ => ("https://cdn.azul.com/zulu/bin/zulu21.30.15-ca-jre21.0.1-win_x64.zip", "https://bmclapi2.bangbang93.com/open-java/21/windows-x64.zip")
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
                using var client = new HttpClient(handler);
                client.Timeout = TimeSpan.FromMinutes(5);

                try
                {
                    fileBytes = await client.GetByteArrayAsync(primaryUrl);
                }
                catch
                {
                    statusCallback?.Invoke($"Переключение на зеркало Java {version}...");
                    fileBytes = await client.GetByteArrayAsync(fallbackUrl);
                }

                await File.WriteAllBytesAsync(zipPath, fileBytes);

                statusCallback?.Invoke($"Распаковка Java {version}...");
                ZipFile.ExtractToDirectory(zipPath, destinationFolder, overwriteFiles: true);

                if (File.Exists(zipPath)) File.Delete(zipPath);

                var javawFiles = Directory.GetFiles(destinationFolder, "javaw.exe", SearchOption.AllDirectories);
                if (javawFiles.Length > 0)
                {
                    statusCallback?.Invoke($"Java {version} готова!");
                    return javawFiles[0];
                }

                throw new Exception($"Не найден javaw.exe в распакованном архиве Java {version}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка авто-установки Java {version}: {ex.Message}");
            }
        }
    }
}
