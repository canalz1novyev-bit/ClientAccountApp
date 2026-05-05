using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ClientAccountApp
{
    public class ClientNote
    {
        [Key]
        public int Id { get; set; }

        public int ClientInfoId { get; set; }

        public ClientInfo? ClientInfo { get; set; }

        public string NoteText { get; set; } = "";

        public DateTime CreatedAt { get; set; }

        // Дата напоминания — если null, напоминание не установлено
        public DateTime? ReminderDate { get; set; }

        [NotMapped]
        public string CreatedAtText =>
            CreatedAt.ToString("dd.MM.yyyy HH:mm");

        [NotMapped]
        public bool HasReminder => ReminderDate.HasValue;

        [NotMapped]
        public bool IsReminderDue =>
            ReminderDate.HasValue && ReminderDate.Value.Date <= DateTime.Today;

        [NotMapped]
        public string ReminderDateText =>
            ReminderDate.HasValue
                ? $"Напоминание: {ReminderDate.Value:dd.MM.yyyy}"
                : "";

        [NotMapped]
        public string ReminderBadgeText =>
            ReminderDate.HasValue && IsReminderDue
                ? "! Напоминание сегодня"
                : ReminderDateText;
    }
}