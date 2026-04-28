using Microsoft.EntityFrameworkCore;
using System;
using System.Data;

namespace ClientAccountApp
{
    public static class InvoiceDocumentSchemaService
    {
        public static void EnsureInvoiceDocumentTables()
        {
            using var db = new AppDbContext();

            db.Database.ExecuteSqlRaw(@"
CREATE TABLE IF NOT EXISTS InvoiceDocuments (
    Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    InvoiceId INTEGER NOT NULL,
    ClientInfoId INTEGER NOT NULL,
    OrganizationProfileId INTEGER NULL,
    DocumentType TEXT NOT NULL DEFAULT '',
    DocumentFormat TEXT NOT NULL DEFAULT '',
    OriginalFileName TEXT NOT NULL DEFAULT '',
    RelativePath TEXT NOT NULL DEFAULT '',
    FileSizeBytes INTEGER NOT NULL DEFAULT 0,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL
);");

            EnsureColumnExists(db, "InvoiceDocuments", "InvoiceId", "INTEGER NOT NULL DEFAULT 0");
            EnsureColumnExists(db, "InvoiceDocuments", "ClientInfoId", "INTEGER NOT NULL DEFAULT 0");
            EnsureColumnExists(db, "InvoiceDocuments", "OrganizationProfileId", "INTEGER NULL");
            EnsureColumnExists(db, "InvoiceDocuments", "DocumentType", "TEXT NOT NULL DEFAULT ''");
            EnsureColumnExists(db, "InvoiceDocuments", "DocumentFormat", "TEXT NOT NULL DEFAULT ''");
            EnsureColumnExists(db, "InvoiceDocuments", "OriginalFileName", "TEXT NOT NULL DEFAULT ''");
            EnsureColumnExists(db, "InvoiceDocuments", "RelativePath", "TEXT NOT NULL DEFAULT ''");
            EnsureColumnExists(db, "InvoiceDocuments", "FileSizeBytes", "INTEGER NOT NULL DEFAULT 0");
            EnsureColumnExists(db, "InvoiceDocuments", "CreatedAt", "TEXT NOT NULL DEFAULT ''");
            EnsureColumnExists(db, "InvoiceDocuments", "UpdatedAt", "TEXT NOT NULL DEFAULT ''");

            db.Database.ExecuteSqlRaw(@"
CREATE UNIQUE INDEX IF NOT EXISTS IX_InvoiceDocuments_Invoice_Type_Format
ON InvoiceDocuments (InvoiceId, DocumentType, DocumentFormat);");
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