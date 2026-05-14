using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;

namespace ClientAccountApp
{
    public class DigitalSignature
    {
        [Key]
        public int Id { get; set; }

        public int ClientInfoId { get; set; }

        public ClientInfo? ClientInfo { get; set; }

        public string CertificationAuthority { get; set; } = "";

        public string Comment { get; set; } = "";

        public DateTime IssuedDate { get; set; }

        public DateTime ExpiresDate { get; set; }

        [NotMapped]
        public string DisplayText
        {
            get
            {
                return $"{IssuedDate:dd.MM.yyyy} — {ExpiresDate:dd.MM.yyyy}";
            }
        }

        [NotMapped]
        public string AuthorityDisplayText =>
            string.IsNullOrWhiteSpace(CertificationAuthority)
                ? "УЦ не указан"
                : CertificationAuthority;

        [NotMapped]
        public string CommentDisplayText =>
            string.IsNullOrWhiteSpace(Comment)
                ? "Комментарий не указан"
                : Comment;

        [NotMapped]
        public string StatusText
        {
            get
            {
                int daysLeft = (ExpiresDate.Date - DateTime.Today).Days;

                if (daysLeft < 0)
                    return $"Просрочена на {Math.Abs(daysLeft)} дн.";

                if (daysLeft <= 7)
                    return $"Срочно: {daysLeft} дн.";

                if (daysLeft <= 30)
                    return $"Внимание: {daysLeft} дн.";

                return $"В порядке: {daysLeft} дн.";
            }
        }

        [NotMapped]
        public SolidColorBrush StatusBackgroundBrush
        {
            get
            {
                int daysLeft = (ExpiresDate.Date - DateTime.Today).Days;

                if (daysLeft < 0)
                    return new SolidColorBrush(ColorHelper.FromArgb(255, 130, 46, 46));

                if (daysLeft <= 7)
                    return new SolidColorBrush(ColorHelper.FromArgb(255, 170, 92, 35));

                if (daysLeft <= 30)
                    return new SolidColorBrush(ColorHelper.FromArgb(255, 160, 125, 40));

                return new SolidColorBrush(ColorHelper.FromArgb(255, 55, 125, 82));
            }
        }

        [NotMapped]
        public SolidColorBrush StatusForegroundBrush =>
            new SolidColorBrush(Colors.White);
    }
}