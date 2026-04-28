using System;
using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ClientAccountApp
{
    public sealed partial class BackupsPage : Page
    {
        public BackupsPage()
        {
            this.InitializeComponent();

            BackupFolderPathTextBlock.Text = BackupService.GetBackupFolder();
            BackupStatusTextBlock.Text = "Раздел резервных копий готов к работе.";
        }

        private void CreateBackupButton_Click(object sender, RoutedEventArgs e)
        {
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

        private void RestoreLatestBackupButton_Click(object sender, RoutedEventArgs e)
        {
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
                string backupsFolder = BackupService.GetBackupFolder();

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
            Frame?.Navigate(typeof(LegacyWorkspacePage));
        }
    }
}