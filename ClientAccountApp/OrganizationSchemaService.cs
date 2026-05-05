using Microsoft.EntityFrameworkCore;
using System;
using System.Data;

namespace ClientAccountApp
{
    public static class OrganizationSchemaService
    {
        public static void EnsureOrganizationTables()
        {
            using var db = new AppDbContext();

            // SQL Server:
            // Таблицы создаются через EF Core Database.EnsureCreated() в AppDbContext.
            // SQLite-команды CREATE TABLE IF NOT EXISTS / PRAGMA здесь не выполняем.
            if (SchemaHelper.IsSqlServerProvider(db))
            {
                db.Database.EnsureCreated();
                return;
            }

            // SQLite:
            // Оставляем старую проверенную логику.
            db.Database.ExecuteSqlRaw(@"
CREATE TABLE IF NOT EXISTS OrganizationProfiles (
    Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL DEFAULT '',
    ShortName TEXT NOT NULL DEFAULT '',
    Inn TEXT NOT NULL DEFAULT '',
    Kpp TEXT NOT NULL DEFAULT '',
    Ogrn TEXT NOT NULL DEFAULT '',
    LegalAddress TEXT NOT NULL DEFAULT '',
    DirectorName TEXT NOT NULL DEFAULT '',
    DirectorPosition TEXT NOT NULL DEFAULT 'Директор',
    BankName TEXT NOT NULL DEFAULT '',
    BankBic TEXT NOT NULL DEFAULT '',
    SettlementAccount TEXT NOT NULL DEFAULT '',
    CorrespondentAccount TEXT NOT NULL DEFAULT '',
    Email TEXT NOT NULL DEFAULT '',
    Phone TEXT NOT NULL DEFAULT '',
    LogoRelativePath TEXT NOT NULL DEFAULT '',
    IsActive INTEGER NOT NULL DEFAULT 1,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL
);");

            SchemaHelper.EnsureColumnExistsSqlite(db, "OrganizationProfiles", "ShortName", "TEXT NOT NULL DEFAULT ''");
            SchemaHelper.EnsureColumnExistsSqlite(db, "OrganizationProfiles", "Kpp", "TEXT NOT NULL DEFAULT ''");
            SchemaHelper.EnsureColumnExistsSqlite(db, "OrganizationProfiles", "Ogrn", "TEXT NOT NULL DEFAULT ''");
            SchemaHelper.EnsureColumnExistsSqlite(db, "OrganizationProfiles", "LegalAddress", "TEXT NOT NULL DEFAULT ''");
            SchemaHelper.EnsureColumnExistsSqlite(db, "OrganizationProfiles", "DirectorName", "TEXT NOT NULL DEFAULT ''");
            SchemaHelper.EnsureColumnExistsSqlite(db, "OrganizationProfiles", "DirectorPosition", "TEXT NOT NULL DEFAULT 'Директор'");
            SchemaHelper.EnsureColumnExistsSqlite(db, "OrganizationProfiles", "BankName", "TEXT NOT NULL DEFAULT ''");
            SchemaHelper.EnsureColumnExistsSqlite(db, "OrganizationProfiles", "BankBic", "TEXT NOT NULL DEFAULT ''");
            SchemaHelper.EnsureColumnExistsSqlite(db, "OrganizationProfiles", "SettlementAccount", "TEXT NOT NULL DEFAULT ''");
            SchemaHelper.EnsureColumnExistsSqlite(db, "OrganizationProfiles", "CorrespondentAccount", "TEXT NOT NULL DEFAULT ''");
            SchemaHelper.EnsureColumnExistsSqlite(db, "OrganizationProfiles", "Email", "TEXT NOT NULL DEFAULT ''");
            SchemaHelper.EnsureColumnExistsSqlite(db, "OrganizationProfiles", "Phone", "TEXT NOT NULL DEFAULT ''");
            SchemaHelper.EnsureColumnExistsSqlite(db, "OrganizationProfiles", "LogoRelativePath", "TEXT NOT NULL DEFAULT ''");
            SchemaHelper.EnsureColumnExistsSqlite(db, "OrganizationProfiles", "IsActive", "INTEGER NOT NULL DEFAULT 1");
            SchemaHelper.EnsureColumnExistsSqlite(db, "OrganizationProfiles", "CreatedAt", "TEXT NOT NULL DEFAULT ''");
            SchemaHelper.EnsureColumnExistsSqlite(db, "OrganizationProfiles", "UpdatedAt", "TEXT NOT NULL DEFAULT ''");
        }
    }
}