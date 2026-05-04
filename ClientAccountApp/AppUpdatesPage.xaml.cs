using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;

namespace ClientAccountApp
{
    public sealed partial class AppUpdatesPage : Page
    {
        private bool _hasUpdate;

        public AppUpdatesPage()
        {
            this.InitializeComponent();

            CurrentVersionTextBlock.Text = AppUpdateService.GetCurrentVersion();
            AvailableVersionTextBlock.Text = "Проверка ещё не выполнялась.";
            UpdateStatusTextBlock.Text = "Раздел обновлений готов к работе.";
            InstallUpdateButton.IsEnabled = false;
        }

        private async void CheckUpdatesButton_Click(object sender, RoutedEventArgs e)
        {
            await CheckUpdatesAsync();
        }

        private async Task CheckUpdatesAsync()
        {
            try
            {
                CheckUpdatesButton.IsEnabled = false;
                InstallUpdateButton.IsEnabled = false;

                UpdateStatusTextBlock.Text = "Проверяю наличие обновлений...";

                AppUpdateCheckResult result = await AppUpdateService.CheckForUpdatesAsync();

                CurrentVersionTextBlock.Text = result.CurrentVersion;

                _hasUpdate = result.IsUpdateAvailable;

                if (result.IsUpdateAvailable)
                {
                    AvailableVersionTextBlock.Text = result.AvailableVersion;
                    InstallUpdateButton.IsEnabled = true;
                }
                else
                {
                    AvailableVersionTextBlock.Text = "Новая версия не найдена.";
                    InstallUpdateButton.IsEnabled = false;
                }

                UpdateStatusTextBlock.Text = result.Message;
            }
            catch (Exception ex)
            {
                _hasUpdate = false;
                InstallUpdateButton.IsEnabled = false;
                UpdateStatusTextBlock.Text = "Ошибка проверки обновлений: " + ex.Message;
            }
            finally
            {
                CheckUpdatesButton.IsEnabled = true;
            }
        }

        private async void InstallUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!_hasUpdate)
                {
                    UpdateStatusTextBlock.Text = "Сначала проверьте наличие обновлений.";
                    return;
                }

                CheckUpdatesButton.IsEnabled = false;
                InstallUpdateButton.IsEnabled = false;

                UpdateStatusTextBlock.Text = "Скачиваю и устанавливаю обновление...";

                string message = await AppUpdateService.DownloadAndApplyUpdateAsync();

                UpdateStatusTextBlock.Text = message;
            }
            catch (Exception ex)
            {
                UpdateStatusTextBlock.Text = "Ошибка установки обновления: " + ex.Message;
                CheckUpdatesButton.IsEnabled = true;
            }
        }

        private void BackToSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsPage));
        }
    }
}