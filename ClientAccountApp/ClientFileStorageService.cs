using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace ClientAccountApp
{
    public static class ClientFileStorageService
    {
        public static string GetFilesRootFolder()
        {
            string rootFolder = AppPaths.ClientFilesFolder;
            Directory.CreateDirectory(rootFolder);
            return rootFolder;
        }

        public static string GetClientFolder(int clientId)
        {
            string clientFolder = Path.Combine(GetFilesRootFolder(), clientId.ToString());
            Directory.CreateDirectory(clientFolder);
            return clientFolder;
        }

        public static (string FullPath, string RelativePath, long FileSizeBytes) CopyFileForClient(int clientId, string sourceFilePath)
        {
            if (string.IsNullOrWhiteSpace(sourceFilePath))
            {
                throw new ArgumentException("Не указан путь к исходному файлу.", nameof(sourceFilePath));
            }

            if (!File.Exists(sourceFilePath))
            {
                throw new FileNotFoundException("Исходный файл не найден.", sourceFilePath);
            }

            string clientFolder = GetClientFolder(clientId);

            string originalFileName = Path.GetFileName(sourceFilePath);
            string safeFileNameWithoutExtension = MakeSafeFileName(Path.GetFileNameWithoutExtension(originalFileName));
            string extension = Path.GetExtension(originalFileName);

            string storedFileName = $"{DateTime.Now:yyyyMMdd_HHmmssfff}_{safeFileNameWithoutExtension}{extension}";
            string destinationFullPath = Path.Combine(clientFolder, storedFileName);
            File.Copy(sourceFilePath, destinationFullPath, true);

            string relativePath = Path.Combine(clientId.ToString(), storedFileName);
            long fileSizeBytes = new FileInfo(destinationFullPath).Length;

            return (destinationFullPath, relativePath, fileSizeBytes);
        }

        public static string GetFullPath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return string.Empty;
            }

            string normalizedRelativePath = relativePath
                .Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar);

            return Path.Combine(GetFilesRootFolder(), normalizedRelativePath);
        }

        public static void DeleteFileIfExists(string fullPath)
        {
            if (!string.IsNullOrWhiteSpace(fullPath) && File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }

        public static void DeleteClientFolderIfEmpty(int clientId)
        {
            string clientFolder = Path.Combine(GetFilesRootFolder(), clientId.ToString());

            if (!Directory.Exists(clientFolder))
            {
                return;
            }

            bool hasFiles = Directory.GetFiles(clientFolder).Length > 0;
            bool hasDirectories = Directory.GetDirectories(clientFolder).Length > 0;

            if (!hasFiles && !hasDirectories)
            {
                Directory.Delete(clientFolder, true);
            }
        }

        public static (string FullPath, string RelativePath) GetNewPdfPathForClient(int clientId, string originalFileNameWithoutExtension)
        {
            string clientFolder = GetClientFolder(clientId);

            string safeFileName = MakeSafeFileName(originalFileNameWithoutExtension);
            string storedFileName = $"{DateTime.Now:yyyyMMdd_HHmmssfff}_{safeFileName}.pdf";

            string fullPath = Path.Combine(clientFolder, storedFileName);
            string relativePath = Path.Combine(clientId.ToString(), storedFileName);

            return (fullPath, relativePath);
        }

        public static void OpenFile(string fullPath)
        {
            if (string.IsNullOrWhiteSpace(fullPath))
                throw new ArgumentException("Путь к файлу пустой.", nameof(fullPath));

            fullPath = Path.GetFullPath(fullPath);

            if (!File.Exists(fullPath))
                throw new FileNotFoundException("Файл не найден на диске.", fullPath);

            var startInfo = new ProcessStartInfo
            {
                FileName = fullPath,
                UseShellExecute = true,
                Verb = "open",
                WorkingDirectory = Path.GetDirectoryName(fullPath) ?? AppContext.BaseDirectory
            };

            Process.Start(startInfo);
        }

        public static void RevealFileInExplorer(string path)
        {
            string resolvedPath = ResolvePath(path);

            if (!File.Exists(resolvedPath))
            {
                throw new FileNotFoundException("Файл не найден.", resolvedPath);
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{resolvedPath}\"",
                UseShellExecute = true
            });
        }

        private static string ResolvePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            return Path.IsPathRooted(path) ? path : GetFullPath(path);
        }

        private static string MakeSafeFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return "file";
            }

            foreach (char invalidChar in Path.GetInvalidFileNameChars())
            {
                fileName = fileName.Replace(invalidChar, '_');
            }

            return fileName.Trim();
        }
        public static string GetOrCreateClientFolderPath(ClientInfo client)
        {
            if (client == null)
                throw new ArgumentNullException(nameof(client));

            Directory.CreateDirectory(AppPaths.ClientFilesFolder);

            var clientId = client.Id;
            var targetFolderName = BuildClientFolderName(client);
            var targetPath = Path.Combine(AppPaths.ClientFilesFolder, targetFolderName);

            var legacyNumericPath = clientId > 0
                ? Path.Combine(AppPaths.ClientFilesFolder, clientId.ToString())
                : string.Empty;

            if (!string.IsNullOrWhiteSpace(legacyNumericPath) &&
                Directory.Exists(legacyNumericPath) &&
                !Directory.Exists(targetPath))
            {
                Directory.Move(legacyNumericPath, targetPath);
                UpdateClientFileRelativePaths(clientId, clientId.ToString(), targetFolderName);
            }
            else if (!Directory.Exists(targetPath))
            {
                Directory.CreateDirectory(targetPath);
            }

            return targetPath;
        }

        public static void OpenClientFolder(ClientInfo client)
        {
            var folderPath = GetOrCreateClientFolderPath(client);

            Process.Start(new ProcessStartInfo
            {
                FileName = folderPath,
                UseShellExecute = true
            });
        }

        private static string BuildClientFolderName(ClientInfo client)
        {
            var name = SanitizeFolderPart(GetClientDisplayName(client));
            var inn = SanitizeFolderPart(GetClientInn(client));
            var clientId = GetClientId(client);

            if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(inn))
                return $"{name}_{inn}";

            if (!string.IsNullOrWhiteSpace(name))
                return clientId > 0 ? $"{name}_{clientId}" : name;

            if (!string.IsNullOrWhiteSpace(inn))
                return $"Клиент_{inn}";

            return $"Клиент_{clientId}";
        }

        private static string SanitizeFolderPart(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var invalidChars = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(value.Trim());

            foreach (var ch in invalidChars)
                builder.Replace(ch, '_');

            var cleaned = builder.ToString();

            while (cleaned.Contains("__"))
                cleaned = cleaned.Replace("__", "_");

            return cleaned.Trim(' ', '.');
        }

        private static int GetClientId(ClientInfo client)
        {
            var type = client.GetType();
            var property = type.GetProperty("Id") ?? type.GetProperty("ClientId");
            if (property == null)
                return 0;

            var value = property.GetValue(client);
            if (value is int intValue)
                return intValue;

            return int.TryParse(value?.ToString(), out var parsed) ? parsed : 0;
        }

        private static string GetClientDisplayName(ClientInfo client)
        {
            var type = client.GetType();

            foreach (var propertyName in new[] { "DisplayName", "ClientName", "Name", "OrganizationName", "FullName", "Title" })
            {
                var property = type.GetProperty(propertyName);
                var value = property?.GetValue(client)?.ToString();

                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }

            return string.Empty;
        }

        private static string GetClientInn(ClientInfo client)
        {
            var type = client.GetType();

            foreach (var propertyName in new[] { "Inn", "INN" })
            {
                var property = type.GetProperty(propertyName);
                var value = property?.GetValue(client)?.ToString();

                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }

            return string.Empty;
        }
        public static void EnsureClientFoldersNamed(IEnumerable<ClientInfo> clients)
        {
            foreach (var client in clients)
            {
                EnsureNamedClientFolder(client);
            }
        }

        public static void DeleteClientFolderIfEmpty(ClientInfo client)
        {
            if (client == null)
                return;

            var namedFolder = GetNamedClientFolderPath(client);
            DeleteFolderIfEmptyInternal(namedFolder);

            var legacyNumericFolder = Path.Combine(AppPaths.ClientFilesFolder, client.Id.ToString());
            if (!string.Equals(namedFolder, legacyNumericFolder, StringComparison.OrdinalIgnoreCase))
            {
                DeleteFolderIfEmptyInternal(legacyNumericFolder);
            }
        }

        public static dynamic CopyFileForClient(ClientInfo client, string sourceFilePath)
        {
            if (client == null)
                throw new ArgumentNullException(nameof(client));

            if (string.IsNullOrWhiteSpace(sourceFilePath))
                throw new ArgumentException("Не указан путь к исходному файлу.", nameof(sourceFilePath));

            if (!File.Exists(sourceFilePath))
                throw new FileNotFoundException("Исходный файл не найден.", sourceFilePath);

            var clientFolder = EnsureNamedClientFolder(client);
            var targetFileName = GetUniqueFileName(clientFolder, Path.GetFileName(sourceFilePath));
            var targetFullPath = Path.Combine(clientFolder, targetFileName);

            File.Copy(sourceFilePath, targetFullPath, overwrite: false);

            return new
            {
                RelativePath = Path.Combine(Path.GetFileName(clientFolder), targetFileName),
                FileSizeBytes = new FileInfo(targetFullPath).Length
            };
        }

        public static dynamic GetNewPdfPathForClient(ClientInfo client, string baseFileName)
        {
            if (client == null)
                throw new ArgumentNullException(nameof(client));

            var clientFolder = EnsureNamedClientFolder(client);

            var safeBaseFileName = SanitizePathPart(baseFileName);
            if (string.IsNullOrWhiteSpace(safeBaseFileName))
                safeBaseFileName = "Файл";

            if (!safeBaseFileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                safeBaseFileName += ".pdf";

            var targetFileName = GetUniqueFileName(clientFolder, safeBaseFileName);
            var targetFullPath = Path.Combine(clientFolder, targetFileName);

            return new
            {
                FullPath = targetFullPath,
                RelativePath = Path.Combine(Path.GetFileName(clientFolder), targetFileName)
            };
        }

        private static string EnsureNamedClientFolder(ClientInfo client)
        {
            Directory.CreateDirectory(AppPaths.ClientFilesFolder);

            var namedFolder = GetNamedClientFolderPath(client);
            var legacyNumericFolder = Path.Combine(AppPaths.ClientFilesFolder, client.Id.ToString());

            if (Directory.Exists(legacyNumericFolder) && !Directory.Exists(namedFolder))
            {
                Directory.Move(legacyNumericFolder, namedFolder);
                UpdateClientFileRelativePaths(client.Id, client.Id.ToString(), Path.GetFileName(namedFolder));
            }
            else if (!Directory.Exists(namedFolder))
            {
                Directory.CreateDirectory(namedFolder);
            }

            return namedFolder;
        }

        private static string GetNamedClientFolderPath(ClientInfo client)
        {
            var clientName = SanitizePathPart(client.Name);
            var inn = SanitizePathPart(client.Inn);

            string folderName;

            if (!string.IsNullOrWhiteSpace(clientName) && !string.IsNullOrWhiteSpace(inn))
            {
                folderName = $"{clientName}_{inn}";
            }
            else if (!string.IsNullOrWhiteSpace(clientName))
            {
                folderName = $"{clientName}_{client.Id}";
            }
            else if (!string.IsNullOrWhiteSpace(inn))
            {
                folderName = $"Клиент_{inn}";
            }
            else
            {
                folderName = $"Клиент_{client.Id}";
            }

            return Path.Combine(AppPaths.ClientFilesFolder, folderName);
        }

        private static void UpdateClientFileRelativePaths(int clientId, string oldFolderName, string newFolderName)
        {
            if (string.Equals(oldFolderName, newFolderName, StringComparison.OrdinalIgnoreCase))
                return;

            using var db = new AppDbContext();

            var files = db.ClientFiles
                .Where(f => f.ClientInfoId == clientId)
                .ToList();

            foreach (var file in files)
            {
                if (string.IsNullOrWhiteSpace(file.RelativePath))
                    continue;

                var normalized = file.RelativePath.Replace('/', '\\');
                var oldPrefix = oldFolderName + "\\";

                if (normalized.StartsWith(oldPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    file.RelativePath = Path.Combine(newFolderName, normalized.Substring(oldPrefix.Length));
                }
            }

            db.SaveChanges();
        }

        private static string GetUniqueFileName(string folderPath, string fileName)
        {
            var safeFileName = SanitizePathPart(fileName);
            if (string.IsNullOrWhiteSpace(safeFileName))
                safeFileName = "Файл";

            var nameWithoutExtension = Path.GetFileNameWithoutExtension(safeFileName);
            var extension = Path.GetExtension(safeFileName);

            var candidate = safeFileName;
            var counter = 1;

            while (File.Exists(Path.Combine(folderPath, candidate)))
            {
                candidate = $"{nameWithoutExtension}_{counter}{extension}";
                counter++;
            }

            return candidate;
        }

        private static string SanitizePathPart(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var result = value.Trim();

            foreach (var ch in Path.GetInvalidFileNameChars())
            {
                result = result.Replace(ch, '_');
            }

            while (result.Contains("__"))
            {
                result = result.Replace("__", "_");
            }

            return result.Trim(' ', '.');
        }

        private static void DeleteFolderIfEmptyInternal(string folderPath)
        {
            if (!Directory.Exists(folderPath))
                return;

            if (Directory.EnumerateFileSystemEntries(folderPath).Any())
                return;

            Directory.Delete(folderPath, recursive: false);
        }
        public static void MigrateLegacyClientFoldersOnce()
        {
            string markerPath = Path.Combine(AppPaths.AppDataFolder, "client-folder-migration-v1.done");

            if (File.Exists(markerPath))
                return;

            Directory.CreateDirectory(AppPaths.ClientFilesFolder);

            using var db = new AppDbContext();

            var clients = db.Clients
                .AsNoTracking()
                .OrderBy(c => c.Id)
                .ToList();

            foreach (var client in clients)
            {
                MigrateLegacyClientFolder(db, client);
            }

            db.SaveChanges();
            File.WriteAllText(markerPath, DateTime.Now.ToString("O"));
        }

        private static void MigrateLegacyClientFolder(AppDbContext db, ClientInfo client)
        {
            string oldFolderName = client.Id.ToString();
            string oldFolderPath = Path.Combine(AppPaths.ClientFilesFolder, oldFolderName);

            if (!Directory.Exists(oldFolderPath))
                return;

            string newFolderName = BuildClientFolderName(client);
            string newFolderPath = Path.Combine(AppPaths.ClientFilesFolder, newFolderName);

            Directory.CreateDirectory(newFolderPath);

            var clientFiles = db.ClientFiles
                .Where(f => f.ClientInfoId == client.Id)
                .ToList();

            foreach (var clientFile in clientFiles)
            {
                if (string.IsNullOrWhiteSpace(clientFile.RelativePath))
                    continue;

                string normalizedRelativePath = clientFile.RelativePath.Replace('/', '\\');
                string oldPrefix = oldFolderName + "\\";

                if (!normalizedRelativePath.StartsWith(oldPrefix, StringComparison.OrdinalIgnoreCase))
                    continue;

                string sourceFullPath = Path.Combine(AppPaths.ClientFilesFolder, normalizedRelativePath);
                string originalFileName = Path.GetFileName(normalizedRelativePath);

                if (File.Exists(sourceFullPath))
                {
                    string targetFileName = GetUniqueFileName(newFolderPath, originalFileName);
                    string targetFullPath = Path.Combine(newFolderPath, targetFileName);

                    File.Move(sourceFullPath, targetFullPath);
                    clientFile.RelativePath = Path.Combine(newFolderName, targetFileName);
                }
                else
                {
                    clientFile.RelativePath = Path.Combine(newFolderName, originalFileName);
                }
            }

            foreach (var orphanFile in Directory.EnumerateFiles(oldFolderPath))
            {
                string targetFileName = GetUniqueFileName(newFolderPath, Path.GetFileName(orphanFile));
                string targetFullPath = Path.Combine(newFolderPath, targetFileName);
                File.Move(orphanFile, targetFullPath);
            }

            if (!Directory.EnumerateFileSystemEntries(oldFolderPath).Any())
            {
                Directory.Delete(oldFolderPath, recursive: false);
            }
        }
    }

}