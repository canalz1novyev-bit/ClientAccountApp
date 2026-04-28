using System;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;

namespace ClientAccountApp
{
    public static class BackupService
    {
        public static string GetAppDataFolder()
        {
            return AppPaths.AppDataFolder;
        }

        public static string GetDatabasePath()
        {
            return AppPaths.DatabasePath;
        }

        public static string GetBackupFolder()
        {
            return AppPaths.BackupsFolder;
        }

        public static string CreateBackup()
        {
            string databasePath = AppPaths.DatabasePath;

            if (!File.Exists(databasePath))
            {
                throw new FileNotFoundException("Файл базы данных не найден.", databasePath);
            }

            SqliteConnection.ClearAllPools();

            string backupRootFolder = AppPaths.BackupsFolder;
            string backupName = $"backup_{DateTime.Now:yyyyMMdd_HHmmss}";
            string backupPath = Path.Combine(backupRootFolder, backupName);
            Directory.CreateDirectory(backupPath);

            string backupDatabasePath = Path.Combine(backupPath, "clients.db");
            File.Copy(databasePath, backupDatabasePath, true);

            string sourceFilesFolder = AppPaths.ClientFilesFolder;
            string backupFilesFolder = Path.Combine(backupPath, "ClientFiles");

            if (Directory.Exists(sourceFilesFolder))
            {
                CopyDirectory(sourceFilesFolder, backupFilesFolder);
            }

            File.WriteAllText(
                Path.Combine(backupPath, "backup_info.txt"),
                $"Создано: {DateTime.Now:dd.MM.yyyy HH:mm:ss}");

            return backupPath;
        }

        public static string RestoreLatestBackup()
        {
            string backupRootFolder = AppPaths.BackupsFolder;

            if (!Directory.Exists(backupRootFolder))
            {
                throw new FileNotFoundException("Папка резервных копий не найдена.");
            }

            string? latestBackupFolder = Directory
                .GetDirectories(backupRootFolder, "backup_*")
                .OrderByDescending(Directory.GetLastWriteTime)
                .FirstOrDefault();

            if (string.IsNullOrWhiteSpace(latestBackupFolder))
            {
                throw new FileNotFoundException("Полные резервные копии не найдены.");
            }

            string backupDatabasePath = Path.Combine(latestBackupFolder, "clients.db");

            if (!File.Exists(backupDatabasePath))
            {
                throw new FileNotFoundException("В резервной копии не найден файл clients.db.");
            }

            string currentDatabasePath = AppPaths.DatabasePath;

            SqliteConnection.ClearAllPools();

            DeleteFileIfExists(currentDatabasePath);
            DeleteFileIfExists(currentDatabasePath + "-shm");
            DeleteFileIfExists(currentDatabasePath + "-wal");

            File.Copy(backupDatabasePath, currentDatabasePath, true);

            string backupFilesFolder = Path.Combine(latestBackupFolder, "ClientFiles");
            string currentFilesFolder = AppPaths.ClientFilesFolder;

            DeleteDirectoryIfExists(currentFilesFolder);

            if (Directory.Exists(backupFilesFolder))
            {
                CopyDirectory(backupFilesFolder, currentFilesFolder);
            }
            else
            {
                Directory.CreateDirectory(currentFilesFolder);
            }

            return latestBackupFolder;
        }

        private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
        {
            Directory.CreateDirectory(destinationDirectory);

            foreach (string file in Directory.GetFiles(sourceDirectory))
            {
                string destinationFile = Path.Combine(destinationDirectory, Path.GetFileName(file));
                File.Copy(file, destinationFile, true);
            }

            foreach (string directory in Directory.GetDirectories(sourceDirectory))
            {
                string destinationSubDirectory = Path.Combine(destinationDirectory, Path.GetFileName(directory));
                CopyDirectory(directory, destinationSubDirectory);
            }
        }

        private static void DeleteFileIfExists(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private static void DeleteDirectoryIfExists(string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
    }
}