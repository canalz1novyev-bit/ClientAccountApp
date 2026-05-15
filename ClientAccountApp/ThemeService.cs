using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
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

        /// <summary>
        /// Возвращает кисть темы по ключу ресурса. Использовать в code-behind вместо hardcoded цветов.
        /// </summary>
        public static SolidColorBrush GetBrush(string key, byte r = 128, byte g = 128, byte b = 128)
        {
            if (Application.Current.Resources.TryGetValue(key, out var obj) && obj is SolidColorBrush brush)
                return brush;
            return new SolidColorBrush(Color.FromArgb(255, r, g, b));
        }

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
                    MutateBrush(res, "NiatecAccentBrush", Color.FromArgb(255, 200, 146, 14));
                    MutateBrush(res, "NiatecAccentBlueBrush", Color.FromArgb(255, 30, 80, 200));
                    MutateBrush(res, "NiatecSuccessBrush", Color.FromArgb(255, 30, 110, 60));
                    MutateBrush(res, "NiatecWarningBrush", Color.FromArgb(255, 160, 70, 5));
                    MutateBrush(res, "NiatecDangerBrush", Color.FromArgb(255, 180, 25, 25));
                    MutateBrush(res, "NiatecInfoBrush", Color.FromArgb(255, 25, 65, 200));
                    // TextBox
                    SetBrush(res, "TextControlBackground",            Color.FromArgb(255, 255, 255, 255));
                    SetBrush(res, "TextControlBackgroundPointerOver", Color.FromArgb(255, 246, 248, 251));
                    SetBrush(res, "TextControlBackgroundFocused",     Color.FromArgb(255, 255, 255, 255));
                    SetBrush(res, "TextControlBackgroundDisabled",    Color.FromArgb(255, 242, 244, 248));
                    SetBrush(res, "TextControlForeground",            Color.FromArgb(255, 15, 23, 42));
                    SetBrush(res, "TextControlForegroundPointerOver", Color.FromArgb(255, 15, 23, 42));
                    SetBrush(res, "TextControlForegroundFocused",     Color.FromArgb(255, 15, 23, 42));
                    SetBrush(res, "TextControlForegroundDisabled",    Color.FromArgb(255, 120, 135, 158));
                    SetBrush(res, "TextControlBorderBrush",            Color.FromArgb(255, 218, 224, 235));
                    SetBrush(res, "TextControlBorderBrushPointerOver", Color.FromArgb(255, 200, 146, 14));
                    SetBrush(res, "TextControlBorderBrushFocused",     Color.FromArgb(255, 200, 146, 14));
                    SetBrush(res, "TextControlBorderBrushDisabled",    Color.FromArgb(255, 218, 224, 235));
                    SetBrush(res, "TextControlPlaceholderForeground",            Color.FromArgb(255, 120, 135, 158));
                    SetBrush(res, "TextControlPlaceholderForegroundPointerOver", Color.FromArgb(255, 120, 135, 158));
                    SetBrush(res, "TextControlPlaceholderForegroundFocused",     Color.FromArgb(255, 120, 135, 158));
                    SetBrush(res, "TextControlHeaderForeground",        Color.FromArgb(255, 60, 75, 100));
                    // ComboBox
                    SetBrush(res, "ComboBoxBackground",            Color.FromArgb(255, 255, 255, 255));
                    SetBrush(res, "ComboBoxBackgroundPointerOver", Color.FromArgb(255, 246, 248, 251));
                    SetBrush(res, "ComboBoxBackgroundPressed",     Color.FromArgb(255, 242, 244, 248));
                    SetBrush(res, "ComboBoxForeground",            Color.FromArgb(255, 15, 23, 42));
                    SetBrush(res, "ComboBoxForegroundPointerOver", Color.FromArgb(255, 15, 23, 42));
                    SetBrush(res, "ComboBoxForegroundPressed",     Color.FromArgb(255, 15, 23, 42));
                    SetBrush(res, "ComboBoxForegroundDisabled",    Color.FromArgb(255, 120, 135, 158));
                    SetBrush(res, "ComboBoxBorderBrush",            Color.FromArgb(255, 218, 224, 235));
                    SetBrush(res, "ComboBoxBorderBrushPointerOver", Color.FromArgb(255, 200, 146, 14));
                    SetBrush(res, "ComboBoxBorderBrushPressed",     Color.FromArgb(255, 200, 146, 14));
                    SetBrush(res, "ComboBoxDropDownBackground",    Color.FromArgb(255, 255, 255, 255));
                    SetBrush(res, "ComboBoxDropDownBorderBrush",   Color.FromArgb(255, 218, 224, 235));
                    SetBrush(res, "ComboBoxDropDownForeground",    Color.FromArgb(255, 15, 23, 42));
                    SetBrush(res, "ComboBoxPlaceholderForeground",            Color.FromArgb(255, 120, 135, 158));
                    SetBrush(res, "ComboBoxPlaceholderForegroundPointerOver", Color.FromArgb(255, 120, 135, 158));
                    SetBrush(res, "ComboBoxPlaceholderForegroundPressed",     Color.FromArgb(255, 120, 135, 158));
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
                    // TextBox
                    SetBrush(res, "TextControlBackground",            Color.FromArgb(255, 26, 38, 16));
                    SetBrush(res, "TextControlBackgroundPointerOver", Color.FromArgb(255, 30, 42, 20));
                    SetBrush(res, "TextControlBackgroundFocused",     Color.FromArgb(255, 30, 42, 20));
                    SetBrush(res, "TextControlBackgroundDisabled",    Color.FromArgb(255, 22, 32, 14));
                    SetBrush(res, "TextControlForeground",            Color.FromArgb(255, 208, 232, 160));
                    SetBrush(res, "TextControlForegroundPointerOver", Color.FromArgb(255, 208, 232, 160));
                    SetBrush(res, "TextControlForegroundFocused",     Color.FromArgb(255, 220, 245, 175));
                    SetBrush(res, "TextControlForegroundDisabled",    Color.FromArgb(255, 88, 115, 60));
                    SetBrush(res, "TextControlBorderBrush",            Color.FromArgb(255, 50, 70, 32));
                    SetBrush(res, "TextControlBorderBrushPointerOver", Color.FromArgb(255, 184, 212, 120));
                    SetBrush(res, "TextControlBorderBrushFocused",     Color.FromArgb(255, 184, 212, 120));
                    SetBrush(res, "TextControlBorderBrushDisabled",    Color.FromArgb(255, 50, 70, 32));
                    SetBrush(res, "TextControlPlaceholderForeground",            Color.FromArgb(255, 88, 115, 60));
                    SetBrush(res, "TextControlPlaceholderForegroundPointerOver", Color.FromArgb(255, 88, 115, 60));
                    SetBrush(res, "TextControlPlaceholderForegroundFocused",     Color.FromArgb(255, 88, 115, 60));
                    SetBrush(res, "TextControlHeaderForeground",        Color.FromArgb(255, 138, 170, 100));
                    // ComboBox
                    SetBrush(res, "ComboBoxBackground",            Color.FromArgb(255, 26, 38, 16));
                    SetBrush(res, "ComboBoxBackgroundPointerOver", Color.FromArgb(255, 30, 42, 20));
                    SetBrush(res, "ComboBoxBackgroundPressed",     Color.FromArgb(255, 22, 32, 14));
                    SetBrush(res, "ComboBoxForeground",            Color.FromArgb(255, 208, 232, 160));
                    SetBrush(res, "ComboBoxForegroundPointerOver", Color.FromArgb(255, 208, 232, 160));
                    SetBrush(res, "ComboBoxForegroundPressed",     Color.FromArgb(255, 208, 232, 160));
                    SetBrush(res, "ComboBoxForegroundDisabled",    Color.FromArgb(255, 88, 115, 60));
                    SetBrush(res, "ComboBoxBorderBrush",            Color.FromArgb(255, 50, 70, 32));
                    SetBrush(res, "ComboBoxBorderBrushPointerOver", Color.FromArgb(255, 184, 212, 120));
                    SetBrush(res, "ComboBoxBorderBrushPressed",     Color.FromArgb(255, 184, 212, 120));
                    SetBrush(res, "ComboBoxDropDownBackground",    Color.FromArgb(255, 30, 42, 20));
                    SetBrush(res, "ComboBoxDropDownBorderBrush",   Color.FromArgb(255, 50, 70, 32));
                    SetBrush(res, "ComboBoxDropDownForeground",    Color.FromArgb(255, 208, 232, 160));
                    SetBrush(res, "ComboBoxPlaceholderForeground",            Color.FromArgb(255, 88, 115, 60));
                    SetBrush(res, "ComboBoxPlaceholderForegroundPointerOver", Color.FromArgb(255, 88, 115, 60));
                    SetBrush(res, "ComboBoxPlaceholderForegroundPressed",     Color.FromArgb(255, 88, 115, 60));
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
                    // TextBox
                    SetBrush(res, "TextControlBackground",            Color.FromArgb(255, 22, 31, 46));
                    SetBrush(res, "TextControlBackgroundPointerOver", Color.FromArgb(255, 18, 24, 38));
                    SetBrush(res, "TextControlBackgroundFocused",     Color.FromArgb(255, 18, 24, 38));
                    SetBrush(res, "TextControlBackgroundDisabled",    Color.FromArgb(255, 11, 15, 23));
                    SetBrush(res, "TextControlForeground",            Color.FromArgb(255, 243, 245, 247));
                    SetBrush(res, "TextControlForegroundPointerOver", Color.FromArgb(255, 243, 245, 247));
                    SetBrush(res, "TextControlForegroundFocused",     Color.FromArgb(255, 255, 255, 255));
                    SetBrush(res, "TextControlForegroundDisabled",    Color.FromArgb(255, 111, 124, 145));
                    SetBrush(res, "TextControlBorderBrush",            Color.FromArgb(255, 42, 53, 72));
                    SetBrush(res, "TextControlBorderBrushPointerOver", Color.FromArgb(255, 212, 168, 95));
                    SetBrush(res, "TextControlBorderBrushFocused",     Color.FromArgb(255, 212, 168, 95));
                    SetBrush(res, "TextControlBorderBrushDisabled",    Color.FromArgb(255, 42, 53, 72));
                    SetBrush(res, "TextControlPlaceholderForeground",            Color.FromArgb(255, 111, 124, 145));
                    SetBrush(res, "TextControlPlaceholderForegroundPointerOver", Color.FromArgb(255, 111, 124, 145));
                    SetBrush(res, "TextControlPlaceholderForegroundFocused",     Color.FromArgb(255, 111, 124, 145));
                    SetBrush(res, "TextControlHeaderForeground",        Color.FromArgb(255, 154, 167, 189));
                    // ComboBox
                    SetBrush(res, "ComboBoxBackground",            Color.FromArgb(255, 22, 31, 46));
                    SetBrush(res, "ComboBoxBackgroundPointerOver", Color.FromArgb(255, 18, 24, 38));
                    SetBrush(res, "ComboBoxBackgroundPressed",     Color.FromArgb(255, 11, 15, 23));
                    SetBrush(res, "ComboBoxForeground",            Color.FromArgb(255, 243, 245, 247));
                    SetBrush(res, "ComboBoxForegroundPointerOver", Color.FromArgb(255, 243, 245, 247));
                    SetBrush(res, "ComboBoxForegroundPressed",     Color.FromArgb(255, 243, 245, 247));
                    SetBrush(res, "ComboBoxForegroundDisabled",    Color.FromArgb(255, 111, 124, 145));
                    SetBrush(res, "ComboBoxBorderBrush",            Color.FromArgb(255, 42, 53, 72));
                    SetBrush(res, "ComboBoxBorderBrushPointerOver", Color.FromArgb(255, 212, 168, 95));
                    SetBrush(res, "ComboBoxBorderBrushPressed",     Color.FromArgb(255, 212, 168, 95));
                    SetBrush(res, "ComboBoxDropDownBackground",    Color.FromArgb(255, 18, 24, 38));
                    SetBrush(res, "ComboBoxDropDownBorderBrush",   Color.FromArgb(255, 42, 53, 72));
                    SetBrush(res, "ComboBoxDropDownForeground",    Color.FromArgb(255, 243, 245, 247));
                    SetBrush(res, "ComboBoxPlaceholderForeground",            Color.FromArgb(255, 111, 124, 145));
                    SetBrush(res, "ComboBoxPlaceholderForegroundPointerOver", Color.FromArgb(255, 111, 124, 145));
                    SetBrush(res, "ComboBoxPlaceholderForegroundPressed",     Color.FromArgb(255, 111, 124, 145));
                    break;
            }
        }

        // Ключи WinUI-кистей, которые мы уже разместили в top-level словаре.
        // После первого размещения — мутируем, не заменяем.
        private static readonly HashSet<string> s_ownedSystemBrushes = new();

        /// <summary>
        /// Мутирует нашу SolidColorBrush (Niatec-кисти) — все привязки обновляются автоматически.
        /// </summary>
        private static void MutateBrush(ResourceDictionary res, string key, Color color)
        {
            if (res.ContainsKey(key) && res[key] is SolidColorBrush brush)
                brush.Color = color;
        }

        /// <summary>
        /// Устанавливает системную WinUI-кисть (TextControl*, ComboBox*).
        /// Первый вызов: создаёт новый SolidColorBrush и регистрирует в top-level словаре.
        /// Последующие вызовы: мутирует тот же объект — контролы видят изменение.
        /// </summary>
        private static void SetBrush(ResourceDictionary res, string key, Color color)
        {
            if (s_ownedSystemBrushes.Contains(key))
            {
                // Наша кисть уже в словаре — мутируем напрямую
                if (res[key] is SolidColorBrush existing)
                {
                    existing.Color = color;
                    return;
                }
            }

            // Первый раз: создаём и кладём в top-level словарь (приоритет выше MergedDictionaries)
            res[key] = new SolidColorBrush(color);
            s_ownedSystemBrushes.Add(key);
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