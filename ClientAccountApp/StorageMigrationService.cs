using System;
using System.IO;

namespace ClientAccountApp
{
    public static class StorageMigrationService
    {
        public static void CopyCurrentStorageTo(string targetRoot)
        {
            if (string.IsNullOrWhiteSpace(targetRoot))
                throw new ArgumentException("Не указан путь новой папки хранения.", nameof(targetRoot));

            targetRoot = targetRoot.Trim().Trim('"');

            string currentRoot = AppPaths.StorageRoot;

            if (string.Equals(
                    Path.GetFullPath(currentRoot).TrimEnd('\\'),
                    Path.GetFullPath(targetRoot).TrimEnd('\\'),
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Новая папка совпадает с текущей папкой хранения.");
            }

            Directory.CreateDirectory(targetRoot);

            string targetDbPath = Path.Combine(targetRoot, "clients.db");
            string targetClientFilesFolder = Path.Combine(targetRoot, "ClientFiles");
            string targetBackupsFolder = Path.Combine(targetRoot, "Backups");

            if (File.Exists(targetDbPath))
            {
                throw new InvalidOperationException(
                    "В выбранной папке уже есть файл clients.db. Чтобы не перезаписать чужую базу, перенос остановлен.");
            }

            Directory.CreateDirectory(targetClientFilesFolder);
            Directory.CreateDirectory(targetBackupsFolder);

            if (File.Exists(AppPaths.DatabasePath))
            {
                File.Copy(AppPaths.DatabasePath, targetDbPath, overwrite: false);
            }

            CopyDirectory(AppPaths.ClientFilesFolder, targetClientFilesFolder);
            CopyDirectory(AppPaths.BackupsFolder, targetBackupsFolder);
        }

        private static void CopyDirectory(string sourceDir, string targetDir)
        {
            if (!Directory.Exists(sourceDir))
                return;

            Directory.CreateDirectory(targetDir);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                string fileName = Path.GetFileName(file);
                string targetFile = Path.Combine(targetDir, fileName);

                if (!File.Exists(targetFile))
                    File.Copy(file, targetFile, overwrite: false);
            }

            foreach (var directory in Directory.GetDirectories(sourceDir))
            {
                string directoryName = Path.GetFileName(directory);
                string targetSubDir = Path.Combine(targetDir, directoryName);

                CopyDirectory(directory, targetSubDir);
            }
        }
    }
}