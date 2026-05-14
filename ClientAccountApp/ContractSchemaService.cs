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

            if (SchemaHelper.IsSqlServerProvider(db))
            {
                db.Database.EnsureCreated();
                return;
            }

            // SQLite: базовая таблица
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

            // Базовые колонки (v1.0)
            SchemaHelper.EnsureColumnExistsSqlite(db, "ClientContracts", "OrganizationProfileId", "INTEGER NOT NULL DEFAULT 0");
            SchemaHelper.EnsureColumnExistsSqlite(db, "ClientContracts", "ClientInfoId", "INTEGER NOT NULL DEFAULT 0");
            SchemaHelper.EnsureColumnExistsSqlite(db, "ClientContracts", "Status", "TEXT NOT NULL DEFAULT 'Требует договора'");
            SchemaHelper.EnsureColumnExistsSqlite(db, "ClientContracts", "ContractNumber", "TEXT NOT NULL DEFAULT ''");
            SchemaHelper.EnsureColumnExistsSqlite(db, "ClientContracts", "DocumentRelativePath", "TEXT NOT NULL DEFAULT ''");
            SchemaHelper.EnsureColumnExistsSqlite(db, "ClientContracts", "GeneratedAt", "TEXT NULL");
            SchemaHelper.EnsureColumnExistsSqlite(db, "ClientContracts", "SignedAt", "TEXT NULL");
            SchemaHelper.EnsureColumnExistsSqlite(db, "ClientContracts", "CreatedAt", "TEXT NOT NULL DEFAULT ''");
            SchemaHelper.EnsureColumnExistsSqlite(db, "ClientContracts", "UpdatedAt", "TEXT NOT NULL DEFAULT ''");

            // Колонки мастера договоров (v1.2)
            SchemaHelper.EnsureColumnExistsSqlite(db, "ClientContracts", "ContractType", "TEXT NOT NULL DEFAULT 'services'");
            SchemaHelper.EnsureColumnExistsSqlite(db, "ClientContracts", "Subject", "TEXT NOT NULL DEFAULT ''");
            SchemaHelper.EnsureColumnExistsSqlite(db, "ClientContracts", "Amount", "REAL NOT NULL DEFAULT 0");
            SchemaHelper.EnsureColumnExistsSqlite(db, "ClientContracts", "VatMode", "TEXT NOT NULL DEFAULT 'Без НДС'");
            SchemaHelper.EnsureColumnExistsSqlite(db, "ClientContracts", "City", "TEXT NOT NULL DEFAULT ''");
            SchemaHelper.EnsureColumnExistsSqlite(db, "ClientContracts", "ValidFrom", "TEXT NULL");
            SchemaHelper.EnsureColumnExistsSqlite(db, "ClientContracts", "ValidTo", "TEXT NULL");
            SchemaHelper.EnsureColumnExistsSqlite(db, "ClientContracts", "Party1Json", "TEXT NOT NULL DEFAULT ''");
            SchemaHelper.EnsureColumnExistsSqlite(db, "ClientContracts", "Party2Json", "TEXT NOT NULL DEFAULT ''");

            db.Database.ExecuteSqlRaw(@"
CREATE UNIQUE INDEX IF NOT EXISTS IX_ClientContracts_Organization_Client
ON ClientContracts (OrganizationProfileId, ClientInfoId);");
        }
    }
}
