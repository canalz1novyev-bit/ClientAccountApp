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
        public static Window? MainAppWindow { get; private set; }

        public App()
        {
            InitializeComponent();
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            WriteAppLog("OnLaunched start");

            RegisterNotifications();

            try
            {
                DatabaseInitializer.Initialize();
                WriteAppLog("DatabaseInitializer ok");
            }
            catch (Exception ex)
            {
                WriteAppLog("DatabaseInitializer error: " + ex);
            }

            // Применяем сохранённую тему до создания окна
            ThemeService.ApplySavedTheme();

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

            MainAppWindow?.DispatcherQueue.TryEnqueue(() =>
            {
                ShowStartupReminders();
            });
        }

        private static void RegisterNotifications()
        {
            try
            {
                AppNotificationManager.Default.Register();
                WriteAppLog("Notifications registered");
            }
            catch (Exception ex)
            {
                WriteAppLog("Notifications register skipped: " + ex.Message);
            }
        }

        private static void ShowStartupReminders()
        {
            try
            {
                DateTime today = DateTime.Today;
                using var db = new AppDbContext();

                var dueNotes = db.ClientNotes
                    .AsNoTracking()
                    .Where(n => n.ReminderDate.HasValue && n.ReminderDate.Value <= today)
                    .OrderBy(n => n.ReminderDate)
                    .Take(5)
                    .ToList();

                if (dueNotes.Count == 0) return;

                var clientIds = dueNotes.Select(n => n.ClientInfoId).Distinct().ToList();
                var clientMap = db.Clients
                    .AsNoTracking()
                    .Where(c => clientIds.Contains(c.Id))
                    .ToDictionary(c => c.Id, c => c.Name);

                if (dueNotes.Count <= 3)
                {
                    foreach (var note in dueNotes)
                    {
                        clientMap.TryGetValue(note.ClientInfoId, out string? clientName);
                        int daysOverdue = (today - note.ReminderDate!.Value.Date).Days;
                        string title = daysOverdue == 0
                            ? "Напоминание на сегодня"
                            : $"Просрочено на {daysOverdue} дн.";
                        string noteText = note.NoteText.Length > 120
                            ? note.NoteText.Substring(0, 120) + "…"
                            : note.NoteText;

                        AppNotificationManager.Default.Show(
                            new AppNotificationBuilder()
                                .AddText(title)
                                .AddText(clientName ?? "Клиент")
                                .AddText(noteText)
                                .BuildNotification());
                    }
                }
                else
                {
                    AppNotificationManager.Default.Show(
                        new AppNotificationBuilder()
                            .AddText($"Напоминаний: {dueNotes.Count}")
                            .AddText("Откройте дашборд для просмотра всех напоминаний")
                            .BuildNotification());
                }

                WriteAppLog($"Showed {dueNotes.Count} reminder notification(s)");
            }
            catch (Exception ex)
            {
                WriteAppLog("Reminder notifications error: " + ex.Message);
            }
        }

        private static void WriteAppLog(string message)
        {
            try
            {
                string folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ClientAccountApp", "Logs");
                Directory.CreateDirectory(folder);
                File.AppendAllText(
                    Path.Combine(folder, "startup-log.txt"),
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " | " + message + Environment.NewLine);
            }
            catch (Exception _ex)
            {
                AppLogger.LogError("App.WriteAppLog", _ex);
            }
        }
    }
}