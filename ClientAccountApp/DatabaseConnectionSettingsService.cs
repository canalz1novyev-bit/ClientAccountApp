using System;
using System.IO;
using System.Text.Json;

namespace ClientAccountApp
{
    public enum DatabaseProviderMode
    {
        SQLite = 0,
        SqlServer = 1
    }

    public sealed class DatabaseConnectionSettings
    {
        public DatabaseProviderMode ProviderMode { get; set; } = DatabaseProviderMode.SQLite;

        public string SqlServerConnectionString { get; set; } = "";

        public string SharedClientFilesFolder { get; set; } = "";
    }

    public static class DatabaseConnectionSettingsService
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        private static string SettingsFolder
        {
            get
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return Path.Combine(localAppData, "ClientAccountApp");
            }
        }

        private static string SettingsFilePath
        {
            get
            {
                return Path.Combine(SettingsFolder, "database-connection.json");
            }
        }

        public static DatabaseConnectionSettings Load()
        {
            try
            {
                if (!File.Exists(SettingsFilePath))
                    return new DatabaseConnectionSettings();

                string json = File.ReadAllText(SettingsFilePath);

                var settings = JsonSerializer.Deserialize<DatabaseConnectionSettings>(json, JsonOptions);

                return settings ?? new DatabaseConnectionSettings();
            }
            catch
            {
                return new DatabaseConnectionSettings();
            }
        }

        public static void Save(DatabaseConnectionSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            Directory.CreateDirectory(SettingsFolder);

            string json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(SettingsFilePath, json);
        }

        public static void UseLocalSqlite()
        {
            var settings = Load();
            settings.ProviderMode = DatabaseProviderMode.SQLite;
            settings.SqlServerConnectionString = "";
            Save(settings);
        }

        public static void UseSqlServer(string connectionString, string sharedClientFilesFolder)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("Не указана строка подключения к SQL Server.", nameof(connectionString));

            var settings = Load();

            settings.ProviderMode = DatabaseProviderMode.SqlServer;
            settings.SqlServerConnectionString = connectionString.Trim();
            settings.SharedClientFilesFolder = (sharedClientFilesFolder ?? "").Trim();

            Save(settings);
        }

        public static bool IsSqlServerMode()
        {
            return Load().ProviderMode == DatabaseProviderMode.SqlServer;
        }

        public static string GetSettingsFilePath()
        {
            return SettingsFilePath;
        }
    }
}