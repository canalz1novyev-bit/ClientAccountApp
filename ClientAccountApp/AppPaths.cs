using System;
using System.IO;

namespace ClientAccountApp
{
    public static class AppPaths
    {
        private const string DatabaseFileName = "clients.db";
        private const string BackupsFolderName = "Backups";
        private const string ClientFilesFolderName = "ClientFiles";

        private static readonly string _defaultStorageRoot;
        private static readonly string _storageRoot;
        private static readonly string _databasePath;
        private static readonly string _backupsFolder;
        private static readonly string _clientFilesFolder;

        static AppPaths()
        {
            _defaultStorageRoot = GetDefaultStorageRoot();
            _storageRoot = ResolveStorageRoot();

            _databasePath    = Path.Combine(_storageRoot, DatabaseFileName);
            _backupsFolder   = Path.Combine(_storageRoot, BackupsFolderName);
            _clientFilesFolder = Path.Combine(_storageRoot, ClientFilesFolderName);

            Directory.CreateDirectory(_storageRoot);
            Directory.CreateDirectory(_backupsFolder);
            Directory.CreateDirectory(_clientFilesFolder);
        }

        public static string AppDataFolder      => _storageRoot ?? _getFallback();
        public static string StorageRoot        => _storageRoot ?? _getFallback();
        public static string DefaultStorageRoot => _defaultStorageRoot ?? _getFallback();

        private static string _getFallback()
        {
            try
            {
                string appData = Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData);
                string folder = Path.Combine(appData, "ClientAccountApp");
                Directory.CreateDirectory(folder);
                return folder;
            }
            catch
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "ClientAccountApp");
            }
        }
        public static string DatabasePath       => _databasePath;
        public static string BackupsFolder      => _backupsFolder;
        public static string ClientFilesFolder  => _clientFilesFolder;

        public static bool IsCustomStorage =>
            !string.Equals(_storageRoot, _defaultStorageRoot, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Стандартный путь — Документы\ClientAccountApp.
        /// Виден в Проводнике, не теряется при переустановке Windows.
        /// </summary>
        private static string GetDefaultStorageRoot()
        {
            // Пробуем Документы
            try
            {
                string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                if (!string.IsNullOrWhiteSpace(documents))
                {
                    string folder = Path.Combine(documents, "ClientAccountApp");
                    Directory.CreateDirectory(folder);
                    return folder;
                }
            }
            catch { }

            // Запасной вариант — AppData (старое поведение)
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string fallback = Path.Combine(appData, "ClientAccountApp");
            Directory.CreateDirectory(fallback);
            return fallback;
        }

        private static string ResolveStorageRoot()
        {
            try
            {
                string? customRoot = StorageLocationService.GetCustomStorageRoot();
                if (!string.IsNullOrWhiteSpace(customRoot))
                {
                    customRoot = customRoot.Trim();
                    Directory.CreateDirectory(customRoot);
                    return customRoot;
                }
            }
            catch { }

            return _defaultStorageRoot;
        }
    }
}
