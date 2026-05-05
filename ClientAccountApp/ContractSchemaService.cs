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
            if (SchemaHelper.IsSqlServerProvider(db))
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

            SchemaHelper.EnsureColumnExistsSqlite(db, "ClientContracts", "OrganizationProfileId", "INTEGER NOT NULL DEFAULT 0");
            SchemaHelper.EnsureColumnExistsSqlite(db, "ClientContracts", "ClientInfoId", "INTEGER NOT NULL DEFAULT 0");
            SchemaHelper.EnsureColumnExistsSqlite(db, "ClientContracts", "Status", "TEXT NOT NULL DEFAULT 'Требует договора'");
            SchemaHelper.EnsureColumnExistsSqlite(db, "ClientContracts", "ContractNumber", "TEXT NOT NULL DEFAULT ''");
            SchemaHelper.EnsureColumnExistsSqlite(db, "ClientContracts", "DocumentRelativePath", "TEXT NOT NULL DEFAULT ''");
            SchemaHelper.EnsureColumnExistsSqlite(db, "ClientContracts", "GeneratedAt", "TEXT NULL");
            SchemaHelper.EnsureColumnExistsSqlite(db, "ClientContracts", "SignedAt", "TEXT NULL");
            SchemaHelper.EnsureColumnExistsSqlite(db, "ClientContracts", "CreatedAt", "TEXT NOT NULL DEFAULT ''");
            SchemaHelper.EnsureColumnExistsSqlite(db, "ClientContracts", "UpdatedAt", "TEXT NOT NULL DEFAULT ''");

            db.Database.ExecuteSqlRaw(@"
CREATE UNIQUE INDEX IF NOT EXISTS IX_ClientContracts_Organization_Client
ON ClientContracts (OrganizationProfileId, ClientInfoId);");
        }
    }
}