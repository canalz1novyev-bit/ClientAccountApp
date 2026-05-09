using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

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
            ThemeStatusTextBlock.Text = "Тёмная тема применена.";
        }

        private void LightThemeCard_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            ThemeService.ApplyTheme(ThemeService.ThemeLight);
            UpdateThemeCardSelection();
            ThemeStatusTextBlock.Text = "Светлая тема применена.";
        }

        private void UpdateThemeCardSelection()
        {
            string theme = ThemeService.CurrentTheme;

            var accent = new SolidColorBrush(ColorHelper.FromArgb(255, 184, 134, 11));
            var def = new SolidColorBrush(ColorHelper.FromArgb(255, 42, 53, 72));

            DarkThemeCard.BorderBrush = theme == ThemeService.ThemeDark ? accent : def;
            DarkThemeCard.BorderThickness = theme == ThemeService.ThemeDark ? new Thickness(2) : new Thickness(1);

            LightThemeCard.BorderBrush = theme == ThemeService.ThemeLight ? accent : def;
            LightThemeCard.BorderThickness = theme == ThemeService.ThemeLight ? new Thickness(2) : new Thickness(1);
        }
    }
}