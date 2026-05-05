using System;
using System.IO;

namespace ClientAccountApp
{
    /// <summary>
    /// Простой логгер для записи ошибок из catch-блоков.
    /// Пишет в файл рядом со startup-log.txt.
    /// </summary>
    public static class AppLogger
    {
        private static readonly string LogFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ClientAccountApp",
            "Logs");

        private static readonly string LogPath = Path.Combine(LogFolder, "errors.log");

        /// <summary>
        /// Записывает ошибку в лог. Никогда не бросает исключений.
        /// </summary>
        public static void LogError(string context, Exception ex)
        {
            try
            {
                Directory.CreateDirectory(LogFolder);

                string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | ERROR | {context}: {ex.GetType().Name}: {ex.Message}";

                File.AppendAllText(LogPath, line + Environment.NewLine);
            }
            catch
            {
                // Логирование не должно ломать приложение
            }
        }

        /// <summary>
        /// Записывает предупреждение в лог. Никогда не бросает исключений.
        /// </summary>
        public static void LogWarning(string context, string message)
        {
            try
            {
                Directory.CreateDirectory(LogFolder);

                string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | WARN  | {context}: {message}";

                File.AppendAllText(LogPath, line + Environment.NewLine);
            }
            catch
            {
                // Логирование не должно ломать приложение
            }
        }
    }
}