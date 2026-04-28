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

        private void OpenStorageSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(DataStorageSettingsPage));
        }

        private void OpenBackupsButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(BackupsPage));
        }
    }
}