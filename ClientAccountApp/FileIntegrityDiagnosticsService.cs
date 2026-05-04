using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ClientAccountApp
{
    public static class FileIntegrityDiagnosticsService
    {
        public static async Task<string> BuildReportAsync()
        {
            var result = new StringBuilder();

            var settings = DatabaseConnectionSettingsService.Load();

            string filesRootFolder = settings.ProviderMode == DatabaseProviderMode.SqlServer
                ? settings.SharedClientFilesFolder
                : AppPaths.ClientFilesFolder;

            result.AppendLine("ДИАГНОСТИКА ЦЕЛОСТНОСТИ ФАЙЛОВ");
            result.AppendLine("========================================");
            result.AppendLine();

            result.AppendLine("Режим базы:");
            result.AppendLine(settings.ProviderMode == DatabaseProviderMode.SqlServer
                ? "SQL Server / корпоративный режим"
                : "SQLite / локальный режим");
            result.AppendLine();

            result.AppendLine("Папка файлов:");
            result.AppendLine(string.IsNullOrWhiteSpace(filesRootFolder)
                ? "Папка файлов не указана."
                : filesRootFolder);
            result.AppendLine();

            bool rootExists = !string.IsNullOrWhiteSpace(filesRootFolder) &&
                              Directory.Exists(filesRootFolder);

            result.AppendLine("Статус папки файлов:");
            result.AppendLine(rootExists ? "Папка найдена." : "Папка не найдена.");
            result.AppendLine();

            using var db = new AppDbContext();

            bool canConnect = await db.Database.CanConnectAsync();

            result.AppendLine("Подключение к базе:");
            result.AppendLine(canConnect ? "Подключение успешно." : "Подключение не удалось.");
            result.AppendLine();

            if (!canConnect)
            {
                result.AppendLine("Проверка остановлена: база данных недоступна.");
                return result.ToString();
            }

            int clientsCount = await db.Clients.CountAsync();
            int clientFilesCount = await db.ClientFiles.CountAsync();
            int invoicesCount = await db.Invoices.CountAsync();
            int invoiceItemsCount = await db.InvoiceItems.CountAsync();
            int invoiceDocumentsCount = await db.InvoiceDocuments.CountAsync();

            result.AppendLine("Контрольные количества:");
            result.AppendLine($"Клиенты: {clientsCount}");
            result.AppendLine($"Файлы клиентов / ClientFiles: {clientFilesCount}");
            result.AppendLine($"Счета / Invoices: {invoicesCount}");
            result.AppendLine($"Позиции счетов / InvoiceItems: {invoiceItemsCount}");
            result.AppendLine($"Документы счетов / InvoiceDocuments: {invoiceDocumentsCount}");
            result.AppendLine();

            var registeredFullPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var clientFiles = await db.ClientFiles
                .AsNoTracking()
                .ToListAsync();

            var invoiceDocuments = await db.InvoiceDocuments
                .AsNoTracking()
                .ToListAsync();

            var invoices = await db.Invoices
                .AsNoTracking()
                .ToListAsync();

            int totalProblems = 0;

            totalProblems += CheckRecordsWithFiles(
                result,
                "Проверка ClientFiles",
                clientFiles.Cast<object>().ToList(),
                new[] { "RelativePath", "FilePath", "Path" },
                registeredFullPaths,
                emptyPathIsProblem: true);

            totalProblems += CheckRecordsWithFiles(
                result,
                "Проверка InvoiceDocuments",
                invoiceDocuments.Cast<object>().ToList(),
                new[] { "RelativePath", "FilePath", "Path" },
                registeredFullPaths,
                emptyPathIsProblem: true);

            totalProblems += CheckRecordsWithFiles(
                result,
                "Проверка Invoices.DocumentRelativePath",
                invoices.Cast<object>().ToList(),
                new[] { "DocumentRelativePath" },
                registeredFullPaths,
                emptyPathIsProblem: false);

            totalProblems += CheckDiskFilesWithoutDatabaseLinks(
                result,
                filesRootFolder,
                registeredFullPaths);

            result.AppendLine();
            result.AppendLine("ИТОГ");
            result.AppendLine("========================================");

            if (totalProblems == 0)
            {
                result.AppendLine("Критических проблем с файлами не найдено.");
            }
            else
            {
                result.AppendLine($"Обнаружены замечания: {totalProblems}");
                result.AppendLine();
                result.AppendLine("Важно: часть замечаний может быть не критичной.");
                result.AppendLine("Например, файл может лежать на диске, но не быть зарегистрированным в базе после старых переносов.");
            }

            return result.ToString();
        }

        private static int CheckRecordsWithFiles(
            StringBuilder result,
            string title,
            List<object> records,
            string[] pathPropertyNames,
            HashSet<string> registeredFullPaths,
            bool emptyPathIsProblem)
        {
            result.AppendLine(title);
            result.AppendLine("----------------------------------------");

            int checkedRecords = 0;
            int emptyPathCount = 0;
            int missingFilesCount = 0;

            var examples = new List<string>();

            foreach (object record in records)
            {
                string savedPath = GetFirstNonEmptyValue(record, pathPropertyNames);

                if (string.IsNullOrWhiteSpace(savedPath))
                {
                    if (emptyPathIsProblem)
                    {
                        emptyPathCount++;

                        if (examples.Count < 20)
                        {
                            examples.Add(
                                "- Пустой путь: " + BuildRecordCaption(record));
                        }
                    }

                    continue;
                }

                checkedRecords++;

                string fullPath = ResolveFullPath(savedPath);

                if (!string.IsNullOrWhiteSpace(fullPath))
                {
                    registeredFullPaths.Add(NormalizeFullPath(fullPath));
                }

                if (string.IsNullOrWhiteSpace(fullPath) || !File.Exists(fullPath))
                {
                    missingFilesCount++;

                    if (examples.Count < 20)
                    {
                        examples.Add(
                            "- Файл не найден: " + BuildRecordCaption(record) + Environment.NewLine +
                            "  путь в базе: " + savedPath + Environment.NewLine +
                            "  полный путь: " + fullPath);
                    }
                }
            }

            result.AppendLine($"Всего записей: {records.Count}");
            result.AppendLine($"Записей с непустым путём: {checkedRecords}");
            result.AppendLine($"Пустых путей: {emptyPathCount}");
            result.AppendLine($"Файлов не найдено: {missingFilesCount}");

            if (examples.Count > 0)
            {
                result.AppendLine();
                result.AppendLine("Примеры замечаний:");
                foreach (string example in examples)
                {
                    result.AppendLine(example);
                }

                if (emptyPathCount + missingFilesCount > examples.Count)
                {
                    result.AppendLine($"...и ещё {emptyPathCount + missingFilesCount - examples.Count} замечаний.");
                }
            }

            result.AppendLine();

            return emptyPathCount + missingFilesCount;
        }

        private static int CheckDiskFilesWithoutDatabaseLinks(
            StringBuilder result,
            string filesRootFolder,
            HashSet<string> registeredFullPaths)
        {
            result.AppendLine("Проверка файлов на диске без привязки к базе");
            result.AppendLine("----------------------------------------");

            if (string.IsNullOrWhiteSpace(filesRootFolder))
            {
                result.AppendLine("Папка файлов не указана.");
                result.AppendLine();
                return 1;
            }

            if (!Directory.Exists(filesRootFolder))
            {
                result.AppendLine("Папка файлов не найдена.");
                result.AppendLine();
                return 1;
            }

            List<string> diskFiles;

            try
            {
                diskFiles = Directory
                    .EnumerateFiles(filesRootFolder, "*", SearchOption.AllDirectories)
                    .ToList();
            }
            catch (Exception ex)
            {
                result.AppendLine("Не удалось прочитать папку файлов:");
                result.AppendLine(ex.Message);
                result.AppendLine();
                return 1;
            }

            var unregisteredFiles = diskFiles
                .Where(file => !registeredFullPaths.Contains(NormalizeFullPath(file)))
                .ToList();

            result.AppendLine($"Файлов найдено на диске: {diskFiles.Count}");
            result.AppendLine($"Файлов без явной привязки к базе: {unregisteredFiles.Count}");

            if (unregisteredFiles.Count > 0)
            {
                result.AppendLine();
                result.AppendLine("Первые файлы без привязки:");

                foreach (string file in unregisteredFiles.Take(30))
                {
                    string relative = GetRelativePathSafe(filesRootFolder, file);
                    result.AppendLine("- " + relative);
                }

                if (unregisteredFiles.Count > 30)
                {
                    result.AppendLine($"...и ещё {unregisteredFiles.Count - 30} файлов.");
                }
            }

            result.AppendLine();

            return unregisteredFiles.Count;
        }

        private static string ResolveFullPath(string savedPath)
        {
            if (string.IsNullOrWhiteSpace(savedPath))
                return "";

            try
            {
                if (Path.IsPathRooted(savedPath))
                    return savedPath;

                return ClientFileStorageService.GetFullPath(savedPath);
            }
            catch
            {
                return savedPath;
            }
        }

        private static string NormalizeFullPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return "";

            try
            {
                return Path
                    .GetFullPath(path)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            catch
            {
                return path.Trim();
            }
        }

        private static string GetRelativePathSafe(string rootFolder, string filePath)
        {
            try
            {
                return Path.GetRelativePath(rootFolder, filePath);
            }
            catch
            {
                return filePath;
            }
        }

        private static string GetFirstNonEmptyValue(object source, string[] propertyNames)
        {
            foreach (string propertyName in propertyNames)
            {
                string value = GetValue(source, propertyName);

                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }

            return "";
        }

        private static string GetValue(object source, string propertyName)
        {
            if (source == null)
                return "";

            PropertyInfo? property = source
                .GetType()
                .GetProperty(
                    propertyName,
                    BindingFlags.Public |
                    BindingFlags.Instance |
                    BindingFlags.IgnoreCase);

            if (property == null)
                return "";

            object? value = property.GetValue(source);

            return value?.ToString() ?? "";
        }

        private static string BuildRecordCaption(object record)
        {
            string id = GetFirstNonEmptyValue(record, new[] { "Id", "ClientFileId", "InvoiceDocumentId", "InvoiceId" });
            string clientId = GetFirstNonEmptyValue(record, new[] { "ClientInfoId", "ClientId" });
            string fileName = GetFirstNonEmptyValue(record, new[] { "OriginalFileName", "FileName", "StoredFileName" });
            string documentType = GetFirstNonEmptyValue(record, new[] { "DocumentType", "Category", "FileCategory" });

            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(id))
                parts.Add("Id=" + id);

            if (!string.IsNullOrWhiteSpace(clientId))
                parts.Add("ClientId=" + clientId);

            if (!string.IsNullOrWhiteSpace(documentType))
                parts.Add("Тип=" + documentType);

            if (!string.IsNullOrWhiteSpace(fileName))
                parts.Add("Файл=" + fileName);

            if (parts.Count == 0)
                return record.GetType().Name;

            return string.Join(", ", parts);
        }
    }
}