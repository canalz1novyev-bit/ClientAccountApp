using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;
using System.Threading.Tasks;

namespace ClientAccountApp
{
    public sealed partial class DatabaseConnectionSettingsPage : Page
    {
        public DatabaseConnectionSettingsPage()
        {
            this.InitializeComponent();
            LoadSettingsIntoForm();
        }

        private void LoadSettingsIntoForm()
        {
            var settings = DatabaseConnectionSettingsService.Load();

            LocalSqliteRadioButton.IsChecked = settings.ProviderMode == DatabaseProviderMode.SQLite;
            SqlServerRadioButton.IsChecked = settings.ProviderMode == DatabaseProviderMode.SqlServer;

            SqlServerConnectionStringTextBox.Text = settings.SqlServerConnectionString ?? "";
            SharedClientFilesFolderTextBox.Text = settings.SharedClientFilesFolder ?? "";

            CurrentSettingsFileTextBlock.Text =
                "Файл настроек: " + DatabaseConnectionSettingsService.GetSettingsFilePath();

            if (settings.ProviderMode == DatabaseProviderMode.SqlServer)
            {
                CurrentDatabaseModeTextBlock.Text = "Текущий режим: серверная база SQL Server";

                if (SqlConnectionStringDisplay.TryParseEndpoints(settings.SqlServerConnectionString, out var ds, out var ic))
                {
                    CurrentDatabaseServerTextBlock.Text =
                        "Сервер: " + (string.IsNullOrWhiteSpace(ds) ? "—" : ds);

                    CurrentDatabaseNameTextBlock.Text =
                        "База данных: " + (string.IsNullOrWhiteSpace(ic) ? "—" : ic);
                }
                else
                {
                    CurrentDatabaseServerTextBlock.Text = "Сервер: не удалось разобрать строку подключения";
                    CurrentDatabaseNameTextBlock.Text = "База данных: не удалось определить";
                }

                CurrentFilesFolderTextBlock.Text =
                    "Папка файлов: " +
                    (string.IsNullOrWhiteSpace(settings.SharedClientFilesFolder)
                        ? "не указана"
                        : settings.SharedClientFilesFolder);

                return;
            }

            CurrentDatabaseModeTextBlock.Text = "Текущий режим: локальная база SQLite";
            CurrentDatabaseServerTextBlock.Text = "Сервер: не используется";
            CurrentDatabaseNameTextBlock.Text = "База данных: " + AppPaths.DatabasePath;
            CurrentFilesFolderTextBlock.Text = "Папка файлов: " + AppPaths.ClientFilesFolder;
        }
        private async void MigrateSqliteToSqlServerButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                MigrateSqliteToSqlServerButton.IsEnabled = false;

                DatabaseConnectionStatusTextBlock.Text =
                    "Начинаю перенос локальной SQLite-базы в SQL Server. Не закрывайте приложение...";

                var result = await Task.Run(() =>
                    SqliteToSqlServerMigrationService.MigrateLocalSqliteToSqlServer());

                DatabaseConnectionStatusTextBlock.Text =
                    result.ToString() + Environment.NewLine +
                    "Перезапустите приложение и проверьте данные в серверном режиме.";
            }
            catch (Exception ex)
            {
                DatabaseConnectionStatusTextBlock.Text =
                    "Не удалось выполнить перенос: " + ex.Message;
            }
            finally
            {
                MigrateSqliteToSqlServerButton.IsEnabled = true;
            }
        }
        private async void TestSqlServerConnectionButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string connectionString = SqlServerConnectionStringTextBox.Text.Trim();

                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    DatabaseConnectionStatusTextBlock.Text = "Укажите строку подключения к SQL Server.";
                    return;
                }

                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                DatabaseConnectionStatusTextBlock.Text =
                    "Подключение к SQL Server успешно. Теперь можно сохранить серверный режим.";
            }
            catch (Exception ex)
            {
                DatabaseConnectionStatusTextBlock.Text =
                    "Не удалось подключиться к SQL Server: " + ex.Message;
            }
        }

        private void SaveSqlServerModeButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string connectionString = SqlServerConnectionStringTextBox.Text.Trim();
                string sharedFilesFolder = SharedClientFilesFolderTextBox.Text.Trim().Trim('"');

                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    DatabaseConnectionStatusTextBlock.Text = "Укажите строку подключения к SQL Server.";
                    return;
                }

                if (string.IsNullOrWhiteSpace(sharedFilesFolder))
                {
                    DatabaseConnectionStatusTextBlock.Text =
                        "Укажите общую папку файлов клиентов. В корпоративном режиме файлы должны быть доступны всем пользователям.";
                    return;
                }

                DatabaseConnectionSettingsService.UseSqlServer(
                    connectionString,
                    sharedFilesFolder);

                DatabaseConnectionStatusTextBlock.Text =
                    "Серверный режим сохранён. Перезапустите приложение, чтобы оно начало работать с SQL Server.";

                LoadSettingsIntoForm();
            }
            catch (Exception ex)
            {
                DatabaseConnectionStatusTextBlock.Text =
                    "Не удалось сохранить серверный режим: " + ex.Message;
            }
        }
        private void OpenDatabaseDiagnosticsButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(DatabaseDiagnosticsPage));
        }
        private void UseLocalSqliteButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                DatabaseConnectionSettingsService.UseLocalSqlite();

                DatabaseConnectionStatusTextBlock.Text =
                    "Локальный режим SQLite сохранён. Перезапустите приложение.";

                LoadSettingsIntoForm();
            }
            catch (Exception ex)
            {
                DatabaseConnectionStatusTextBlock.Text =
                    "Не удалось включить локальный режим: " + ex.Message;
            }
        }
    }
}