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

            SchemaHelper.EnsureColumnExistsSqlite(db, "InvoiceDocuments", "InvoiceId", "INTEGER NOT NULL DEFAULT 0");
            SchemaHelper.EnsureColumnExistsSqlite(db, "InvoiceDocuments", "ClientInfoId", "INTEGER NOT NULL DEFAULT 0");
            SchemaHelper.EnsureColumnExistsSqlite(db, "InvoiceDocuments", "OrganizationProfileId", "INTEGER NULL");
            SchemaHelper.EnsureColumnExistsSqlite(db, "InvoiceDocuments", "DocumentType", "TEXT NOT NULL DEFAULT ''");
            SchemaHelper.EnsureColumnExistsSqlite(db, "InvoiceDocuments", "DocumentFormat", "TEXT NOT NULL DEFAULT ''");
            SchemaHelper.EnsureColumnExistsSqlite(db, "InvoiceDocuments", "OriginalFileName", "TEXT NOT NULL DEFAULT ''");
            SchemaHelper.EnsureColumnExistsSqlite(db, "InvoiceDocuments", "RelativePath", "TEXT NOT NULL DEFAULT ''");
            SchemaHelper.EnsureColumnExistsSqlite(db, "InvoiceDocuments", "FileSizeBytes", "INTEGER NOT NULL DEFAULT 0");
            SchemaHelper.EnsureColumnExistsSqlite(db, "InvoiceDocuments", "CreatedAt", "TEXT NOT NULL DEFAULT ''");
            SchemaHelper.EnsureColumnExistsSqlite(db, "InvoiceDocuments", "UpdatedAt", "TEXT NOT NULL DEFAULT ''");

            db.Database.ExecuteSqlRaw(@"
CREATE UNIQUE INDEX IF NOT EXISTS IX_InvoiceDocuments_Invoice_Type_Format
ON InvoiceDocuments (InvoiceId, DocumentType, DocumentFormat);");
        }
    }
}