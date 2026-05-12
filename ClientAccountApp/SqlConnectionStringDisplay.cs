using Microsoft.Data.SqlClient;

namespace ClientAccountApp
{
    /// <summary>
    /// Единое разбор строки подключения SQL Server для подписей в UI (без дублирования try/catch).
    /// </summary>
    public static class SqlConnectionStringDisplay
    {
        /// <summary>
        /// Пытается извлечь DataSource и InitialCatalog. При ошибке разбора возвращает false.
        /// </summary>
        public static bool TryParseEndpoints(string? connectionString, out string dataSource, out string initialCatalog)
        {
            dataSource = "";
            initialCatalog = "";
            if (string.IsNullOrWhiteSpace(connectionString))
                return false;

            try
            {
                var builder = new SqlConnectionStringBuilder(connectionString);
                dataSource = builder.DataSource ?? "";
                initialCatalog = builder.InitialCatalog ?? "";
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Имя базы для компактного текста (футер); null если разобрать не удалось или каталог пуст.</summary>
        public static string? TryGetInitialCatalogOrNull(string? connectionString)
        {
            if (!TryParseEndpoints(connectionString, out _, out var catalog))
                return null;
            return string.IsNullOrWhiteSpace(catalog) ? null : catalog;
        }
    }
}
