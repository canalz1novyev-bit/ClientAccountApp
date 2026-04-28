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
            _defaultStorageRoot = global::Windows.Storage.ApplicationData.Current.LocalFolder.Path;
            _storageRoot = ResolveStorageRoot();

            _databasePath = Path.Combine(_storageRoot, DatabaseFileName);
            _backupsFolder = Path.Combine(_storageRoot, BackupsFolderName);
            _clientFilesFolder = Path.Combine(_storageRoot, ClientFilesFolderName);

            Directory.CreateDirectory(_storageRoot);
            Directory.CreateDirectory(_backupsFolder);
            Directory.CreateDirectory(_clientFilesFolder);
        }

        /// <summary>
        /// Текущая рабочая папка хранения данных.
        /// Для совместимости старое имя AppDataFolder оставляем.
        /// </summary>
        public static string AppDataFolder => _storageRoot;

        /// <summary>
        /// Текущая рабочая папка хранения данных.
        /// </summary>
        public static string StorageRoot => _storageRoot;

        /// <summary>
        /// Стандартная локальная папка приложения.
        /// </summary>
        public static string DefaultStorageRoot => _defaultStorageRoot;

        public static string DatabasePath => _databasePath;
        public static string BackupsFolder => _backupsFolder;
        public static string ClientFilesFolder => _clientFilesFolder;

        public static bool IsCustomStorage
        {
            get
            {
                return !string.Equals(
                    _storageRoot,
                    _defaultStorageRoot,
                    StringComparison.OrdinalIgnoreCase);
            }
        }

        private static string ResolveStorageRoot()
        {
            string? customRoot = StorageLocationService.GetCustomStorageRoot();

            if (string.IsNullOrWhiteSpace(customRoot))
                return _defaultStorageRoot;

            customRoot = customRoot.Trim();

            try
            {
                Directory.CreateDirectory(customRoot);
                Directory.CreateDirectory(Path.Combine(customRoot, BackupsFolderName));
                Directory.CreateDirectory(Path.Combine(customRoot, ClientFilesFolderName));

                return customRoot;
            }
            catch
            {
                // Если сетевой диск или внешняя папка недоступны,
                // приложение не должно падать при запуске.
                // В этом случае временно возвращаемся к локальному хранилищу.
                return _defaultStorageRoot;
            }
        }
    }
}