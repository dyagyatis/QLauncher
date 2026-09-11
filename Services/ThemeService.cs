using System;
using System.Windows;
using System.Windows.Media;

namespace MinecraftLauncher.Services
{
    public interface IThemeService
    {
        bool IsDarkTheme { get; set; }
        event Action? ThemeChanged;
        void ToggleTheme();
        void SetTheme(bool isDark);
        void ApplyAccentColor(string mainHex, string hoverHex);
        void ApplyAccentPreset(string presetName);
    }

    public class ThemeService : IThemeService
    {
        public static ThemeService Instance { get; } = new ThemeService();

        public bool IsDarkTheme { get; set; } = true;
        public event Action? ThemeChanged;

        private string? _currentAccentMain;
        private string? _currentAccentHover;

        public void SetTheme(bool isDark)
        {
            IsDarkTheme = isDark;
            string themeName = IsDarkTheme ? "DarkTheme.xaml" : "LightTheme.xaml";
            var uri = new Uri($"pack://application:,,,/Themes/{themeName}", UriKind.Absolute);
            var dict = new ResourceDictionary { Source = uri };

            if (Application.Current != null && Application.Current.Resources != null)
            {
                var merged = Application.Current.Resources.MergedDictionaries;
                ResourceDictionary? existingTheme = null;
                foreach (var md in merged)
                {
                    if (md.Source != null && (md.Source.OriginalString.EndsWith("DarkTheme.xaml", StringComparison.OrdinalIgnoreCase) ||
                                              md.Source.OriginalString.EndsWith("LightTheme.xaml", StringComparison.OrdinalIgnoreCase)))
                    {
                        existingTheme = md;
                        break;
                    }
                }

                if (existingTheme != null)
                {
                    merged.Remove(existingTheme);
                }
                merged.Add(dict);

                foreach (var key in dict.Keys)
                {
                    Application.Current.Resources[key] = dict[key];
                }

                if (!string.IsNullOrEmpty(_currentAccentMain) && !string.IsNullOrEmpty(_currentAccentHover))
                {
                    ApplyAccentColor(_currentAccentMain, _currentAccentHover);
                }
            }

            try
            {
                var settings = SettingsService.Instance.Settings;
                if (settings.IsDarkTheme != isDark)
                {
                    settings.IsDarkTheme = isDark;
                    SettingsService.Instance.Save();
                }
            }
            catch { }

            ThemeChanged?.Invoke();
        }

        public void ToggleTheme()
        {
            SetTheme(!IsDarkTheme);
        }

        public void ApplyAccentColor(string mainHex, string hoverHex)
        {
            try
            {
                _currentAccentMain = mainHex;
                _currentAccentHover = hoverHex;

                var mainColor = (Color)ColorConverter.ConvertFromString(mainHex);
                var hoverColor = (Color)ColorConverter.ConvertFromString(hoverHex);

                Application.Current.Resources["AccentBrush"] = new SolidColorBrush(mainColor);
                Application.Current.Resources["AccentHoverBrush"] = new SolidColorBrush(hoverColor);
                Application.Current.Resources["AccentClickBrush"] = new SolidColorBrush(mainColor);
            }
            catch { }
        }

        public void ApplyAccentPreset(string presetName)
        {
            switch (presetName.ToLowerInvariant())
            {
                case "emerald":
                    ApplyAccentColor("#10B981", "#34D399");
                    break;
                case "purple":
                case "amethyst":
                    ApplyAccentColor("#8B5CF6", "#A78BFA");
                    break;
                case "rose":
                case "pink":
                    ApplyAccentColor("#F43F5E", "#FB7185");
                    break;
                case "amber":
                case "gold":
                    ApplyAccentColor("#F59E0B", "#FBBF24");
                    break;
                case "sapphire":
                case "blue":
                default:
                    ApplyAccentColor("#3B85E6", "#6AC4F7");
                    break;
            }
        }
    }
}
