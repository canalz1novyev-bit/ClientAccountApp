using Microsoft.EntityFrameworkCore;
using System;
using System.Data;

namespace ClientAccountApp
{
    public static class BillingSchemaService
    {
        public static void EnsureBillingTables()
        {
            using var db = new AppDbContext();

            db.Database.ExecuteSqlRaw(@"
CREATE TABLE IF NOT EXISTS Invoices (
    Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
OrganizationProfileId INTEGER NULL,
    ClientInfoId INTEGER NOT NULL,
    InvoiceNumber TEXT NOT NULL,
    InvoiceDate TEXT NOT NULL,
    DueDate TEXT NULL,
    PeriodFrom TEXT NULL,
    PeriodTo TEXT NULL,
    PeriodText TEXT NOT NULL DEFAULT '',
    Status TEXT NOT NULL DEFAULT 'Черновик',
    SourceType TEXT NOT NULL DEFAULT 'Ручной',
    TotalWithoutVat REAL NOT NULL DEFAULT 0,
    VatAmount REAL NOT NULL DEFAULT 0,
    TotalWithVat REAL NOT NULL DEFAULT 0,
    Comment TEXT NOT NULL DEFAULT '',
    DocumentRelativePath TEXT NOT NULL DEFAULT '',
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL
);");

            db.Database.ExecuteSqlRaw(@"
CREATE TABLE IF NOT EXISTS InvoiceItems (
    Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    InvoiceId INTEGER NOT NULL,
    ServiceCatalogId INTEGER NULL,
    ServiceName TEXT NOT NULL,
    Quantity REAL NOT NULL DEFAULT 1,
    Unit TEXT NOT NULL DEFAULT 'усл.',
    UnitPrice REAL NOT NULL DEFAULT 0,
    VatRate REAL NOT NULL DEFAULT 0,
    AmountWithoutVat REAL NOT NULL DEFAULT 0,
    VatAmount REAL NOT NULL DEFAULT 0,
    AmountWithVat REAL NOT NULL DEFAULT 0,
    SortOrder INTEGER NOT NULL DEFAULT 0
);");

            db.Database.ExecuteSqlRaw(@"
CREATE TABLE IF NOT EXISTS ServicesCatalog (
    Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL,
    DefaultPrice REAL NOT NULL DEFAULT 0,
    Unit TEXT NOT NULL DEFAULT 'усл.',
    DefaultVatRate REAL NOT NULL DEFAULT 0,
    IsActive INTEGER NOT NULL DEFAULT 1,
    SortOrder INTEGER NOT NULL DEFAULT 0,
    Comment TEXT NOT NULL DEFAULT '',
    CreatedAt TEXT NOT NULL
);");

            db.Database.ExecuteSqlRaw(@"
CREATE TABLE IF NOT EXISTS ClientRecurringServices (
    Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    ClientInfoId INTEGER NOT NULL,
    ServiceCatalogId INTEGER NULL,
    ServiceName TEXT NOT NULL DEFAULT '',
    Unit TEXT NOT NULL DEFAULT 'усл.',
    UnitPrice REAL NOT NULL DEFAULT 0,
    Quantity REAL NOT NULL DEFAULT 1,
    VatRate REAL NOT NULL DEFAULT 0,
    BillingCycle TEXT NOT NULL DEFAULT 'Ежемесячно',
    IsAdvanceBilling INTEGER NOT NULL DEFAULT 1,
    GenerateDay INTEGER NOT NULL DEFAULT 1,
    StartDate TEXT NOT NULL,
    EndDate TEXT NULL,
    IsActive INTEGER NOT NULL DEFAULT 1,
    Comment TEXT NOT NULL DEFAULT '',
    LastGeneratedPeriodKey TEXT NOT NULL DEFAULT ''
);");
            EnsureColumnExists(db, "Invoices", "OrganizationProfileId", "INTEGER NULL");
            EnsureColumnExists(db, "InvoiceItems", "ServiceCatalogId", "INTEGER NULL");
            EnsureColumnExists(db, "InvoiceItems", "SortOrder", "INTEGER NOT NULL DEFAULT 0");

            EnsureColumnExists(db, "ServicesCatalog", "DefaultVatRate", "REAL NOT NULL DEFAULT 0");
            EnsureColumnExists(db, "ServicesCatalog", "SortOrder", "INTEGER NOT NULL DEFAULT 0");
            EnsureColumnExists(db, "ServicesCatalog", "Comment", "TEXT NOT NULL DEFAULT ''");
            EnsureColumnExists(db, "ServicesCatalog", "CreatedAt", "TEXT NOT NULL DEFAULT ''");

            EnsureColumnExists(db, "ClientRecurringServices", "ServiceName", "TEXT NOT NULL DEFAULT ''");
            EnsureColumnExists(db, "ClientRecurringServices", "Unit", "TEXT NOT NULL DEFAULT 'усл.'");
            EnsureColumnExists(db, "ClientRecurringServices", "UnitPrice", "REAL NOT NULL DEFAULT 0");
            EnsureColumnExists(db, "ClientRecurringServices", "VatRate", "REAL NOT NULL DEFAULT 0");
            EnsureColumnExists(db, "ClientRecurringServices", "IsAdvanceBilling", "INTEGER NOT NULL DEFAULT 1");
            EnsureColumnExists(db, "ClientRecurringServices", "GenerateDay", "INTEGER NOT NULL DEFAULT 1");
            EnsureColumnExists(db, "ClientRecurringServices", "Comment", "TEXT NOT NULL DEFAULT ''");
            EnsureColumnExists(db, "ClientRecurringServices", "LastGeneratedPeriodKey", "TEXT NOT NULL DEFAULT ''");
        }

        private static void EnsureColumnExists(
            AppDbContext db,
            string tableName,
            string columnName,
            string columnDefinition)
        {
            var connection = db.Database.GetDbConnection();

            if (connection.State != ConnectionState.Open)
                connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA table_info({tableName});";

            using var reader = command.ExecuteReader();

            bool exists = false;

            while (reader.Read())
            {
                var existingColumnName = reader["name"]?.ToString();

                if (string.Equals(existingColumnName, columnName, StringComparison.OrdinalIgnoreCase))
                {
                    exists = true;
                    break;
                }
            }

            reader.Close();

            if (!exists)
            {
                db.Database.ExecuteSqlRaw(
                    $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition};");
            }
        }
    }
}