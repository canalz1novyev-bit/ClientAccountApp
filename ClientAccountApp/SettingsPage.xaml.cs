using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
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
                UpdateDaDataKeyStatus();
            });
        }

        // ─── DaData API key ───────────────────────────────────────────────────

        private void UpdateDaDataKeyStatus()
        {
            try
            {
                bool hasKey = CounterpartyApiKeysService.HasDaDataApiKey();
                DaDataApiKeySettingsBox.Password = hasKey ? "********" : "";
                DaDataKeySettingsStatusTextBlock.Text = hasKey
                    ? "✓ Ключ DaData сохранён в защищённом хранилище Windows."
                    : "Ключ DaData не настроен — автозаполнение по ИНН и поиск банка по БИК недоступны.";
            }
            catch (Exception ex)
            {
                AppLogger.LogError("SettingsPage.UpdateDaDataKeyStatus", ex);
            }
        }

        private void SaveDaDataKeyFromSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string raw = DaDataApiKeySettingsBox.Password ?? "";

                if (raw == "********")
                {
                    DaDataKeySettingsStatusTextBlock.Text = "Ключ оставлен без изменений.";
                    return;
                }

                if (string.IsNullOrWhiteSpace(raw))
                {
                    DaDataKeySettingsStatusTextBlock.Text = "Введите ключ DaData или нажмите «Удалить».";
                    return;
                }

                CounterpartyApiKeysService.SaveDaDataApiKey(raw.Trim());
                DaDataApiKeySettingsBox.Password = "********";
                DaDataKeySettingsStatusTextBlock.Text = "✓ Ключ DaData сохранён в защищённом хранилище Windows.";
            }
            catch (Exception ex)
            {
                AppLogger.LogError("SettingsPage.SaveDaDataKey", ex);
                DaDataKeySettingsStatusTextBlock.Text = "Не удалось сохранить ключ: " + ex.Message;
            }
        }

        private void ClearDaDataKeyFromSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                CounterpartyApiKeysService.SaveDaDataApiKey("");
                DaDataApiKeySettingsBox.Password = "";
                DaDataKeySettingsStatusTextBlock.Text = "Ключ DaData удалён.";
            }
            catch (Exception ex)
            {
                AppLogger.LogError("SettingsPage.ClearDaDataKey", ex);
                DaDataKeySettingsStatusTextBlock.Text = "Не удалось удалить ключ: " + ex.Message;
            }
        }

        // ─── Тема ─────────────────────────────────────────────────────────────

        private void ThemePreviewCard_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (sender is not FrameworkElement { Tag: string themeKey }) return;

            string status = themeKey switch
            {
                ThemeService.ThemeDark => "✓ Тёмная тема применена.",
                ThemeService.ThemeLight => "✓ Светлая тема применена.",
                ThemeService.ThemeMilitary => "✓ Военная тема применена.",
                _ => ""
            };
            if (string.IsNullOrEmpty(status)) return;

            ThemeService.ApplyTheme(themeKey);
            UpdateThemeCardSelection();
            ThemeStatusTextBlock.Text = status;
        }

        private void UpdateThemeCardSelection()
        {
            string theme = ThemeService.CurrentTheme;

            var accentBrush  = ThemeService.GetBrush("NiatecAccentBrush");
            var milAccent    = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 100, 160, 50));
            var defaultBrush = ThemeService.GetBrush("NiatecBorderBrush");

            DarkThemeCard.BorderBrush = theme == ThemeService.ThemeDark ? accentBrush : defaultBrush;
            DarkThemeCard.BorderThickness = theme == ThemeService.ThemeDark
                ? new Thickness(2.5) : new Thickness(1.5);

            LightThemeCard.BorderBrush = theme == ThemeService.ThemeLight ? accentBrush : defaultBrush;
            LightThemeCard.BorderThickness = theme == ThemeService.ThemeLight
                ? new Thickness(2.5) : new Thickness(1.5);

            if (MilitaryThemeCard != null)
            {
                MilitaryThemeCard.BorderBrush = theme == ThemeService.ThemeMilitary ? milAccent : defaultBrush;
                MilitaryThemeCard.BorderThickness = theme == ThemeService.ThemeMilitary
                    ? new Thickness(2.5) : new Thickness(1.5);
            }
        }

        // ─── Данные приложения и совместная работа ─────────────────────────────

        private void SafeUpdateStorageInfo()
        {
            try
            {
                var settings = DatabaseConnectionSettingsService.Load();
                bool isSqlServerMode = settings.ProviderMode == DatabaseProviderMode.SqlServer;

                string storageRoot = "Не определена";

                try
                {
                    storageRoot = AppPaths.StorageRoot;
                }
                catch (Exception ex)
                {
                    AppLogger.LogError("SettingsPage.AppPaths.StorageRoot", ex);
                }

                // Верхняя строка под заголовком страницы
                if (StoragePathInfoTextBlock != null)
                {
                    StoragePathInfoTextBlock.Text = isSqlServerMode
                        ? "Включена совместная работа. Данные хранятся в общей базе SQL Server."
                        : "Локальный режим. Сервер не используется, данные хранятся на этом компьютере.";
                }

                // Карточка “Данные приложения”
                if (DataModeTextBlock != null)
                {
                    DataModeTextBlock.Text = isSqlServerMode
                        ? "Текущий режим: Совместная работа"
                        : "Текущий режим: Локальный";
                }

                if (StorageHelpTextBlock != null)
                {
                    StorageHelpTextBlock.Text = isSqlServerMode
                        ? "В корпоративном режиме записи клиентов хранятся в SQL Server, а файлы клиентов — в общей папке."
                        : "Здесь хранятся база приложения, файлы клиентов, документы и резервные копии.";
                }

                if (StorageCurrentPathTextBlock != null)
                {
                    StorageCurrentPathTextBlock.Text = storageRoot;
                }

                if (ResetStorageButton != null)
                {
                    bool custom = false;

                    try
                    {
                        custom = AppPaths.IsCustomStorage;
                    }
                    catch (Exception ex)
                    {
                        AppLogger.LogError("SettingsPage.AppPaths.IsCustomStorage", ex);
                    }

                    ResetStorageButton.Visibility = custom
                        ? Visibility.Visible
                        : Visibility.Collapsed;
                }

                // Карточка “Совместная работа”
                if (SqlServerModeTextBlock != null)
                {
                    SqlServerModeTextBlock.Text = isSqlServerMode
                        ? "Совместная работа включена."
                        : "Сейчас приложение работает в локальном режиме.";
                }

                if (SharedWorkDescriptionTextBlock != null)
                {
                    SharedWorkDescriptionTextBlock.Text = isSqlServerMode
                        ? "Несколько сотрудников могут работать с одной базой одновременно. Для работы должен быть доступен SQL Server и общая папка файлов."
                        : "Этот режим подходит, если с программой работает один пользователь на одном компьютере. Если нужно, чтобы несколько сотрудников работали с одной базой, настройте совместную работу.";
                }

                if (SharedWorkDetailsTextBlock != null)
                {
                    SharedWorkDetailsTextBlock.Text = isSqlServerMode
                        ? BuildSqlServerDetailsText(settings)
                        : "Сервер не используется. Все данные хранятся на этом компьютере.";
                }

                // Карточка “Резервные копии”
                if (BackupStatusTextBlock != null)
                {
                    BackupStatusTextBlock.Text = isSqlServerMode
                        ? "В корпоративном режиме резервное копирование SQL Server нужно настраивать отдельно. Файлы клиентов хранятся в общей папке."
                        : "Резервные копии сохраняются внутри папки данных приложения.";
                }

                if (CreateBackupButton != null)
                {
                    CreateBackupButton.IsEnabled = !isSqlServerMode;
                    CreateBackupButton.Content = isSqlServerMode
                        ? "Резервная копия SQL Server настраивается отдельно"
                        : "Создать резервную копию";
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError("SettingsPage.SafeUpdateStorageInfo", ex);

                if (StoragePathInfoTextBlock != null)
                    StoragePathInfoTextBlock.Text = "Не удалось загрузить сведения о данных приложения.";

                if (DataModeTextBlock != null)
                    DataModeTextBlock.Text = "Текущий режим: не определён";

                if (StorageHelpTextBlock != null)
                    StorageHelpTextBlock.Text = "Не удалось обновить описание хранилища.";

                if (StorageCurrentPathTextBlock != null)
                    StorageCurrentPathTextBlock.Text = "Не удалось определить папку данных.";

                if (ResetStorageButton != null)
                    ResetStorageButton.Visibility = Visibility.Collapsed;

                if (SqlServerModeTextBlock != null)
                    SqlServerModeTextBlock.Text = "Не удалось определить режим работы базы.";

                if (SharedWorkDescriptionTextBlock != null)
                    SharedWorkDescriptionTextBlock.Text = "Не удалось загрузить описание совместной работы.";

                if (SharedWorkDetailsTextBlock != null)
                    SharedWorkDetailsTextBlock.Text = "";

                if (BackupStatusTextBlock != null)
                    BackupStatusTextBlock.Text = "Не удалось обновить сведения о резервных копиях.";

                if (CreateBackupButton != null)
                {
                    CreateBackupButton.IsEnabled = false;
                    CreateBackupButton.Content = "Создать резервную копию";
                }
            }
        }

        private static string BuildSqlServerDetailsText(DatabaseConnectionSettings settings)
        {
            string server = "не указан";
            string database = "не указана";
            string sharedFolder = string.IsNullOrWhiteSpace(settings.SharedClientFilesFolder)
                ? "не указана"
                : settings.SharedClientFilesFolder.Trim();

            if (SqlConnectionStringDisplay.TryParseEndpoints(settings.SqlServerConnectionString, out var ds, out var ic))
            {
                if (!string.IsNullOrWhiteSpace(ds))
                    server = ds;
                if (!string.IsNullOrWhiteSpace(ic))
                    database = ic;
            }

            return
                "Данные хранятся в общей базе SQL Server." +
                Environment.NewLine +
                Environment.NewLine +
                "Сервер:" +
                Environment.NewLine +
                server +
                Environment.NewLine +
                Environment.NewLine +
                "База данных:" +
                Environment.NewLine +
                database +
                Environment.NewLine +
                Environment.NewLine +
                "Папка файлов клиентов:" +
                Environment.NewLine +
                sharedFolder;
        }

        // ─── Хранилище ────────────────────────────────────────────────────────

        private void OpenStorageFolderButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                OpenFolder(AppPaths.StorageRoot);
            }
            catch (Exception ex)
            {
                AppLogger.LogError("SettingsPage.OpenStorageFolderButton_Click", ex);
            }
        }

        private void OpenStorageSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Frame.Navigate(typeof(DataStorageSettingsPage));
            }
            catch (Exception ex)
            {
                AppLogger.LogError("SettingsPage.OpenStorageSettingsButton_Click", ex);
            }
        }

        private void OpenDatabaseConnectionButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Frame.Navigate(typeof(DatabaseConnectionSettingsPage));
            }
            catch (Exception ex)
            {
                AppLogger.LogError("SettingsPage.OpenDatabaseConnectionButton_Click", ex);
            }
        }

        private async void ResetStorageButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new ContentDialog
                {
                    Title = "Сбросить папку данных?",
                    Content =
                        "Приложение снова будет использовать стандартную папку:" +
                        Environment.NewLine +
                        AppPaths.DefaultStorageRoot +
                        Environment.NewLine +
                        Environment.NewLine +
                        "Текущие данные в нестандартной папке не удаляются.",
                    PrimaryButtonText = "Сбросить",
                    CloseButtonText = "Отмена",
                    XamlRoot = XamlRoot
                };

                if (await dialog.ShowAsync() != ContentDialogResult.Primary)
                    return;

                StorageLocationService.ClearCustomStorageRoot();

                await new ContentDialog
                {
                    Title = "Папка данных сброшена",
                    Content = "Изменения вступят в силу после перезапуска приложения.",
                    CloseButtonText = "ОК",
                    XamlRoot = XamlRoot
                }.ShowAsync();
            }
            catch (Exception ex)
            {
                AppLogger.LogError("SettingsPage.ResetStorageButton_Click", ex);
            }
        }

        // ─── Резервные копии ──────────────────────────────────────────────────

        private async void CreateBackupButton_Click(object sender, RoutedEventArgs e)
        {
            var settings = DatabaseConnectionSettingsService.Load();

            if (settings.ProviderMode == DatabaseProviderMode.SqlServer)
            {
                await new ContentDialog
                {
                    Title = "Корпоративный режим",
                    Content =
                        "Сейчас приложение работает через SQL Server." +
                        Environment.NewLine +
                        Environment.NewLine +
                        "Резервное копирование SQL Server нужно настраивать отдельно. " +
                        "Эта кнопка предназначена для локального режима.",
                    CloseButtonText = "Понятно",
                    XamlRoot = XamlRoot
                }.ShowAsync();

                return;
            }

            if (CreateBackupButton != null)
                CreateBackupButton.IsEnabled = false;

            if (BackupStatusTextBlock != null)
                BackupStatusTextBlock.Text = "Создаётся резервная копия...";

            string backupPath = string.Empty;

            try
            {
                backupPath = await Task.Run(() => BackupService.CreateBackup());

                if (BackupStatusTextBlock != null)
                    BackupStatusTextBlock.Text = "✓ Копия создана: " + Path.GetFileName(backupPath);
            }
            catch (Exception ex)
            {
                AppLogger.LogError("SettingsPage.CreateBackupButton_Click", ex);

                if (BackupStatusTextBlock != null)
                    BackupStatusTextBlock.Text = "Ошибка: " + ex.Message;

                if (CreateBackupButton != null)
                    CreateBackupButton.IsEnabled = true;

                return;
            }

            if (CreateBackupButton != null)
                CreateBackupButton.IsEnabled = true;

            try
            {
                var dialog = new ContentDialog
                {
                    Title = "Резервная копия создана",
                    Content = "Сохранено в:" + Environment.NewLine + backupPath,
                    PrimaryButtonText = "Открыть папку",
                    CloseButtonText = "ОК",
                    XamlRoot = XamlRoot
                };

                if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                    OpenFolder(backupPath);
            }
            catch (Exception ex)
            {
                AppLogger.LogError("SettingsPage.CreateBackupButton_Click.Dialog", ex);
            }
        }

        private void OpenBackupsFolderButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                OpenFolder(AppPaths.BackupsFolder);
            }
            catch (Exception ex)
            {
                AppLogger.LogError("SettingsPage.OpenBackupsFolderButton_Click", ex);
            }
        }

        private void OpenBackupsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Frame.Navigate(typeof(BackupsPage));
            }
            catch (Exception ex)
            {
                AppLogger.LogError("SettingsPage.OpenBackupsButton_Click", ex);
            }
        }

        private static void OpenFolder(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            Directory.CreateDirectory(path);

            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = "\"" + path + "\"",
                UseShellExecute = true
            });
        }
    }
}