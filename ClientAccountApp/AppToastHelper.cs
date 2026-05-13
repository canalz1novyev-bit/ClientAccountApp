using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using System;

namespace ClientAccountApp
{
    // Тонкая обёртка вокруг AppNotificationManager, чтобы любая страница могла
    // одной строкой сообщить пользователю «операция завершена», не возясь с билдером.
    // Все вызовы безопасны: ошибки регистрации/отображения тостов проглатываются и логируются.
    public static class AppToastHelper
    {
        public static void Show(string title, string? message = null, string? extra = null)
        {
            try
            {
                var builder = new AppNotificationBuilder().AddText(title);

                if (!string.IsNullOrWhiteSpace(message))
                    builder.AddText(message);

                if (!string.IsNullOrWhiteSpace(extra))
                    builder.AddText(extra);

                AppNotificationManager.Default.Show(builder.BuildNotification());
            }
            catch (Exception ex)
            {
                AppLogger.LogError("AppToastHelper.Show", ex);
            }
        }

        public static void Success(string title, string? message = null) =>
            Show(title, message);

        public static void Error(string title, string? message = null) =>
            Show("Ошибка: " + title, message);
    }
}
