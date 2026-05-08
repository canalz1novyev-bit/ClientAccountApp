using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;

namespace ClientAccountApp
{
    public sealed partial class AppUpdatesPage : Page
    {
        private string _downloadUrl = "";

        public AppUpdatesPage()
        {
            InitializeComponent();
            Loaded += (_, _) =>
            {
                CurrentVersionTextBlock.Text = AppUpdateService.GetCurrentVersion();
                AvailableVersionTextBlock.Text = "Нажмите «Проверить обновления».";
            };
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
                UpdateProgressBar.Visibility = Visibility.Visible;
                UpdateProgressBar.IsIndeterminate = true;
                UpdateStatusTextBlock.Text = "Проверка обновлений...";

                var result = await AppUpdateService.CheckForUpdatesAsync();

                CurrentVersionTextBlock.Text = result.CurrentVersion;
                AvailableVersionTextBlock.Text = result.IsUpdateAvailable
                    ? result.AvailableVersion : "Нет доступных обновлений.";

                UpdateStatusTextBlock.Text = result.Message;
                _downloadUrl = result.DownloadUrl;

                if (result.IsUpdateAvailable)
                {
                    InstallUpdateButton.IsEnabled = true;
                    // Показываем заметки о релизе
                    if (!string.IsNullOrWhiteSpace(result.ReleaseNotes))
                    {
                        ReleaseNotesTextBlock.Text = result.ReleaseNotes;
                        ReleaseNotesBorder.Visibility = Visibility.Visible;
                    }
                }
                else
                {
                    ReleaseNotesBorder.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                UpdateStatusTextBlock.Text = "Ошибка: " + ex.Message;
            }
            finally
            {
                CheckUpdatesButton.IsEnabled = true;
                UpdateProgressBar.Visibility = Visibility.Collapsed;
                UpdateProgressBar.IsIndeterminate = false;
            }
        }

        private async void InstallUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                Title = "Скачать обновление?",
                Content = "Откроется страница загрузки на GitHub. Скачайте ZIP, распакуйте и замените файлы приложения.",
                PrimaryButtonText = "Открыть страницу загрузки",
                CloseButtonText = "Отмена",
                XamlRoot = XamlRoot
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                AppUpdateService.OpenDownloadPage(_downloadUrl);
                UpdateStatusTextBlock.Text = "Страница загрузки открыта в браузере.";
            }
        }

        private void BackToSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack) Frame.GoBack();
        }
    }
}
