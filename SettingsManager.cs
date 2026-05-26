using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows; // Добавили для MessageBox

namespace MinecraftLauncher
{
    public class ModpackProfile
    {
        public string Name { get; set; }
        public string Loader { get; set; }

        // Добавляем ВСЕ переменные, которые требует компилятор:
        public string Version { get; set; }
        public string GameVersion { get; set; }
        public string FolderPath { get; set; }
    }

    public class AccountProfile
    {
        public string Nickname { get; set; }
        public string AccessToken { get; set; }
        public string Uuid { get; set; }

        // Добавляем только эту строчку:
        public override string ToString() => Nickname;
    }

    public class LauncherSettings
    {
        public string GamePath { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".qlauncher");
        public int RamMb { get; set; } = 4096;
        public string JavaPath { get; set; } = "";
        public bool CloseOnLaunch { get; set; } = false;
        public bool EnableDiscordRpc { get; set; } = true;
        public bool HideServerIp { get; set; } = true;
        public bool AutoAddServers { get; set; } = false;
        public string ActiveAccount { get; set; } = "";
        public string LastSelectedVersion { get; set; } = "";

        public List<AccountProfile> Accounts { get; set; } = new List<AccountProfile>();
        public List<ModpackProfile> Modpacks { get; set; } = new List<ModpackProfile>();
    }

    public static class SettingsManager
    {
        private static readonly string SettingsFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".qlauncher", "settings.json");

        public static LauncherSettings Load()
        {
            if (!File.Exists(SettingsFile)) return new LauncherSettings();

            try
            {
                string json = File.ReadAllText(SettingsFile);
                return JsonSerializer.Deserialize<LauncherSettings>(json) ?? new LauncherSettings();
            }
            catch (Exception ex)
            {
                // Если файл поврежден, сообщим об этом
                MessageBox.Show($"Ошибка при загрузке (чтении) настроек:\n{ex.Message}", "Ошибка чтения", MessageBoxButton.OK, MessageBoxImage.Error);
                return new LauncherSettings();
            }
        }

        public static void Save(LauncherSettings settings)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile)!);

                // Настраиваем красивый JSON с поддержкой русского языка
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };

                string json = JsonSerializer.Serialize(settings, options);
                File.WriteAllText(SettingsFile, json);
            }
            catch (Exception ex)
            {
                // Теперь мы точно увидим, если лаунчер не смог сохранить файл!
                MessageBox.Show($"Ошибка при сохранении настроек:\n{ex.Message}", "Ошибка записи", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}