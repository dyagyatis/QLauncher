using System;
using System.Linq;
using System.Windows;
using MinecraftLauncher.Helpers;
using MinecraftLauncher.Services;
using MinecraftLauncher.Views.Windows;

namespace MinecraftLauncher
{
    public partial class App : Application
    {
        public static string? JustUpdatedVersion { get; set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            LauncherPathHelper.CleanupOldBackupsAndTemp();

            bool noSplashArg = e.Args.Any(a => string.Equals(a, "--no-splash", StringComparison.OrdinalIgnoreCase));

            for (int i = 0; i < e.Args.Length; i++)
            {
                if (string.Equals(e.Args[i], "--updated", StringComparison.OrdinalIgnoreCase) && i + 1 < e.Args.Length)
                {
                    JustUpdatedVersion = e.Args[i + 1];
                    break;
                }
            }

            try
            {
                var settings = SettingsService.Instance.Load();
                if (!settings.IsDarkTheme)
                {
                    ThemeService.Instance.SetTheme(false);
                }

                if (noSplashArg || !settings.ShowSplashOnStartup)
                {
                    var mainWindow = new MainWindow();
                    mainWindow.Show();
                    return;
                }
            }
            catch { }

            var splash = new SplashScreenWindow();
            splash.Show();
        }
    }
}

