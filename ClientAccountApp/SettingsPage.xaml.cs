using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace ClientAccountApp
{
    public sealed partial class SettingsPage : Page
    {
        public SettingsPage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            // Откладываем чтобы страница успела полностью отрисоваться
            DispatcherQueue.TryEnqueue(() => SafeUpdateStorageInfo());
        }

        private void SafeUpdateStorageInfo()
        {
            // Режим базы данных
            try
            {
                if (SqlServerModeTextBlock != null)
                {
                    bool isSql = false;
                    try { isSql = DatabaseConnectionSettingsService.IsSqlServerMode(); } catch { }
                    SqlServerModeTextBlock.Text = isSql
                        ? "Режим: SQL Server (корпоративный)"
                        : "Режим: Локальная SQLite";
                }
            }
            catch { }

            // StorageCurrentPathTextBlock
            try
            {
                if (StorageCurrentPathTextBlock != null)
                {
                    string txt = "Не определён";
                    try { txt = AppPaths.StorageRoot ?? txt; } catch { }
                    StorageCurrentPathTextBlock.Text = txt;
                }
            }
            catch { }

            // StoragePathInfoTextBlock
            try
            {
                if (StoragePathInfoTextBlock != null)
                {
                    bool custom = false;
                    try { custom = AppPaths.IsCustomStorage; } catch { }
                    StoragePathInfoTextBlock.Text = custom
                        ? "Используется нестандартная папка."
                        : "Данные хранятся в папке «Документы» — легко найти в Проводнике.";
                }
            }
            catch { }

            // ResetStorageButton
            try
            {
                if (ResetStorageButton != null)
                {
                    bool custom = false;
                    try { custom = AppPaths.IsCustomStorage; } catch { }
                    ResetStorageButton.Visibility = custom
                        ? Visibility.Visible : Visibility.Collapsed;
                }
            }
            catch { }
        }

        // ─── Хранилище ────────────────────────────────────────────────────────

        private void OpenStorageFolderButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string folder = AppPaths.StorageRoot;
                Directory.CreateDirectory(folder);
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = "\"" + folder + "\"",
                    UseShellExecute = true
                });
            }
            catch { }
        }

        private void OpenStorageSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            try { Frame.Navigate(typeof(DataStorageSettingsPage)); } catch { }
        }

        private void OpenDatabaseConnectionButton_Click(object sender, RoutedEventArgs e)
        {
            try { Frame.Navigate(typeof(DatabaseConnectionSettingsPage)); } catch { }
        }

        private async void ResetStorageButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new ContentDialog
                {
                    Title = "Сбросить на стандартный путь?",
                    Content = "Данные будут использоваться из:\n" + AppPaths.DefaultStorageRoot +
                              "\n\nТекущие данные в нестандартной папке не удаляются.",
                    PrimaryButtonText = "Сбросить",
                    CloseButtonText = "Отмена",
                    XamlRoot = XamlRoot
                };

                if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

                StorageLocationService.ClearCustomStorageRoot();

                await new ContentDialog
                {
                    Title = "Путь сброшен",
                    Content = "Изменения вступят в силу после перезапуска приложения.",
                    CloseButtonText = "ОК",
                    XamlRoot = XamlRoot
                }.ShowAsync();
            }
            catch { }
        }

        // ─── Резервные копии ──────────────────────────────────────────────────

        private async void CreateBackupButton_Click(object sender, RoutedEventArgs e)
        {
            if (CreateBackupButton != null) CreateBackupButton.IsEnabled = false;
            if (BackupStatusTextBlock != null) BackupStatusTextBlock.Text = "Создаётся резервная копия...";

            string backupPath = string.Empty;
            try
            {
                backupPath = await Task.Run(() => BackupService.CreateBackup());
                if (BackupStatusTextBlock != null)
                    BackupStatusTextBlock.Text = "✓ Копия создана: " + Path.GetFileName(backupPath);
            }
            catch (Exception ex)
            {
                if (BackupStatusTextBlock != null)
                    BackupStatusTextBlock.Text = "Ошибка: " + ex.Message;
                if (CreateBackupButton != null) CreateBackupButton.IsEnabled = true;
                return;
            }

            if (CreateBackupButton != null) CreateBackupButton.IsEnabled = true;

            try
            {
                var d = new ContentDialog
                {
                    Title = "Резервная копия создана",
                    Content = "Сохранено в:\n" + backupPath,
                    PrimaryButtonText = "Открыть папку",
                    CloseButtonText = "ОК",
                    XamlRoot = XamlRoot
                };
                if (await d.ShowAsync() == ContentDialogResult.Primary)
                    OpenFolder(backupPath);
            }
            catch { }
        }

        private void OpenBackupsFolderButton_Click(object sender, RoutedEventArgs e)
        {
            try { OpenFolder(AppPaths.BackupsFolder); } catch { }
        }

        private void OpenBackupsButton_Click(object sender, RoutedEventArgs e)
        {
            try { Frame.Navigate(typeof(BackupsPage)); } catch { }
        }

        // ─── Вспомогательные ──────────────────────────────────────────────────

        private static void OpenFolder(string path)
        {
            try
            {
                Directory.CreateDirectory(path);
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = "\"" + path + "\"",
                    UseShellExecute = true
                });
            }
            catch { }
        }
    }
}
