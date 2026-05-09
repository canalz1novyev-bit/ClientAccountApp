using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace ClientAccountApp
{
    public sealed partial class BackupsPage : Page
    {
        public BackupsPage()
        {
            this.InitializeComponent();

            RefreshBackupPageState();
        }

        private void RefreshBackupPageState()
        {
            var databaseSettings = DatabaseConnectionSettingsService.Load();

            if (databaseSettings.ProviderMode == DatabaseProviderMode.SqlServer)
            {
                BackupFolderPathTextBlock.Text = SqlServerBackupService.GetBackupRootFolder();
                BackupStatusTextBlock.Text =
                    "Серверный режим SQL Server. Резервная копия создаёт .bak базы и копию общей папки файлов.";
            }
            else
            {
                BackupFolderPathTextBlock.Text = BackupService.GetBackupFolder();
                BackupStatusTextBlock.Text = "Локальный режим SQLite. Раздел резервных копий готов к работе.";
            }
        }

        private async void CreateBackupButton_Click(object sender, RoutedEventArgs e)
        {
            var databaseSettings = DatabaseConnectionSettingsService.Load();

            if (databaseSettings.ProviderMode == DatabaseProviderMode.SqlServer)
            {
                Button? clickedButton = sender as Button;

                try
                {
                    if (clickedButton != null)
                        clickedButton.IsEnabled = false;

                    BackupStatusTextBlock.Text = "Создаётся резервная копия SQL Server...";

                    string backupFolder = await SqlServerBackupService.CreateSqlServerBackupAsync();

                    BackupFolderPathTextBlock.Text = backupFolder;
                    BackupStatusTextBlock.Text = $"Резервная копия SQL Server создана: {backupFolder}";

                    await ShowBackupDialogAsync(
                        "Резервная копия создана",
                        "Резервная копия SQL Server успешно создана.\n\n" +
                        $"Папка:\n{backupFolder}");

                    OpenFolderIfExists(backupFolder);
                }
                catch (Exception ex)
                {
                    BackupStatusTextBlock.Text = $"Ошибка резервного копирования SQL Server: {ex.Message}";

                    await ShowBackupDialogAsync(
                        "Ошибка резервного копирования",
                        "Не удалось создать резервную копию SQL Server.\n\n" +
                        ex.Message);
                }
                finally
                {
                    if (clickedButton != null)
                        clickedButton.IsEnabled = true;
                }

                return;
            }

            try
            {
                string backupPath = BackupService.CreateBackup();
                BackupFolderPathTextBlock.Text = BackupService.GetBackupFolder();
                BackupStatusTextBlock.Text = $"Резервная копия создана: {backupPath}";
            }
            catch (Exception ex)
            {
                BackupStatusTextBlock.Text = $"Ошибка резервного копирования: {ex.Message}";
            }
        }

        private async void RestoreLatestBackupButton_Click(object sender, RoutedEventArgs e)
        {
            var databaseSettings = DatabaseConnectionSettingsService.Load();

            if (databaseSettings.ProviderMode == DatabaseProviderMode.SqlServer)
            {
                BackupStatusTextBlock.Text =
                    "Автоматическое восстановление SQL Server пока отключено для безопасности.";

                await ShowBackupDialogAsync(
                    "Восстановление SQL Server",
                    "Сейчас приложение работает в SQL Server-режиме.\n\n" +
                    "Старую SQLite-команду восстановления здесь запускать нельзя, потому что она не восстановит серверную базу.\n\n" +
                    "Для SQL Server нужно отдельное безопасное восстановление из .bak-файла: с отключением активных подключений, восстановлением базы и возвратом общей папки файлов.\n\n" +
                    "Этот мастер восстановления лучше добавить отдельным следующим этапом.");

                return;
            }

            try
            {
                string restoredFrom = BackupService.RestoreLatestBackup();
                BackupFolderPathTextBlock.Text = BackupService.GetBackupFolder();
                BackupStatusTextBlock.Text = $"База и файлы восстановлены из копии: {restoredFrom}";
            }
            catch (Exception ex)
            {
                BackupStatusTextBlock.Text = $"Ошибка восстановления: {ex.Message}";
            }
        }

        private void OpenBackupsFolderButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var databaseSettings = DatabaseConnectionSettingsService.Load();

                string backupsFolder = databaseSettings.ProviderMode == DatabaseProviderMode.SqlServer
                    ? SqlServerBackupService.GetBackupRootFolder()
                    : BackupService.GetBackupFolder();

                Directory.CreateDirectory(backupsFolder);

                Process.Start(new ProcessStartInfo
                {
                    FileName = backupsFolder,
                    UseShellExecute = true
                });

                BackupFolderPathTextBlock.Text = backupsFolder;
                BackupStatusTextBlock.Text = $"Открыта папка резервных копий: {backupsFolder}";
            }
            catch (Exception ex)
            {
                BackupStatusTextBlock.Text = $"Ошибка открытия папки резервных копий: {ex.Message}";
            }
        }

        private void OpenLegacyWorkspaceButton_Click(object sender, RoutedEventArgs e)
        {
            Frame?.Navigate(typeof(ClientsPage));
        }

        private async Task ShowBackupDialogAsync(string title, string message)
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = message,
                CloseButtonText = "ОК",
                XamlRoot = this.XamlRoot
            };

            await dialog.ShowAsync();
        }

        private static void OpenFolderIfExists(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
                return;

            if (!Directory.Exists(folderPath))
                return;

            Process.Start(new ProcessStartInfo
            {
                FileName = folderPath,
                UseShellExecute = true
            });
        }
    }
}