using Microsoft.EntityFrameworkCore;
using System;

namespace ClientAccountApp
{
    /// <summary>
    /// Единственное место инициализации базы данных.
    /// Вызывается один раз при запуске приложения в App.OnLaunched().
    ///
    /// Заменяет:
    ///   - AppDbContext.InitializeDatabase() (убран из конструктора)
    ///   - App.EnsureDatabaseSchema()
    ///   - Вызовы EnsureXxxTables() разбросанные по 15+ местам
    /// </summary>
    public static class DatabaseInitializer
    {
        private static bool _initialized = false;
        private static readonly object _lock = new object();

        public static void Initialize()
        {
            lock (_lock)
            {
                if (_initialized)
                    return;

                try
                {
                    RunInitialization();
                    _initialized = true;
                    AppLogger.LogWarning("DatabaseInitializer", "Инициализация базы завершена успешно");
                }
                catch (Exception ex)
                {
                    AppLogger.LogError("DatabaseInitializer.Initialize", ex);
                    throw;
                }
            }
        }

        private static void RunInitialization()
        {
            using var db = new AppDbContext();

            // Шаг 1: EF Core создаёт схему (таблицы из OnModelCreating)
            db.Database.EnsureCreated();

            bool isSqlite = !SchemaHelper.IsSqlServerProvider(db);

            if (isSqlite)
            {
                // Шаг 2: патчи колонок для SQLite
                // (SQL Server получает правильную схему через EnsureCreated)
                EnsureClientStatusColumn(db);
                EnsureBankAccountCorrespondentAccountColumn(db);
                EnsureClientContractColumns(db);
                EnsureOgrnColumn(db);
                EnsureNoteReminderDateColumn(db);
            }

            // Шаг 3: создание таблиц через Schema-сервисы
            // Эти методы используют CREATE TABLE IF NOT EXISTS — безопасно вызывать повторно
            OrganizationSchemaService.EnsureOrganizationTables();
            ContractSchemaService.EnsureContractTables();
            BillingSchemaService.EnsureBillingTables();
            InvoiceDocumentSchemaService.EnsureInvoiceDocumentTables();
        }

        // ─── Патчи колонок SQLite ────────────────────────────────────────────────
        // Добавляют колонки к существующим таблицам при обновлении приложения.
        // Используют PRAGMA table_info чтобы не падать если колонка уже есть.

        private static void EnsureClientStatusColumn(AppDbContext db)
        {
            SchemaHelper.EnsureColumnExistsSqlite(
                db, "Clients", "Status", "TEXT NOT NULL DEFAULT 'Активный'");
        }

        private static void EnsureBankAccountCorrespondentAccountColumn(AppDbContext db)
        {
            SchemaHelper.EnsureColumnExistsSqlite(
                db, "BankAccounts", "CorrespondentAccount", "TEXT NOT NULL DEFAULT ''");
        }

        private static void EnsureClientContractColumns(AppDbContext db)
        {
            SchemaHelper.EnsureColumnExistsSqlite(
                db, "Clients", "ContractStatus", "TEXT NOT NULL DEFAULT 'Требует договора'");
            SchemaHelper.EnsureColumnExistsSqlite(
                db, "Clients", "ContractGeneratedAt", "TEXT NULL");
            SchemaHelper.EnsureColumnExistsSqlite(
                db, "Clients", "ContractSignedAt", "TEXT NULL");
        }

        private static void EnsureOgrnColumn(AppDbContext db)
        {
            SchemaHelper.EnsureColumnExistsSqlite(
                db, "Clients", "Ogrn", "TEXT NOT NULL DEFAULT ''");
        }

        private static void EnsureNoteReminderDateColumn(AppDbContext db)
        {
            SchemaHelper.EnsureColumnExistsSqlite(
                db, "ClientNotes", "ReminderDate", "TEXT NULL");
        }
    }
}