using Microsoft.EntityFrameworkCore;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using System;
using System.IO;
using System.Linq;

namespace ClientAccountApp
{
    public partial class App : Application
    {
        // Главное окно приложения. Используется в ToolsPage и LegacyWorkspacePage
        // для получения дескриптора окна при открытии файловых диалогов.
        public static Window? MainAppWindow { get; private set; }

        public App()
        {
            InitializeComponent();
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            WriteAppLog("OnLaunched start");

            // Регистрируем менеджер уведомлений как можно раньше — до создания окна.
            // Если регистрация упадёт, приложение всё равно запустится нормально.
            RegisterNotifications();

            try
            {
                WriteAppLog("Before ShellWindow");

                MainAppWindow = new ShellWindow();

                WriteAppLog("After ShellWindow");

                MainAppWindow.Activate();

                WriteAppLog("After Activate");
            }
            catch (Exception ex)
            {
                WriteAppLog("ShellWindow error: " + ex);
                throw;
            }

            // Показываем уведомления уже после того как окно открылось.
            // Небольшая задержка через DispatcherQueue чтобы не тормозить запуск.
            MainAppWindow?.DispatcherQueue.TryEnqueue(() =>
            {
                ShowStartupReminders();
            });
        }

        // ─── Уведомления ────────────────────────────────────────────────────────

        private static void RegisterNotifications()
        {
            try
            {
                AppNotificationManager.Default.Register();
                WriteAppLog("Notifications registered");
            }
            catch (Exception ex)
            {
                // Регистрация могла уже пройти ранее — это нормально, игнорируем
                WriteAppLog("Notifications register skipped: " + ex.Message);
            }
        }

        private static void ShowStartupReminders()
        {
            try
            {
                DateTime today = DateTime.Today;

                using var db = new AppDbContext();

                // Берём только просроченные и сегодняшние, максимум 5 штук
                var dueNotes = db.ClientNotes
                    .AsNoTracking()
                    .Where(n => n.ReminderDate.HasValue && n.ReminderDate.Value <= today)
                    .OrderBy(n => n.ReminderDate)
                    .Take(5)
                    .ToList();

                if (dueNotes.Count == 0)
                    return;

                // Загружаем имена клиентов одним запросом
                var clientIds = dueNotes.Select(n => n.ClientInfoId).Distinct().ToList();
                var clientMap = db.Clients
                    .AsNoTracking()
                    .Where(c => clientIds.Contains(c.Id))
                    .ToDictionary(c => c.Id, c => c.Name);

                if (dueNotes.Count <= 3)
                {
                    // До трёх напоминаний — показываем каждое отдельным уведомлением
                    foreach (var note in dueNotes)
                    {
                        clientMap.TryGetValue(note.ClientInfoId, out string? clientName);

                        int daysOverdue = (today - note.ReminderDate!.Value.Date).Days;

                        string title = daysOverdue == 0
                            ? "Напоминание на сегодня"
                            : $"Просрочено на {daysOverdue} дн.";

                        // Обрезаем длинный текст заметки чтобы он влез в уведомление
                        string noteText = note.NoteText.Length > 120
                            ? note.NoteText.Substring(0, 120) + "…"
                            : note.NoteText;

                        var notification = new AppNotificationBuilder()
                            .AddText(title)
                            .AddText(clientName ?? "Клиент")
                            .AddText(noteText)
                            .BuildNotification();

                        AppNotificationManager.Default.Show(notification);
                    }
                }
                else
                {
                    // Четыре и больше — показываем одно сводное уведомление
                    var notification = new AppNotificationBuilder()
                        .AddText($"Напоминаний: {dueNotes.Count}")
                        .AddText("Откройте дашборд для просмотра всех напоминаний")
                        .BuildNotification();

                    AppNotificationManager.Default.Show(notification);
                }

                WriteAppLog($"Showed {dueNotes.Count} reminder notification(s)");
            }
            catch (Exception ex)
            {
                // Уведомления не должны ломать приложение
                WriteAppLog("Reminder notifications error: " + ex.Message);
            }
        }

        // ─── Логирование запуска ─────────────────────────────────────────────────

        private static void WriteAppLog(string message)
        {
            try
            {
                string folder = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ClientAccountApp",
                    "Logs");

                Directory.CreateDirectory(folder);

                string path = System.IO.Path.Combine(folder, "startup-log.txt");

                File.AppendAllText(
                    path,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " | " + message + Environment.NewLine);
            }
            catch
            {
                // логирование не должно ломать запуск
            }
        }
    }
}
