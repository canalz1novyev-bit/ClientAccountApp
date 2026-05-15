using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace ClientAccountApp
{
    public sealed partial class AppearanceSettingsPage : Page
    {
        public AppearanceSettingsPage()
        {
            this.InitializeComponent();
            UpdateThemeCardSelection();
        }

        private void DarkThemeCard_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            ThemeService.ApplyTheme(ThemeService.ThemeDark);
            UpdateThemeCardSelection();
            ThemeStatusTextBlock.Text = "✓ Тёмная тема применена.";
        }

        private void LightThemeCard_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            ThemeService.ApplyTheme(ThemeService.ThemeLight);
            UpdateThemeCardSelection();
            ThemeStatusTextBlock.Text = "✓ Светлая тема применена.";
        }

        private void MilitaryThemeCard_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            ThemeService.ApplyTheme(ThemeService.ThemeMilitary);
            UpdateThemeCardSelection();
            ThemeStatusTextBlock.Text = "✓ Военная тема применена.";
        }

        private void UpdateThemeCardSelection()
        {
            string theme = ThemeService.CurrentTheme;

            // Акцентный цвет рамки — берём из ресурсов темы если доступен
            SolidColorBrush accentBrush;
            if (Application.Current.Resources.TryGetValue("NiatecAccentBrush", out object obj) &&
                obj is SolidColorBrush resBrush)
            {
                accentBrush = resBrush;
            }
            else
            {
                accentBrush = new SolidColorBrush(Color.FromArgb(255, 184, 134, 11));
            }

            // Цвет рамки по умолчанию — зависит от фона карточки
            var darkDefault = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255));   // полупрозрачный на тёмном
            var lightDefault = new SolidColorBrush(Color.FromArgb(100, 26, 32, 51));    // полупрозрачный на светлом
            var militaryDefault = new SolidColorBrush(Color.FromArgb(80, 160, 220, 100));

            DarkThemeCard.BorderBrush   = theme == ThemeService.ThemeDark     ? accentBrush : darkDefault;
            LightThemeCard.BorderBrush  = theme == ThemeService.ThemeLight    ? accentBrush : lightDefault;
            MilitaryThemeCard.BorderBrush = theme == ThemeService.ThemeMilitary ? accentBrush : militaryDefault;

            DarkThemeCard.BorderThickness     = theme == ThemeService.ThemeDark     ? new Thickness(2.5) : new Thickness(1.5);
            LightThemeCard.BorderThickness    = theme == ThemeService.ThemeLight    ? new Thickness(2.5) : new Thickness(1.5);
            MilitaryThemeCard.BorderThickness = theme == ThemeService.ThemeMilitary ? new Thickness(2.5) : new Thickness(1.5);
        }
    }
}
