using System;
using System.Windows;
using System.Windows.Media;

namespace MinecraftLauncher
{
    public static class ThemeAccentManager
    {
        public static void ApplyAccentColor(string mainHex, string hoverHex)
        {
            try
            {
                var mainColor = (Color)ColorConverter.ConvertFromString(mainHex);
                var hoverColor = (Color)ColorConverter.ConvertFromString(hoverHex);

                Application.Current.Resources["AccentBrush"] = new SolidColorBrush(mainColor);
                Application.Current.Resources["AccentHoverBrush"] = new SolidColorBrush(hoverColor);
                Application.Current.Resources["AccentClickBrush"] = new SolidColorBrush(mainColor);
            }
            catch { }
        }

        public static void ApplyPreset(string presetName)
        {
            switch (presetName.ToLower())
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
