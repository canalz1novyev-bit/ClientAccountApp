using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using System.IO;
using Windows.UI;

namespace ClientAccountApp
{
    public static class ThemeService
    {
        public const string ThemeDark = "Dark";
        public const string ThemeLight = "Light";
        public const string ThemeMilitary = "Military";

        private static readonly string _settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ClientAccountApp", "theme.txt");

        public static string CurrentTheme => LoadTheme();
        public static bool IsLightTheme => CurrentTheme == ThemeLight;
        public static bool IsMilitaryTheme => CurrentTheme == ThemeMilitary;

        public static event Action<string>? ThemeChanged;

        public static void ApplyTheme(string theme)
        {
            SaveTheme(theme);
            ApplyColors(theme);
            ThemeChanged?.Invoke(theme);
        }

        public static void ApplySavedTheme()
        {
            string theme = CurrentTheme;
            ApplyColors(theme);
            ThemeChanged?.Invoke(theme);
        }

        private static void ApplyColors(string theme)
        {
            var res = Application.Current.Resources;

            switch (theme)
            {
                case ThemeLight:
                    MutateBrush(res, "NiatecBackgroundBrush", Color.FromArgb(255, 242, 244, 248));
                    MutateBrush(res, "NiatecSurfaceBrush", Color.FromArgb(255, 255, 255, 255));
                    MutateBrush(res, "NiatecSurfaceAltBrush", Color.FromArgb(255, 246, 248, 251));
                    MutateBrush(res, "NiatecBorderBrush", Color.FromArgb(255, 218, 224, 235));
                    MutateBrush(res, "NiatecTextPrimaryBrush", Color.FromArgb(255, 15, 23, 42));
                    MutateBrush(res, "NiatecTextSecondaryBrush", Color.FromArgb(255, 60, 75, 100));
                    MutateBrush(res, "NiatecTextMutedBrush", Color.FromArgb(255, 120, 135, 158));
                    MutateBrush(res, "NiatecAccentBrush", Color.FromArgb(255, 200, 146, 14));  // #C8920E — яркий золотой
                    MutateBrush(res, "NiatecAccentBlueBrush", Color.FromArgb(255, 30, 80, 200));
                    MutateBrush(res, "NiatecSuccessBrush", Color.FromArgb(255, 30, 110, 60));
                    MutateBrush(res, "NiatecWarningBrush", Color.FromArgb(255, 160, 70, 5));
                    MutateBrush(res, "NiatecDangerBrush", Color.FromArgb(255, 180, 25, 25));
                    MutateBrush(res, "NiatecInfoBrush", Color.FromArgb(255, 25, 65, 200));
                    break;

                case ThemeMilitary:
                    MutateBrush(res, "NiatecBackgroundBrush", Color.FromArgb(255, 22, 32, 14));
                    MutateBrush(res, "NiatecSurfaceBrush", Color.FromArgb(255, 30, 42, 20));
                    MutateBrush(res, "NiatecSurfaceAltBrush", Color.FromArgb(255, 26, 38, 16));
                    MutateBrush(res, "NiatecBorderBrush", Color.FromArgb(255, 50, 70, 32));
                    MutateBrush(res, "NiatecTextPrimaryBrush", Color.FromArgb(255, 208, 232, 160));
                    MutateBrush(res, "NiatecTextSecondaryBrush", Color.FromArgb(255, 138, 170, 100));
                    MutateBrush(res, "NiatecTextMutedBrush", Color.FromArgb(255, 88, 115, 60));
                    MutateBrush(res, "NiatecAccentBrush", Color.FromArgb(255, 184, 212, 120));
                    MutateBrush(res, "NiatecAccentBlueBrush", Color.FromArgb(255, 90, 154, 90));
                    MutateBrush(res, "NiatecSuccessBrush", Color.FromArgb(255, 74, 138, 74));
                    MutateBrush(res, "NiatecWarningBrush", Color.FromArgb(255, 184, 160, 64));
                    MutateBrush(res, "NiatecDangerBrush", Color.FromArgb(255, 200, 80, 80));
                    MutateBrush(res, "NiatecInfoBrush", Color.FromArgb(255, 90, 138, 184));
                    break;

                default: // Dark
                    MutateBrush(res, "NiatecBackgroundBrush", Color.FromArgb(255, 11, 15, 23));
                    MutateBrush(res, "NiatecSurfaceBrush", Color.FromArgb(255, 18, 24, 38));
                    MutateBrush(res, "NiatecSurfaceAltBrush", Color.FromArgb(255, 22, 31, 46));
                    MutateBrush(res, "NiatecBorderBrush", Color.FromArgb(255, 42, 53, 72));
                    MutateBrush(res, "NiatecTextPrimaryBrush", Color.FromArgb(255, 243, 245, 247));
                    MutateBrush(res, "NiatecTextSecondaryBrush", Color.FromArgb(255, 154, 167, 189));
                    MutateBrush(res, "NiatecTextMutedBrush", Color.FromArgb(255, 111, 124, 145));
                    MutateBrush(res, "NiatecAccentBrush", Color.FromArgb(255, 212, 168, 95));
                    MutateBrush(res, "NiatecAccentBlueBrush", Color.FromArgb(255, 76, 126, 219));
                    MutateBrush(res, "NiatecSuccessBrush", Color.FromArgb(255, 79, 163, 107));
                    MutateBrush(res, "NiatecWarningBrush", Color.FromArgb(255, 214, 161, 74));
                    MutateBrush(res, "NiatecDangerBrush", Color.FromArgb(255, 198, 91, 91));
                    MutateBrush(res, "NiatecInfoBrush", Color.FromArgb(255, 92, 141, 255));
                    break;
            }
        }

        private static void MutateBrush(ResourceDictionary res, string key, Color color)
        {
            if (res.ContainsKey(key) && res[key] is SolidColorBrush brush)
                brush.Color = color;
        }

        private static void SaveTheme(string theme)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
                File.WriteAllText(_settingsPath, theme);
            }
            catch { }
        }

        private static string LoadTheme()
        {
            try
            {
                if (File.Exists(_settingsPath))
                    return File.ReadAllText(_settingsPath).Trim();
            }
            catch { }
            return ThemeDark;
        }
    }
}