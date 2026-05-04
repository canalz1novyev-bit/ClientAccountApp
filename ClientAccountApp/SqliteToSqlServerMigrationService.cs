using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ClientAccountApp
{
    public sealed class SqliteToSqlServerMigrationResult
    {
        public int FilesCopied { get; set; }

        public Dictionary<string, int> TableRows { get; } = new();

        public override string ToString()
        {
            var builder = new StringBuilder();

            builder.AppendLine("Перенос локальной SQLite-базы в SQL Server завершён.");
            builder.AppendLine();

            foreach (var item in TableRows)
            {
                builder.AppendLine($"{item.Key}: {item.Value}");
            }

            builder.AppendLine();
            builder.AppendLine($"Файлов скопировано: {FilesCopied}");

            return builder.ToString();
        }
    }

    public static class SqliteToSqlServerMigrationService
    {
        public static SqliteToSqlServerMigrationResult MigrateLocalSqliteToSqlServer()
        {
            var settings = DatabaseConnectionSettingsService.Load();

            if (settings.ProviderMode != DatabaseProviderMode.SqlServer)
                throw new InvalidOperationException("Сначала включите серверный режим SQL Server в настройках корпоративной базы.");

            if (string.IsNullOrWhiteSpace(settings.SqlServerConnectionString))
                throw new InvalidOperationException("Не указана строка подключения к SQL Server.");

            if (string.IsNullOrWhiteSpace(settings.SharedClientFilesFolder))
                throw new InvalidOperationException("Не указана общая папка файлов клиентов.");

            if (!File.Exists(AppPaths.DatabasePath))
                throw new FileNotFoundException("Локальная SQLite-база не найдена.", AppPaths.DatabasePath);

            Directory.CreateDirectory(settings.SharedClientFilesFolder);

            var sqliteOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={AppPaths.DatabasePath}")
                .Options;

            var sqlServerOptions = new DbContextOptionsBuilder<AppDbContext>()
    .UseSqlServer(settings.SqlServerConnectionString)
    .Options;

            using var sourceDb = new AppDbContext(sqliteOptions);
            using var targetDb = new AppDbContext(sqlServerOptions);

            targetDb.Database.EnsureCreated();

            if (TargetHasAnyData(targetDb))
            {
                throw new InvalidOperationException(
                    "Серверная база SQL Server не пустая. " +
                    "Чтобы избежать смешивания данных, сначала очистите тестовую SQL-базу или создайте новую пустую базу.");
            }

            var result = new SqliteToSqlServerMigrationResult();

            using var transaction = targetDb.Database.BeginTransaction();

            CopyTableWithIdentity(
                targetDb,
                "OrganizationProfiles",
                sourceDb.OrganizationProfiles.AsNoTracking().ToList(),
                result);

            CopyTableWithIdentity(
                targetDb,
                "Clients",
                sourceDb.Clients.AsNoTracking().ToList(),
                result);

            CopyTableWithIdentity(
                targetDb,
                "ServicesCatalog",
                sourceDb.ServicesCatalog.AsNoTracking().ToList(),
                result);

            var digitalSignatures = sourceDb.DigitalSignatures
    .AsNoTracking()
    .Select(s => new DigitalSignature
    {
        Id = s.Id,
        ClientInfoId = s.ClientInfoId,
        CertificationAuthority = s.CertificationAuthority,
        Comment = s.Comment,
        IssuedDate = s.IssuedDate,
        ExpiresDate = s.ExpiresDate
    })
    .ToList();

            CopyTableWithIdentity(
                targetDb,
                "DigitalSignatures",
                digitalSignatures,
                result);

            var bankAccounts = sourceDb.BankAccounts
    .AsNoTracking()
    .Select(b => new BankAccount
    {
        Id = b.Id,
        ClientInfoId = b.ClientInfoId,
        BankName = b.BankName,
        BIC = b.BIC,
        CorrespondentAccount = b.CorrespondentAccount,
        AccountNumber = b.AccountNumber,
        Comment = b.Comment
    })
    .ToList();

            var clientNotes = sourceDb.ClientNotes
    .AsNoTracking()
    .Select(n => new ClientNote
    {
        Id = n.Id,
        ClientInfoId = n.ClientInfoId,
        NoteText = n.NoteText,
        CreatedAt = n.CreatedAt
    })
    .ToList();

            CopyTableWithIdentity(
                targetDb,
                "ClientNotes",
                clientNotes,
                result);


            var clientFiles = sourceDb.ClientFiles
                .AsNoTracking()
                .Select(f => new ClientFile
                {
                    Id = f.Id,
                    ClientInfoId = f.ClientInfoId,
                    OriginalFileName = f.OriginalFileName,
                    RelativePath = f.RelativePath,
                    FileSizeBytes = f.FileSizeBytes,
                    AddedAt = f.AddedAt,
                    Category = f.Category
                })
                .ToList();

            CopyTableWithIdentity(
                targetDb,
                "ClientFiles",
                clientFiles,
                result);


            var recurringServices = sourceDb.ClientRecurringServices
                .AsNoTracking()
                .Select(s => new ClientRecurringService
                {
                    Id = s.Id,
                    ClientInfoId = s.ClientInfoId,
                    ServiceCatalogId = s.ServiceCatalogId,
                    ServiceName = s.ServiceName,
                    Unit = s.Unit,
                    UnitPrice = s.UnitPrice,
                    Quantity = s.Quantity,
                    VatRate = s.VatRate,
                    BillingCycle = s.BillingCycle,
                    IsAdvanceBilling = s.IsAdvanceBilling,
                    GenerateDay = s.GenerateDay,
                    StartDate = s.StartDate,
                    EndDate = s.EndDate,
                    IsActive = s.IsActive,
                    Comment = s.Comment,
                    LastGeneratedPeriodKey = s.LastGeneratedPeriodKey
                })
                .ToList();

            CopyTableWithIdentity(
                targetDb,
                "ClientRecurringServices",
                recurringServices,
                result);


            var clientContracts = sourceDb.ClientContracts
                .AsNoTracking()
                .Select(c => new ClientContract
                {
                    Id = c.Id,
                    OrganizationProfileId = c.OrganizationProfileId,
                    ClientInfoId = c.ClientInfoId,
                    Status = c.Status,
                    ContractNumber = c.ContractNumber,
                    DocumentRelativePath = c.DocumentRelativePath,
                    GeneratedAt = c.GeneratedAt,
                    SignedAt = c.SignedAt,
                    CreatedAt = c.CreatedAt,
                    UpdatedAt = c.UpdatedAt
                })
                .ToList();

            CopyTableWithIdentity(
                targetDb,
                "ClientContracts",
                clientContracts,
                result);


            var invoices = sourceDb.Invoices
                .AsNoTracking()
                .Select(i => new Invoice
                {
                    Id = i.Id,
                    OrganizationProfileId = i.OrganizationProfileId,
                    ClientInfoId = i.ClientInfoId,
                    InvoiceNumber = i.InvoiceNumber,
                    InvoiceDate = i.InvoiceDate,
                    DueDate = i.DueDate,
                    PeriodFrom = i.PeriodFrom,
                    PeriodTo = i.PeriodTo,
                    PeriodText = i.PeriodText,
                    Status = i.Status,
                    SourceType = i.SourceType,
                    TotalWithoutVat = i.TotalWithoutVat,
                    VatAmount = i.VatAmount,
                    TotalWithVat = i.TotalWithVat,
                    Comment = i.Comment,
                    DocumentRelativePath = i.DocumentRelativePath,
                    CreatedAt = i.CreatedAt,
                    UpdatedAt = i.UpdatedAt
                })
                .ToList();

            CopyTableWithIdentity(
                targetDb,
                "Invoices",
                invoices,
                result);


            var invoiceItems = sourceDb.InvoiceItems
                .AsNoTracking()
                .Select(i => new InvoiceItem
                {
                    Id = i.Id,
                    InvoiceId = i.InvoiceId,
                    ServiceCatalogId = i.ServiceCatalogId,
                    ServiceName = i.ServiceName,
                    Quantity = i.Quantity,
                    Unit = i.Unit,
                    UnitPrice = i.UnitPrice,
                    VatRate = i.VatRate,
                    AmountWithoutVat = i.AmountWithoutVat,
                    VatAmount = i.VatAmount,
                    AmountWithVat = i.AmountWithVat,
                    SortOrder = i.SortOrder
                })
                .ToList();

            CopyTableWithIdentity(
                targetDb,
                "InvoiceItems",
                invoiceItems,
                result);


            var validInvoiceIds = sourceDb.Invoices
    .AsNoTracking()
    .Select(i => i.Id)
    .ToHashSet();

            var validClientIds = sourceDb.Clients
                .AsNoTracking()
                .Select(c => c.Id)
                .ToHashSet();

            var validOrganizationIds = sourceDb.OrganizationProfiles
                .AsNoTracking()
                .Select(o => o.Id)
                .ToHashSet();

            int totalInvoiceDocuments = sourceDb.InvoiceDocuments
                .AsNoTracking()
                .Count();

            var invoiceDocuments = sourceDb.InvoiceDocuments
                .AsNoTracking()
                .Where(d => validInvoiceIds.Contains(d.InvoiceId))
                .Where(d => validClientIds.Contains(d.ClientInfoId))
                .Select(d => new InvoiceDocument
                {
                    Id = d.Id,
                    InvoiceId = d.InvoiceId,
                    ClientInfoId = d.ClientInfoId,

                    OrganizationProfileId =
                        d.OrganizationProfileId.HasValue &&
                        validOrganizationIds.Contains(d.OrganizationProfileId.Value)
                            ? d.OrganizationProfileId
                            : null,

                    DocumentType = d.DocumentType,
                    DocumentFormat = d.DocumentFormat,
                    OriginalFileName = d.OriginalFileName,
                    RelativePath = d.RelativePath,
                    FileSizeBytes = d.FileSizeBytes,
                    CreatedAt = d.CreatedAt,
                    UpdatedAt = d.UpdatedAt
                })
                .ToList();

            int skippedInvoiceDocuments = totalInvoiceDocuments - invoiceDocuments.Count;

            CopyTableWithIdentity(
                targetDb,
                "InvoiceDocuments",
                invoiceDocuments,
                result);

            if (skippedInvoiceDocuments > 0)
            {
                result.TableRows["InvoiceDocuments пропущено"] = skippedInvoiceDocuments;
            }

            transaction.Commit();

            result.FilesCopied = CopyClientFilesToSharedFolder(
                AppPaths.ClientFilesFolder,
                settings.SharedClientFilesFolder);

            return result;
        }

        private static bool TargetHasAnyData(AppDbContext db)
        {
            return
                db.OrganizationProfiles.Any() ||
                db.Clients.Any() ||
                db.ServicesCatalog.Any() ||
                db.DigitalSignatures.Any() ||
                db.BankAccounts.Any() ||
                db.ClientNotes.Any() ||
                db.ClientFiles.Any() ||
                db.ClientRecurringServices.Any() ||
                db.ClientContracts.Any() ||
                db.Invoices.Any() ||
                db.InvoiceItems.Any() ||
                db.InvoiceDocuments.Any();
        }

        private static void CopyTableWithIdentity<T>(
            AppDbContext targetDb,
            string tableName,
            List<T> rows,
            SqliteToSqlServerMigrationResult result)
            where T : class
        {
            result.TableRows[tableName] = rows.Count;

            if (rows.Count == 0)
                return;

            bool identityInsertEnabled = false;

            try
            {
                targetDb.Database.ExecuteSqlRaw($"SET IDENTITY_INSERT [dbo].[{tableName}] ON;");
                identityInsertEnabled = true;

                targetDb.Set<T>().AddRange(rows);
                targetDb.SaveChanges();
                targetDb.ChangeTracker.Clear();
            }
            finally
            {
                if (identityInsertEnabled)
                {
                    targetDb.Database.ExecuteSqlRaw($"SET IDENTITY_INSERT [dbo].[{tableName}] OFF;");
                }
            }
        }

        private static int CopyClientFilesToSharedFolder(string sourceRoot, string targetRoot)
        {
            if (string.IsNullOrWhiteSpace(sourceRoot) || !Directory.Exists(sourceRoot))
                return 0;

            Directory.CreateDirectory(targetRoot);

            string fullSourceRoot = Path.GetFullPath(sourceRoot);
            string fullTargetRoot = Path.GetFullPath(targetRoot);

            if (string.Equals(fullSourceRoot, fullTargetRoot, StringComparison.OrdinalIgnoreCase))
                return 0;

            int copied = 0;

            foreach (var sourceFile in Directory.EnumerateFiles(fullSourceRoot, "*", SearchOption.AllDirectories))
            {
                string relativePath = Path.GetRelativePath(fullSourceRoot, sourceFile);
                string targetFile = Path.Combine(fullTargetRoot, relativePath);

                string? targetFolder = Path.GetDirectoryName(targetFile);

                if (!string.IsNullOrWhiteSpace(targetFolder))
                    Directory.CreateDirectory(targetFolder);

                if (!File.Exists(targetFile))
                {
                    File.Copy(sourceFile, targetFile, overwrite: false);
                    copied++;
                }
            }

            return copied;
        }
    }
}