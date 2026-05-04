using Microsoft.EntityFrameworkCore;
using System;
using System.Data;

namespace ClientAccountApp
{
    public static class ContractSchemaService
    {
        public static void EnsureContractTables()
        {
            using var db = new AppDbContext();

            // SQL Server:
            // Таблицы создаются через EF Core Database.EnsureCreated() в AppDbContext.
            // SQLite-команды CREATE TABLE IF NOT EXISTS / PRAGMA / AUTOINCREMENT здесь не выполняем.
            if (IsSqlServerProvider(db))
            {
                db.Database.EnsureCreated();
                return;
            }

            // SQLite:
            // Оставляем старую проверенную логику.
            db.Database.ExecuteSqlRaw(@"
CREATE TABLE IF NOT EXISTS ClientContracts (
    Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    OrganizationProfileId INTEGER NOT NULL,
    ClientInfoId INTEGER NOT NULL,
    Status TEXT NOT NULL DEFAULT 'Требует договора',
    ContractNumber TEXT NOT NULL DEFAULT '',
    DocumentRelativePath TEXT NOT NULL DEFAULT '',
    GeneratedAt TEXT NULL,
    SignedAt TEXT NULL,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL
);");

            EnsureColumnExistsSqlite(db, "ClientContracts", "OrganizationProfileId", "INTEGER NOT NULL DEFAULT 0");
            EnsureColumnExistsSqlite(db, "ClientContracts", "ClientInfoId", "INTEGER NOT NULL DEFAULT 0");
            EnsureColumnExistsSqlite(db, "ClientContracts", "Status", "TEXT NOT NULL DEFAULT 'Требует договора'");
            EnsureColumnExistsSqlite(db, "ClientContracts", "ContractNumber", "TEXT NOT NULL DEFAULT ''");
            EnsureColumnExistsSqlite(db, "ClientContracts", "DocumentRelativePath", "TEXT NOT NULL DEFAULT ''");
            EnsureColumnExistsSqlite(db, "ClientContracts", "GeneratedAt", "TEXT NULL");
            EnsureColumnExistsSqlite(db, "ClientContracts", "SignedAt", "TEXT NULL");
            EnsureColumnExistsSqlite(db, "ClientContracts", "CreatedAt", "TEXT NOT NULL DEFAULT ''");
            EnsureColumnExistsSqlite(db, "ClientContracts", "UpdatedAt", "TEXT NOT NULL DEFAULT ''");

            db.Database.ExecuteSqlRaw(@"
CREATE UNIQUE INDEX IF NOT EXISTS IX_ClientContracts_Organization_Client
ON ClientContracts (OrganizationProfileId, ClientInfoId);");
        }

        private static bool IsSqlServerProvider(AppDbContext db)
        {
            string providerName = db.Database.ProviderName ?? "";

            return providerName.Contains(
                "SqlServer",
                StringComparison.OrdinalIgnoreCase);
        }

        private static void EnsureColumnExistsSqlite(
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
                string existingColumnName = reader["name"]?.ToString() ?? "";

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