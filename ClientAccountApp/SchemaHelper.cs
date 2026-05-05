using Microsoft.EntityFrameworkCore;
using System;
using System.Data;

namespace ClientAccountApp
{
    /// <summary>
    /// Общие вспомогательные методы для сервисов инициализации схемы базы данных.
    /// Заменяет дублированные приватные методы в BillingSchemaService,
    /// ContractSchemaService, InvoiceDocumentSchemaService и OrganizationSchemaService.
    /// </summary>
    internal static class SchemaHelper
    {
        /// <summary>
        /// Возвращает true если текущий провайдер базы данных — SQL Server.
        /// </summary>
        public static bool IsSqlServerProvider(AppDbContext db)
        {
            string providerName = db.Database.ProviderName ?? "";

            return providerName.Contains(
                "SqlServer",
                StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Добавляет колонку в таблицу SQLite если её ещё нет.
        /// Использует PRAGMA table_info для проверки существования колонки.
        /// </summary>
        public static void EnsureColumnExistsSqlite(
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