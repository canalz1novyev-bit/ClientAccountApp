using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ClientAccountApp
{
    public sealed partial class SettingsPage : Page
    {
        public SettingsPage()
        {
            this.InitializeComponent();
        }
        private void OpenDatabaseDiagnosticsButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(DatabaseDiagnosticsPage));
        }

        private void OpenStorageSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(DataStorageSettingsPage));
        }
        private void OpenAiSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(AiSettingsPage));
        }
        private void OpenDatabaseConnectionSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(DatabaseConnectionSettingsPage));
        }
        private void OpenBackupsButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(BackupsPage));
        }
        private void OpenCounterpartySourcesSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(CounterpartySourcesSettingsPage));
        }
        private void OpenAppUpdatesButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(AppUpdatesPage));
        }
    }
}