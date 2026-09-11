using System;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using MinecraftLauncher.Models;

namespace MinecraftLauncher.Services
{
    public interface ISettingsService
    {
        LauncherSettings Settings { get; }
        LauncherSettings Load();
        void Save();
        void Save(LauncherSettings settings);
    }

    public class SettingsService : ISettingsService
    {
        private static string SettingsFile => MinecraftLauncher.Helpers.LauncherPathHelper.GetSettingsFilePath();

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        private LauncherSettings _currentSettings;

        public static SettingsService Instance { get; } = new SettingsService();

        public LauncherSettings Settings => _currentSettings;

        public SettingsService()
        {
            _currentSettings = Load();
        }

        public LauncherSettings Load()
        {
            if (!File.Exists(SettingsFile))
            {
                _currentSettings = new LauncherSettings();
                return _currentSettings;
            }

            try
            {
                string json = File.ReadAllText(SettingsFile);
                _currentSettings = JsonSerializer.Deserialize<LauncherSettings>(json) ?? new LauncherSettings();
            }
            catch
            {
                _currentSettings = new LauncherSettings();
            }

            return _currentSettings;
        }

        public void Save()
        {
            Save(_currentSettings);
        }

        public void Save(LauncherSettings settings)
        {
            try
            {
                _currentSettings = settings;
                string? dir = Path.GetDirectoryName(SettingsFile);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                string json = JsonSerializer.Serialize(settings, JsonOptions);
                File.WriteAllText(SettingsFile, json);
            }
            catch
            {
            }
        }
    }
}
