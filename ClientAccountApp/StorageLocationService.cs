using System;
using System.IO;
using System.Text.Json;

namespace ClientAccountApp
{
    public sealed class StorageLocationSettings
    {
        public string? CustomStorageRoot { get; set; }
    }

    public static class StorageLocationService
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
                return Path.Combine(SettingsFolder, "storage-location.json");
            }
        }

        public static string? GetCustomStorageRoot()
        {
            try
            {
                if (!File.Exists(SettingsFilePath))
                    return null;

                string json = File.ReadAllText(SettingsFilePath);

                var settings = JsonSerializer.Deserialize<StorageLocationSettings>(json, JsonOptions);

                if (settings == null)
                    return null;

                if (string.IsNullOrWhiteSpace(settings.CustomStorageRoot))
                    return null;

                return settings.CustomStorageRoot.Trim();
            }
            catch
            {
                return null;
            }
        }

        public static bool HasCustomStorageRoot()
        {
            string? customRoot = GetCustomStorageRoot();
            return !string.IsNullOrWhiteSpace(customRoot);
        }

        public static void SaveCustomStorageRoot(string storageRoot)
        {
            if (string.IsNullOrWhiteSpace(storageRoot))
                throw new ArgumentException("Не указан путь к хранилищу.", nameof(storageRoot));

            storageRoot = storageRoot.Trim();

            Directory.CreateDirectory(storageRoot);
            Directory.CreateDirectory(Path.Combine(storageRoot, "ClientFiles"));
            Directory.CreateDirectory(Path.Combine(storageRoot, "Backups"));

            var settings = new StorageLocationSettings
            {
                CustomStorageRoot = storageRoot
            };

            Directory.CreateDirectory(SettingsFolder);

            string json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(SettingsFilePath, json);
        }

        public static void ClearCustomStorageRoot()
        {
            if (File.Exists(SettingsFilePath))
                File.Delete(SettingsFilePath);
        }

        public static string GetSettingsFilePath()
        {
            return SettingsFilePath;
        }

        public static void EnsureStorageFolders(string storageRoot)
        {
            if (string.IsNullOrWhiteSpace(storageRoot))
                return;

            Directory.CreateDirectory(storageRoot);
            Directory.CreateDirectory(Path.Combine(storageRoot, "ClientFiles"));
            Directory.CreateDirectory(Path.Combine(storageRoot, "Backups"));
        }
    }
}