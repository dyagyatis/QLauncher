using System;
using System.IO;

namespace MinecraftLauncher.Helpers
{
    public static class LauncherPathHelper
    {
        private static readonly string AppBaseDir = AppDomain.CurrentDomain.BaseDirectory;

        public static bool IsPortableMode =>
            File.Exists(Path.Combine(AppBaseDir, "portable")) ||
            File.Exists(Path.Combine(AppBaseDir, "portable.txt")) ||
            Directory.Exists(Path.Combine(AppBaseDir, "data"));

        public static string GetDefaultDataDirectory()
        {
            if (IsPortableMode)
            {
                string localData = Path.Combine(AppBaseDir, "data");
                Directory.CreateDirectory(localData);
                return localData;
            }

            string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".qlauncher");
            Directory.CreateDirectory(appData);
            return appData;
        }

        public static string GetSettingsFilePath()
        {
            return Path.Combine(GetDefaultDataDirectory(), "settings.json");
        }

        public static void CleanupOldBackupsAndTemp()
        {
            try
            {
                string currentExe = Environment.ProcessPath ?? "";
                if (!string.IsNullOrEmpty(currentExe))
                {
                    string bakFile = currentExe + ".bak";
                    if (File.Exists(bakFile))
                    {
                        File.Delete(bakFile);
                    }
                }

                string tempUpdateDir = Path.Combine(Path.GetTempPath(), "QLauncher_Update");
                if (Directory.Exists(tempUpdateDir))
                {
                    Directory.Delete(tempUpdateDir, true);
                }
            }
            catch
            {
                // Игнорируем ошибки фоновой очистки
            }
        }
    }
}
