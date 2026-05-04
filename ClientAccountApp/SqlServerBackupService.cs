using Microsoft.Data.SqlClient;
using System;
using System.Data;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ClientAccountApp
{
    public static class SqlServerBackupService
    {
        public static string GetBackupRootFolder()
        {
            return Path.Combine(
                @"C:\NIATEC",
                "Backups",
                "SqlServer");
        }

        public static async Task<string> CreateSqlServerBackupAsync()
        {
            var settings = DatabaseConnectionSettingsService.Load();

            if (settings.ProviderMode != DatabaseProviderMode.SqlServer)
                throw new InvalidOperationException("SQL Server-режим не включён.");

            if (string.IsNullOrWhiteSpace(settings.SqlServerConnectionString))
                throw new InvalidOperationException("Строка подключения SQL Server не указана.");

            var builder = new SqlConnectionStringBuilder(settings.SqlServerConnectionString);

            string databaseName = builder.InitialCatalog;

            if (string.IsNullOrWhiteSpace(databaseName))
                throw new InvalidOperationException("В строке подключения не указано имя базы данных.");

            string backupRoot = GetBackupRootFolder();

            Directory.CreateDirectory(backupRoot);

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string safeDatabaseName = MakeSafeFileName(databaseName);

            string backupFolder = Path.Combine(
                backupRoot,
                $"{timestamp}_{safeDatabaseName}");

            Directory.CreateDirectory(backupFolder);

            string backupFilePath = Path.Combine(
                backupFolder,
                $"{safeDatabaseName}_{timestamp}.bak");

            string escapedDatabaseName = EscapeSqlIdentifier(databaseName);

            string sql = $@"
BACKUP DATABASE [{escapedDatabaseName}]
TO DISK = @BackupPath
WITH INIT, FORMAT, NAME = @BackupName;
";

            await using var connection = new SqlConnection(settings.SqlServerConnectionString);
            await connection.OpenAsync();

            await using var command = new SqlCommand(sql, connection);
            command.CommandType = CommandType.Text;
            command.CommandTimeout = 300;

            command.Parameters.AddWithValue("@BackupPath", backupFilePath);
            command.Parameters.AddWithValue("@BackupName", $"NIATEC.Client backup {timestamp}");

            await command.ExecuteNonQueryAsync();

            if (!File.Exists(backupFilePath))
                throw new FileNotFoundException(
                    "SQL Server сообщил об успешном создании backup, но .bak файл не найден.",
                    backupFilePath);

            CopyClientFilesFolderIfExists(settings.SharedClientFilesFolder, backupFolder);

            return backupFolder;
        }

        private static void CopyClientFilesFolderIfExists(string? sourceFolder, string backupFolder)
        {
            if (string.IsNullOrWhiteSpace(sourceFolder))
                return;

            if (!Directory.Exists(sourceFolder))
                return;

            string destinationFolder = Path.Combine(backupFolder, "ClientFiles");

            CopyDirectory(sourceFolder, destinationFolder);
        }

        private static void CopyDirectory(string sourceDir, string destinationDir)
        {
            Directory.CreateDirectory(destinationDir);

            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string fileName = Path.GetFileName(file);
                string destinationFile = Path.Combine(destinationDir, fileName);

                File.Copy(file, destinationFile, overwrite: true);
            }

            foreach (string directory in Directory.GetDirectories(sourceDir))
            {
                string directoryName = Path.GetFileName(directory);
                string destinationSubDir = Path.Combine(destinationDir, directoryName);

                CopyDirectory(directory, destinationSubDir);
            }
        }

        private static string MakeSafeFileName(string value)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(c, '_');
            }

            return Regex.Replace(value, @"\s+", "_");
        }

        private static string EscapeSqlIdentifier(string value)
        {
            return value.Replace("]", "]]");
        }
    }
}