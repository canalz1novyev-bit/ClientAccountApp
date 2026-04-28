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

            EnsureColumnExists(db, "OrganizationProfiles", "ShortName", "TEXT NOT NULL DEFAULT ''");
            EnsureColumnExists(db, "OrganizationProfiles", "Kpp", "TEXT NOT NULL DEFAULT ''");
            EnsureColumnExists(db, "OrganizationProfiles", "Ogrn", "TEXT NOT NULL DEFAULT ''");
            EnsureColumnExists(db, "OrganizationProfiles", "LegalAddress", "TEXT NOT NULL DEFAULT ''");
            EnsureColumnExists(db, "OrganizationProfiles", "DirectorName", "TEXT NOT NULL DEFAULT ''");
            EnsureColumnExists(db, "OrganizationProfiles", "DirectorPosition", "TEXT NOT NULL DEFAULT 'Директор'");
            EnsureColumnExists(db, "OrganizationProfiles", "BankName", "TEXT NOT NULL DEFAULT ''");
            EnsureColumnExists(db, "OrganizationProfiles", "BankBic", "TEXT NOT NULL DEFAULT ''");
            EnsureColumnExists(db, "OrganizationProfiles", "SettlementAccount", "TEXT NOT NULL DEFAULT ''");
            EnsureColumnExists(db, "OrganizationProfiles", "CorrespondentAccount", "TEXT NOT NULL DEFAULT ''");
            EnsureColumnExists(db, "OrganizationProfiles", "Email", "TEXT NOT NULL DEFAULT ''");
            EnsureColumnExists(db, "OrganizationProfiles", "Phone", "TEXT NOT NULL DEFAULT ''");
            EnsureColumnExists(db, "OrganizationProfiles", "LogoRelativePath", "TEXT NOT NULL DEFAULT ''");
            EnsureColumnExists(db, "OrganizationProfiles", "IsActive", "INTEGER NOT NULL DEFAULT 1");
            EnsureColumnExists(db, "OrganizationProfiles", "CreatedAt", "TEXT NOT NULL DEFAULT ''");
            EnsureColumnExists(db, "OrganizationProfiles", "UpdatedAt", "TEXT NOT NULL DEFAULT ''");
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