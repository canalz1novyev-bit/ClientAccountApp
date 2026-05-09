using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
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
            DispatcherQueue.TryEnqueue(() =>
            {
                SafeUpdateStorageInfo();
                UpdateThemeCardSelection();
            });
        }

        // ─── Тема ─────────────────────────────────────────────────────────────

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

        private void MilitaryThemeCard_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            ThemeService.ApplyTheme(ThemeService.ThemeMilitary);
            UpdateThemeCardSelection();
            ThemeStatusTextBlock.Text = "Тема М81 Woodland применена.";
        }

        private void UpdateThemeCardSelection()
        {
            string theme = ThemeService.CurrentTheme;

            var accent = new SolidColorBrush(ColorHelper.FromArgb(255, 184, 134, 11));
            var milAccent = new SolidColorBrush(ColorHelper.FromArgb(255, 100, 160, 50));
            var def = new SolidColorBrush(ColorHelper.FromArgb(255, 42, 53, 72));

            DarkThemeCard.BorderBrush = theme == ThemeService.ThemeDark ? accent : def;
            DarkThemeCard.BorderThickness = theme == ThemeService.ThemeDark ? new Thickness(2) : new Thickness(1);

            LightThemeCard.BorderBrush = theme == ThemeService.ThemeLight ? accent : def;
            LightThemeCard.BorderThickness = theme == ThemeService.ThemeLight ? new Thickness(2) : new Thickness(1);

            if (MilitaryThemeCard != null)
            {
                MilitaryThemeCard.BorderBrush = theme == ThemeService.ThemeMilitary ? milAccent : def;
                MilitaryThemeCard.BorderThickness = theme == ThemeService.ThemeMilitary ? new Thickness(2) : new Thickness(1);
            }
        }

        // ─── Хранилище ────────────────────────────────────────────────────────

        private void SafeUpdateStorageInfo()
        {
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